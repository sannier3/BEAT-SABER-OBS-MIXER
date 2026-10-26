using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OBSMixer.Core
{
    public enum ConnectionState { Disconnected, Connecting, Connected, AuthenticationFailed }

    public sealed class ObsConnection : IDisposable
    {
        private sealed class Pending
        {
            public string Type;
            public TaskCompletionSource<JObject> Completion = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        private readonly ConcurrentDictionary<string, Pending> pending = new ConcurrentDictionary<string, Pending>();
        private readonly SemaphoreSlim sendGate = new SemaphoreSlim(1, 1);
        private readonly object lifecycle = new object();
        private CancellationTokenSource lifetime;
        private Task runner;
        private ClientWebSocket socket;
        private bool disposed;
        private volatile ConnectionState state;
        public ConnectionState State => state;
        public event Action<ConnectionState> StateChanged;
        public event Action<string, JObject> EventReceived;
        public event Action Identified;
        public event Action<string> Diagnostic;
        public int RequestTimeoutMs { get; set; } = 7000;
        public bool Reconnect { get; set; } = true;
        private void SetState(ConnectionState value) { state = value; StateChanged?.Invoke(value); }

        public void Start(Uri endpoint, string password)
        {
            lock (lifecycle)
            {
                if (disposed) throw new ObjectDisposedException(nameof(ObsConnection));
                if (runner != null && !runner.IsCompleted) return;
                lifetime?.Dispose();
                lifetime = new CancellationTokenSource();
                runner = Task.Run(() => RunAsync(endpoint, password ?? "", lifetime.Token));
            }
        }
        public async Task StopAsync()
        {
            Task task;
            lock (lifecycle) { lifetime?.Cancel(); socket?.Abort(); task = runner; }
            if (task != null) await task.ConfigureAwait(false);
        }
        private async Task RunAsync(Uri endpoint, string password, CancellationToken token)
        {
            int delay = 500;
            bool authFailed = false;
            while (!token.IsCancellationRequested)
            {
                using (var client = new ClientWebSocket())
                using (var session = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    client.Options.AddSubProtocol("obswebsocket.json");
                    client.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
                    lock (lifecycle) socket = client;
                    try
                    {
                        SetState(ConnectionState.Connecting);
                        session.CancelAfter(TimeSpan.FromSeconds(15));
                        await client.ConnectAsync(endpoint, session.Token).ConfigureAwait(false);
                        bool helloReceived = false, identified = false;
                        while (!session.IsCancellationRequested && client.State == WebSocketState.Open)
                        {
                            string raw = await ReceiveAsync(client, session.Token).ConfigureAwait(false);
                            if (raw == null)
                            {
                                authFailed = (int?)client.CloseStatus == 4009;
                                break;
                            }
                            JObject message;
                            try { message = JObject.Parse(raw); }
                            catch (JsonException) { Diagnostic?.Invoke("Ignored invalid JSON"); continue; }
                            int op = (int?)message["op"] ?? -1;
                            var data = message["d"] as JObject;
                            if (data == null) continue;
                            if (op == 0 && !helloReceived)
                            {
                                helloReceived = true;
                                await SendAsync(client, ObsProtocol.Identify(data, password), session.Token).ConfigureAwait(false);
                            }
                            else if (op == 2 && helloReceived && !identified)
                            {
                                if ((int?)data["negotiatedRpcVersion"] != 1) throw new InvalidOperationException("Unsupported negotiated RPC version");
                                identified = true;
                                session.CancelAfter(Timeout.Infinite);
                                delay = 500;
                                SetState(ConnectionState.Connected);
                                Identified?.Invoke();
                            }
                            else if (op == 7 && identified) Complete(data);
                            else if (op == 5 && identified)
                            {
                                try { EventReceived?.Invoke((string)data["eventType"], data["eventData"] as JObject ?? new JObject()); }
                                catch (Exception) { Diagnostic?.Invoke("Ignored malformed OBS event"); }
                            }
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex) { Diagnostic?.Invoke("OBS connection interrupted: " + ex.GetType().Name); }
                    finally
                    {
                        lock (lifecycle) if (ReferenceEquals(socket, client)) socket = null;
                        client.Abort();
                        SetState(authFailed ? ConnectionState.AuthenticationFailed : ConnectionState.Disconnected);
                        FailPending();
                    }
                }
                if (token.IsCancellationRequested || !Reconnect || authFailed) break;
                try { await Task.Delay(delay, token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                delay = Math.Min(delay * 2, 30000);
            }
        }
        public async Task<JObject> RequestAsync(string type, JObject data = null, CancellationToken token = default)
        {
            ClientWebSocket client;
            lock (lifecycle) client = socket;
            if (state != ConnectionState.Connected || client == null) throw new IOException("OBS disconnected");
            string id = Guid.NewGuid().ToString("N");
            var request = new Pending { Type = type };
            pending[id] = request;
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(RequestTimeoutMs);
                using (timeout.Token.Register(() => request.Completion.TrySetCanceled()))
                try
                {
                    await SendAsync(client, ObsProtocol.Envelope(6, new JObject { ["requestType"] = type,
                        ["requestId"] = id, ["requestData"] = data ?? new JObject() }), timeout.Token).ConfigureAwait(false);
                    return await request.Completion.Task.ConfigureAwait(false);
                }
                finally { pending.TryRemove(id, out _); }
            }
        }
        private void Complete(JObject response)
        {
            string id = (string)response["requestId"];
            if (id == null || !pending.TryGetValue(id, out var request)) return;
            if ((string)response["requestType"] != request.Type) return;
            var status = response["requestStatus"] as JObject;
            if ((bool?)status?["result"] == true)
                request.Completion.TrySetResult(response["responseData"] as JObject ?? new JObject());
            else request.Completion.TrySetException(new ObsRequestException(request.Type, (int?)status?["code"] ?? 0));
        }
        private void FailPending()
        {
            foreach (var pair in pending)
                if (pending.TryRemove(pair.Key, out var request)) request.Completion.TrySetException(new IOException("OBS disconnected"));
        }
        private async Task SendAsync(ClientWebSocket client, JObject message, CancellationToken token)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message.ToString(Formatting.None));
            await sendGate.WaitAsync(token).ConfigureAwait(false);
            try { await client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token).ConfigureAwait(false); }
            finally { sendGate.Release(); }
        }
        private static async Task<string> ReceiveAsync(ClientWebSocket client, CancellationToken token)
        {
            var buffer = new byte[16384];
            using (var stream = new MemoryStream())
            {
                WebSocketReceiveResult part;
                do
                {
                    part = await client.ReceiveAsync(new ArraySegment<byte>(buffer), token).ConfigureAwait(false);
                    if (part.MessageType == WebSocketMessageType.Close) return null;
                    if (part.MessageType != WebSocketMessageType.Text) throw new IOException("Expected OBS JSON text");
                    if (stream.Length + part.Count > 8 * 1024 * 1024) throw new IOException("OBS message too large");
                    stream.Write(buffer, 0, part.Count);
                } while (!part.EndOfMessage);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
        public void Dispose()
        {
            lock (lifecycle)
            {
                if (disposed) return;
                disposed = true;
                lifetime?.Cancel(); socket?.Abort();
            }
            FailPending();
            var source = lifetime;
            var task = runner;
            if (task == null) source?.Dispose();
            else _ = task.ContinueWith(_ => source?.Dispose(), TaskScheduler.Default);
        }
    }
}
