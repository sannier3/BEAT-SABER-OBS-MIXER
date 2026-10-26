using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace OBSMixer.Core
{
    public sealed class MixerStore : IDisposable
    {
        private readonly ObsConnection connection;
        private readonly object sync = new object();
        private Dictionary<string, InputState> inputs = new Dictionary<string, InputState>();
        private readonly Dictionary<string, MeterState> meters = new Dictionary<string, MeterState>();
        private readonly List<KeyValuePair<string, JObject>> changes = new List<KeyValuePair<string, JObject>>();
        private readonly SemaphoreSlim refreshSignal = new SemaphoreSlim(0, 1);
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly Task worker;
        private bool refreshing;
        private int generation;
        private volatile bool activeSceneOnly = true;
        private string sceneName = "";
        private HashSet<string> sceneSources = new HashSet<string>();
        private HashSet<string> pinned = new HashSet<string>();
        private readonly Dictionary<string, DateTime> audible = new Dictionary<string, DateTime>();
        public bool IncludeRecentlyAudible { get; set; }
        public void SetPinned(IEnumerable<string> keys) { lock (sync) pinned = new HashSet<string>(keys); }
        public InputState[] AllSnapshot() { lock (sync) return inputs.Values.Select(x => x.Clone()).OrderBy(x => x.Name).ToArray(); }
        public bool ActiveSceneOnly { get => activeSceneOnly; set { if (activeSceneOnly == value) return; activeSceneOnly = value; Refresh(); } }
        public string SceneName { get { lock (sync) return sceneName; } }
        public event Action<string> Diagnostic;
        public MixerStore(ObsConnection connection)
        {
            this.connection = connection;
            connection.EventReceived += OnEvent;
            connection.Identified += Refresh;
            connection.StateChanged += OnConnection;
            worker = Task.Run(RefreshLoopAsync);
        }
        public InputState[] Snapshot()
        {
            lock (sync)
            {
                var now = DateTime.UtcNow;
                return inputs.Values.Where(x => !activeSceneOnly || sceneSources.Contains(x.Name) || pinned.Contains(x.Key)
                    || IncludeRecentlyAudible && audible.TryGetValue(x.Key, out var seen) && (now - seen).TotalSeconds < 1.5)
                    .Select(x => x.Clone()).OrderBy(x => x.Name).ToArray();
            }
        }
        public MeterState Meter(string key) { lock (sync) return meters.TryGetValue(key, out var value) ? value : null; }
        public void Refresh()
        {
            if (lifetime.IsCancellationRequested) return;
            try { refreshSignal.Release(); } catch (SemaphoreFullException) { }
        }
        private void OnConnection(ConnectionState state)
        {
            if (state == ConnectionState.Connected) return;
            lock (sync) { generation++; inputs.Clear(); meters.Clear(); audible.Clear(); sceneSources.Clear(); sceneName = ""; }
        }
        private static readonly HashSet<string> StructuralEvents = new HashSet<string> {
            "CurrentProgramSceneChanged", "SceneCreated", "SceneRemoved", "SceneNameChanged", "SceneItemCreated",
            "SceneItemRemoved", "SceneItemEnableStateChanged", "InputCreated", "InputRemoved", "InputNameChanged",
            "CurrentSceneCollectionChanged" };
        private void OnEvent(string type, JObject data)
        {
            if (type == "InputVolumeMeters")
            {
                lock (sync)
                foreach (JObject item in data["inputs"] as JArray ?? new JArray())
                {
                    var input = Find(inputs, item);
                    if (input != null && item["inputLevelsMul"] is JArray channels)
                    {
                        var meter = MeterState.Parse(channels);
                        meters[input.Key] = meter;
                        if (Math.Max(meter.LeftPeak, meter.RightPeak) > .02) audible[input.Key] = meter.ReceivedUtc;
                    }
                }
                return;
            }
            lock (sync)
            {
                Apply(inputs, type, data);
                if (refreshing && changes.Count < 10000) changes.Add(new KeyValuePair<string, JObject>(type, (JObject)data.DeepClone()));
            }
            if (StructuralEvents.Contains(type)) Refresh();
        }
        private static InputState Find(Dictionary<string, InputState> target, JObject data)
        {
            var uuid = (string)data["inputUuid"];
            if (uuid != null && target.TryGetValue(uuid, out var input)) return input;
            var name = (string)data["inputName"];
            return target.Values.FirstOrDefault(x => x.Name == name || x.Name == (string)data["oldInputName"]);
        }
        private static void Apply(Dictionary<string, InputState> target, string type, JObject data)
        {
            var input = Find(target, data);
            if (input == null) return;
            switch (type)
            {
                case "InputVolumeChanged": input.VolumeDb = (double)data["inputVolumeDb"]; break;
                case "InputMuteStateChanged": input.Muted = (bool)data["inputMuted"]; break;
                case "InputAudioSyncOffsetChanged": input.OffsetMs = (long)data["inputAudioSyncOffset"]; break;
                case "InputAudioTracksChanged": input.Tracks = (JObject)data["inputAudioTracks"].DeepClone(); break;
                case "InputAudioMonitorTypeChanged": input.MonitorType = (string)data["monitorType"]; break;
                case "InputNameChanged": input.Name = (string)data["inputName"]; break;
                case "InputRemoved": target.Remove(input.Key); break;
            }
        }
        private async Task RefreshLoopAsync()
        {
            var token = lifetime.Token;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await refreshSignal.WaitAsync(token).ConfigureAwait(false);
                    await Task.Delay(150, token).ConfigureAwait(false);
                    while (refreshSignal.Wait(0)) { }
                    if (connection.State != ConnectionState.Connected) continue;
                    int refreshGeneration;
                    bool filter = ActiveSceneOnly;
                    lock (sync) { refreshing = true; changes.Clear(); refreshGeneration = generation; }
                    try
                    {
                        var list = await connection.RequestAsync("GetInputList", token: token).ConfigureAwait(false);
                        var program = await connection.RequestAsync("GetCurrentProgramScene", token: token).ConfigureAwait(false);
                        var special = await connection.RequestAsync("GetSpecialInputs", token: token).ConfigureAwait(false);
                        string scene = (string)program["currentProgramSceneName"];
                        var visible = new HashSet<string>(StringComparer.Ordinal);
                        foreach (var property in special.Properties())
                            if (property.Value.Type == JTokenType.String) visible.Add((string)property.Value);
                        if (filter) await WalkSceneAsync(scene, false, visible, new HashSet<string>(), token).ConfigureAwait(false);
                        var next = new Dictionary<string, InputState>();
                        foreach (JObject item in list["inputs"] as JArray ?? new JArray())
                        {
                            string name = (string)item["inputName"];
                            var input = new InputState { Name = name, Uuid = (string)item["inputUuid"], Key = ObsProtocol.Key(item) };
                            try { await ReadInputAsync(input, token).ConfigureAwait(false); }
                            catch (ObsRequestException ex) when (ex.Code == 604 || ex.Code == 600) { continue; }
                            next[input.Key] = input;
                        }
                        lock (sync)
                        {
                            if (connection.State == ConnectionState.Connected && refreshGeneration == generation)
                            {
                                foreach (var change in changes) Apply(next, change.Key, change.Value);
                                inputs = next; sceneName = scene; sceneSources = visible;
                                foreach (var key in meters.Keys.Where(k => !next.ContainsKey(k)).ToArray()) { meters.Remove(key); audible.Remove(key); }
                            }
                        }
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                    catch (Exception ex)
                    {
                        Diagnostic?.Invoke("Mixer synchronization failed: " + ex.GetType().Name);
                        if (connection.State == ConnectionState.Connected)
                        { await Task.Delay(2000, token).ConfigureAwait(false); Refresh(); }
                    }
                    finally { lock (sync) { refreshing = false; changes.Clear(); } }
                }
            }
            catch (OperationCanceledException) { }
        }
        private async Task WalkSceneAsync(string name, bool group, HashSet<string> visible, HashSet<string> visited, CancellationToken token)
        {
            if (name == null || !visited.Add(name)) return;
            var data = await connection.RequestAsync(group ? "GetGroupSceneItemList" : "GetSceneItemList",
                new JObject { ["sceneName"] = name }, token).ConfigureAwait(false);
            foreach (JObject item in data["sceneItems"] as JArray ?? new JArray())
            {
                if ((bool?)item["sceneItemEnabled"] == false) continue;
                string source = (string)item["sourceName"];
                if (source == null) continue;
                visible.Add(source);
                bool isGroup = (bool?)item["isGroup"] == true;
                if (isGroup || (string)item["sourceType"] == "OBS_SOURCE_TYPE_SCENE")
                    await WalkSceneAsync(source, isGroup, visible, visited, token).ConfigureAwait(false);
            }
        }
        private async Task ReadInputAsync(InputState input, CancellationToken token)
        {
            var selector = ObsProtocol.Selector(input);
            var volume = await connection.RequestAsync("GetInputVolume", selector, token).ConfigureAwait(false);
            input.VolumeDb = (double)volume["inputVolumeDb"];
            input.Muted = (bool)(await connection.RequestAsync("GetInputMute", selector, token).ConfigureAwait(false))["inputMuted"];
            input.OffsetMs = (long)(await connection.RequestAsync("GetInputAudioSyncOffset", selector, token).ConfigureAwait(false))["inputAudioSyncOffset"];
            input.Tracks = (JObject)(await connection.RequestAsync("GetInputAudioTracks", selector, token).ConfigureAwait(false))["inputAudioTracks"];
            input.MonitorType = (string)(await connection.RequestAsync("GetInputAudioMonitorType", selector, token).ConfigureAwait(false))["monitorType"];
        }
        public async Task SetAsync(InputState input, string request, string field, JToken value, CancellationToken token = default)
        {
            var data = ObsProtocol.Selector(input);
            data[field] = value;
            try { await connection.RequestAsync(request, data, token).ConfigureAwait(false); }
            catch { Refresh(); throw; }
        }
        public void Dispose()
        {
            connection.EventReceived -= OnEvent;
            connection.Identified -= Refresh;
            connection.StateChanged -= OnConnection;
            if (lifetime.IsCancellationRequested) return;
            lifetime.Cancel();
            _ = worker.ContinueWith(_ => { lifetime.Dispose(); refreshSignal.Dispose(); }, TaskScheduler.Default);
        }
    }
}
