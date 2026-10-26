using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace OBSMixer.Core
{
    public sealed class ObsPreviewFrame
    {
        public byte[] Image { get; }
        public string SceneName { get; }
        internal ObsPreviewFrame(byte[] image, string sceneName) { Image = image; SceneName = sceneName; }
    }

    public sealed class ObsPreview : IDisposable
    {
        private readonly ObsConnection connection;
        private readonly object sync = new object();
        private CancellationTokenSource capture;
        private ObsPreviewFrame frame;
        private bool active, busy, failed, disposed;
        private int intervalMs = 100;
        private long generation;
        private DateTime nextCapture;
        public event Action<string> Diagnostic;
        public ObsPreview(ObsConnection connection)
        {
            this.connection = connection;
            connection.StateChanged += ConnectionChanged;
            connection.EventReceived += OnEvent;
        }
        public bool Active
        {
            get { lock (sync) return active; }
            set
            {
                lock (sync)
                {
                    if (disposed || active == value) return;
                    active = value; Invalidate();
                }
            }
        }
        public int IntervalMs
        {
            get { lock (sync) return intervalMs; }
            set
            {
                lock (sync)
                {
                    int next = Math.Max(100, Math.Min(10000, value));
                    if (intervalMs == next) return;
                    intervalMs = next; nextCapture = DateTime.MinValue;
                }
            }
        }
        public ObsPreviewFrame Frame { get { lock (sync) return frame; } }
        public bool Failed { get { lock (sync) return failed; } }
        private void Invalidate()
        {
            generation++; frame = null; failed = false; nextCapture = DateTime.MinValue;
            capture?.Cancel();
        }
        private void ConnectionChanged(ConnectionState state) { lock (sync) { if (!disposed) Invalidate(); } }
        private void OnEvent(string type, JObject data)
        {
            if (type != "CurrentProgramSceneChanged" && type != "CurrentSceneCollectionChanging") return;
            lock (sync) { if (!disposed) Invalidate(); }
        }
        public async Task RefreshAsync(DateTime now)
        {
            long version;
            CancellationTokenSource cancellation;
            lock (sync)
            {
                if (disposed || !active || busy || connection.State != ConnectionState.Connected || now < nextCapture) return;
                busy = true; version = generation;
                nextCapture = now.AddMilliseconds(intervalMs);
                capture = cancellation = new CancellationTokenSource();
            }
            try
            {
                var program = await connection.RequestAsync("GetCurrentProgramScene", token: cancellation.Token).ConfigureAwait(false);
                string name = (string)program["currentProgramSceneName"];
                if (string.IsNullOrEmpty(name)) throw new InvalidOperationException("OBS returned no Program scene");
                cancellation.Token.ThrowIfCancellationRequested();
                var response = await connection.RequestAsync("GetSourceScreenshot", new JObject {
                    ["sourceName"] = name, ["imageFormat"] = "jpg", ["imageWidth"] = 640,
                    ["imageHeight"] = 360, ["imageCompressionQuality"] = 60
                }, cancellation.Token).ConfigureAwait(false);
                byte[] image = Decode((string)response["imageData"]);
                lock (sync)
                {
                    if (!disposed && active && version == generation && connection.State == ConnectionState.Connected)
                    { frame = new ObsPreviewFrame(image, name); failed = false; }
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (Exception error)
            {
                lock (sync)
                {
                    if (!disposed && active && version == generation) { frame = null; failed = true; }
                }
                Diagnostic?.Invoke("OBS preview failed: " + error.GetType().Name);
            }
            finally
            {
                lock (sync) { if (capture == cancellation) capture = null; busy = false; }
                cancellation.Dispose();
            }
        }
        private static byte[] Decode(string data)
        {
            if (data == null || data.Length > 2 * 1024 * 1024) throw new FormatException("Invalid OBS screenshot size");
            int comma = data.IndexOf(',');
            string header = comma < 0 ? "" : data.Substring(0, comma);
            if (!header.Equals("data:image/jpeg;base64", StringComparison.OrdinalIgnoreCase)
                && !header.Equals("data:image/jpg;base64", StringComparison.OrdinalIgnoreCase)
                && !header.Equals("data:image/png;base64", StringComparison.OrdinalIgnoreCase))
                throw new FormatException("Invalid OBS screenshot format");
            byte[] result = Convert.FromBase64String(data.Substring(comma + 1));
            if (result.Length == 0) throw new FormatException("Empty OBS screenshot");
            return result;
        }
        public void Dispose()
        {
            lock (sync) { if (disposed) return; disposed = true; active = false; Invalidate(); }
            connection.StateChanged -= ConnectionChanged; connection.EventReceived -= OnEvent;
        }
    }
}
