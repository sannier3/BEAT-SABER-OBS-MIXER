using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace OBSMixer.Core
{
    public sealed class ObsSceneState
    {
        public string Name { get; }
        public string Uuid { get; }
        public string Key => Uuid ?? Name;
        public bool Active { get; }
        public ObsSceneState(string name, string uuid, bool active = false) { Name = name; Uuid = uuid; Active = active; }
    }

    public sealed class ObsSceneStore : IDisposable
    {
        private readonly ObsConnection connection;
        private readonly object sync = new object();
        private readonly SemaphoreSlim signal = new SemaphoreSlim(0, 1);
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly Task worker;
        private ObsSceneState[] scenes = Array.Empty<ObsSceneState>();
        private string activeName, activeUuid;
        private bool enabled, disposed;
        private long revision;
        private int switching;
        public event Action<string> Diagnostic;
        public bool IsSwitching => Volatile.Read(ref switching) != 0;
        public bool Enabled
        {
            get { lock (sync) return enabled; }
            set
            {
                lock (sync)
                {
                    if (disposed || enabled == value) return;
                    enabled = value; revision++;
                    if (!value) { scenes = Array.Empty<ObsSceneState>(); activeName = activeUuid = null; }
                }
                if (value) Refresh();
            }
        }
        public ObsSceneStore(ObsConnection connection)
        {
            this.connection = connection;
            connection.Identified += Refresh;
            connection.StateChanged += ConnectionChanged;
            connection.EventReceived += OnEvent;
            worker = Task.Run(RefreshLoopAsync);
        }
        public ObsSceneState[] Snapshot()
        {
            lock (sync)
                return scenes.Select(s => new ObsSceneState(s.Name, s.Uuid,
                    activeUuid != null && s.Uuid != null ? s.Uuid == activeUuid : s.Name == activeName)).ToArray();
        }
        public void Refresh()
        {
            lock (sync)
            {
                if (disposed || !enabled) return;
                try { signal.Release(); } catch (SemaphoreFullException) { }
            }
        }
        private void ConnectionChanged(ConnectionState state)
        {
            if (state == ConnectionState.Connected) return;
            lock (sync) { revision++; scenes = Array.Empty<ObsSceneState>(); activeName = activeUuid = null; }
        }
        private static ObsSceneState[] Parse(JArray items) => items.OfType<JObject>()
            .Where(s => !string.IsNullOrEmpty((string)s["sceneName"]))
            // OBS numbers from the bottom and returns the list in reverse UI order.
            // Descending indices restore the top-to-bottom order of its Scenes dock.
            // Keep incoming order for older agents that omit sceneIndex (stable sort).
            .OrderByDescending(s => (int?)s["sceneIndex"] ?? int.MinValue)
            .Select(s => new ObsSceneState((string)s["sceneName"], (string)s["sceneUuid"]))
            .GroupBy(s => s.Key, StringComparer.Ordinal).Select(g => g.First()).ToArray();
        private void OnEvent(string type, JObject data)
        {
            lock (sync)
            {
                if (!enabled || disposed) return;
                switch (type)
                {
                    case "CurrentProgramSceneChanged":
                        activeName = (string)data["sceneName"]; activeUuid = (string)data["sceneUuid"]; revision++;
                        if (!scenes.Any(s => activeUuid != null && s.Uuid != null ? s.Uuid == activeUuid : s.Name == activeName)) Refresh();
                        return;
                    case "SceneListChanged":
                        if (data["scenes"] is JArray list) { scenes = Parse(list); revision++; }
                        break;
                    case "SceneNameChanged":
                        string oldName = (string)data["oldSceneName"], newName = (string)data["sceneName"], uuid = (string)data["sceneUuid"];
                        scenes = scenes.Select(s => (uuid != null && s.Uuid != null ? s.Uuid == uuid : s.Name == oldName)
                            ? new ObsSceneState(newName, uuid ?? s.Uuid) : s).ToArray();
                        if (uuid != null && activeUuid == uuid || activeName == oldName) activeName = newName;
                        revision++;
                        break;
                    case "SceneCreated": case "SceneRemoved": case "CurrentSceneCollectionChanged":
                        revision++;
                        break;
                    case "CurrentSceneCollectionChanging":
                        revision++; scenes = Array.Empty<ObsSceneState>(); activeName = activeUuid = null;
                        return;
                    default: return;
                }
            }
            Refresh();
        }
        private async Task RefreshLoopAsync()
        {
            var token = lifetime.Token;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await signal.WaitAsync(token).ConfigureAwait(false);
                    await Task.Delay(100, token).ConfigureAwait(false);
                    while (signal.Wait(0)) { }
                    long before;
                    lock (sync)
                    {
                        if (disposed || !enabled || connection.State != ConnectionState.Connected) continue;
                        before = revision;
                    }
                    try
                    {
                        var data = await connection.RequestAsync("GetSceneList", token: token).ConfigureAwait(false);
                        if (!(data["scenes"] is JArray items)) throw new InvalidOperationException("OBS returned no scene list");
                        bool retry = false;
                        lock (sync)
                        {
                            if (enabled && !disposed && connection.State == ConnectionState.Connected)
                            {
                                if (before != revision) retry = true;
                                else
                                {
                                    scenes = Parse(items);
                                    activeName = (string)data["currentProgramSceneName"];
                                    activeUuid = (string)data["currentProgramSceneUuid"];
                                    revision++;
                                }
                            }
                        }
                        if (retry) Refresh();
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                    catch (Exception error)
                    {
                        Diagnostic?.Invoke("OBS scene synchronization failed: " + error.GetType().Name);
                        if (Enabled && connection.State == ConnectionState.Connected)
                        { await Task.Delay(2000, token).ConfigureAwait(false); Refresh(); }
                    }
                }
            }
            catch (OperationCanceledException) { }
        }
        public async Task SelectAsync(string key, CancellationToken token = default)
        {
            if (Interlocked.CompareExchange(ref switching, 1, 0) != 0) return;
            try
            {
                ObsSceneState selected;
                lock (sync)
                {
                    if (!enabled || disposed) throw new InvalidOperationException("OBS scene selector is disabled");
                    selected = scenes.FirstOrDefault(s => s.Key == key);
                    if (selected == null) throw new InvalidOperationException("OBS scene no longer exists");
                    if (activeUuid != null && selected.Uuid != null ? selected.Uuid == activeUuid : selected.Name == activeName) return;
                }
                using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token))
                {
                    await connection.RequestAsync("SetCurrentProgramScene", new JObject { ["sceneName"] = selected.Name }, cancellation.Token).ConfigureAwait(false);
                    long before;
                    lock (sync) before = revision;
                    var confirmed = await connection.RequestAsync("GetCurrentProgramScene", token: cancellation.Token).ConfigureAwait(false);
                    lock (sync)
                    {
                        if (enabled && !disposed && before == revision && connection.State == ConnectionState.Connected)
                        {
                            activeName = (string)confirmed["currentProgramSceneName"];
                            activeUuid = (string)confirmed["currentProgramSceneUuid"];
                            revision++;
                        }
                    }
                }
            }
            catch { Refresh(); throw; }
            finally { Volatile.Write(ref switching, 0); }
        }
        public void Dispose()
        {
            lock (sync) { if (disposed) return; disposed = true; enabled = false; revision++; }
            connection.Identified -= Refresh;
            connection.StateChanged -= ConnectionChanged;
            connection.EventReceived -= OnEvent;
            lifetime.Cancel();
            _ = worker.ContinueWith(_ => { lifetime.Dispose(); signal.Dispose(); }, TaskScheduler.Default);
        }
    }
}
