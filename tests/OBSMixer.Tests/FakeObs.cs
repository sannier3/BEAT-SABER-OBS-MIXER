using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OBSMixer.Core;

namespace OBSMixer.Tests;

internal sealed class FakeObs : IAsyncDisposable
{
    private WebApplication app;
    private WebSocket client;
    private readonly SemaphoreSlim sendGate = new(1, 1);
    private readonly CancellationTokenSource shutdown = new();
    private readonly CancellationToken shutdownToken;
    private readonly object lifecycle = new();
    private Task disposeTask;

    public FakeObs() => shutdownToken = shutdown.Token;

    public Uri Endpoint { get; private set; }
    public string Password { get; set; } = "test-password";
    public int Connections;
    public JObject Identification;
    public readonly ConcurrentQueue<JObject> Requests = new();
    public Func<JObject, Task<JObject>> Handler = request => Task.FromResult(new JObject());
    public TaskCompletionSource<bool> Authenticated = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool HasActiveClient => Volatile.Read(ref client) != null;

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        app = builder.Build();
        app.UseWebSockets();
        app.Run(async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            Interlocked.Increment(ref Connections);
            using var ws = await context.WebSockets.AcceptWebSocketAsync("obswebsocket.json");
            Interlocked.Exchange(ref client, ws);
            var replies = new List<Task>();
            try
            {
                await SendAsync(ws, ObsProtocol.Envelope(0, new JObject
                {
                    ["rpcVersion"] = 1,
                    ["authentication"] = new JObject { ["salt"] = "test-salt", ["challenge"] = "test-challenge" }
                }));
                Identification = await ReceiveAsync(ws, shutdownToken);
                if ((string)Identification?["d"]?["authentication"] != ObsProtocol.Authenticate(Password, "test-salt", "test-challenge"))
                {
                    await ws.CloseOutputAsync((WebSocketCloseStatus)4009, "Authentication failed", shutdownToken);
                    return;
                }

                await SendAsync(ws, ObsProtocol.Envelope(2, new JObject { ["negotiatedRpcVersion"] = 1 }));
                Authenticated.TrySetResult(true);
                while (ws.State == WebSocketState.Open)
                {
                    var frame = await ReceiveAsync(ws, shutdownToken);
                    if (frame == null) break;
                    var request = (JObject)frame["d"];
                    Requests.Enqueue(request);
                    replies.Add(ReplyAsync(ws, request));
                }
            }
            catch (WebSocketException) { }
            catch (OperationCanceledException) { }
            finally
            {
                Interlocked.CompareExchange(ref client, null, ws);
                try { await Task.WhenAll(replies); }
                catch (WebSocketException) { }
                catch (OperationCanceledException) { }
            }
        });
        await app.StartAsync();
        string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.Single();
        Endpoint = new Uri(address.Replace("http:", "ws:"));
    }

    private async Task ReplyAsync(WebSocket ws, JObject request)
    {
        JObject frame;
        try
        {
            var response = await Handler(request).WaitAsync(shutdownToken);
            if (response == null) return;
            frame = new JObject
            {
                ["requestId"] = request["requestId"],
                ["requestType"] = request["requestType"],
                ["requestStatus"] = new JObject { ["result"] = true, ["code"] = 100 },
                ["responseData"] = response
            };
        }
        catch (ObsRequestException error)
        {
            frame = new JObject
            {
                ["requestId"] = request["requestId"],
                ["requestType"] = request["requestType"],
                ["requestStatus"] = new JObject { ["result"] = false, ["code"] = error.Code }
            };
        }
        await SendAsync(ws, ObsProtocol.Envelope(7, frame));
    }

    private WebSocket CurrentClient => Volatile.Read(ref client)
        ?? throw new InvalidOperationException("No active OBS test connection");

    public Task EventAsync(string type, JObject data) => SendAsync(CurrentClient, ObsProtocol.Envelope(5,
        new JObject { ["eventType"] = type, ["eventIntent"] = 8, ["eventData"] = data }));

    public async Task CloseAsync()
    {
        var ws = CurrentClient;
        await sendGate.WaitAsync(shutdownToken);
        try { await ws.CloseOutputAsync(WebSocketCloseStatus.EndpointUnavailable, "Restart", shutdownToken); }
        finally { sendGate.Release(); }
    }

    private async Task SendAsync(WebSocket ws, JObject data)
    {
        var bytes = Encoding.UTF8.GetBytes(data.ToString(Formatting.None));
        await sendGate.WaitAsync(shutdownToken);
        try { await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, shutdownToken); }
        finally { sendGate.Release(); }
    }

    private static async Task<JObject> ReceiveAsync(WebSocket ws, CancellationToken token)
    {
        var buffer = new byte[4096];
        using var stream = new MemoryStream();
        WebSocketReceiveResult part;
        do
        {
            part = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);
            if (part.MessageType == WebSocketMessageType.Close) return null;
            stream.Write(buffer, 0, part.Count);
        } while (!part.EndOfMessage);
        return JObject.Parse(Encoding.UTF8.GetString(stream.ToArray()));
    }

    public ValueTask DisposeAsync()
    {
        lock (lifecycle)
            return new ValueTask(disposeTask ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        shutdown.Cancel();
        try
        {
            if (app != null)
            {
                try { await app.StopAsync(); }
                finally { await app.DisposeAsync(); }
            }
        }
        finally
        {
            shutdown.Dispose();
            sendGate.Dispose();
        }
    }
}
