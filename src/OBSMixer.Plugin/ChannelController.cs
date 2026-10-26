using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OBSMixer.Core;
using UnityEngine;

namespace OBSMixer
{
    internal sealed class ChannelController : IDisposable
    {
        private readonly MixerRuntime runtime;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private Task commands = Task.CompletedTask, volumeTask = Task.CompletedTask;
        private int pendingCommands;
        private bool disposed, pumping;
        private double? pendingVolume;
        private float protectUntil;
        internal InputState State { get; private set; }
        internal bool Dragging { get; private set; }
        internal ChannelController(MixerRuntime runtime, InputState input) { this.runtime = runtime; State = input.Clone(); }
        internal void Update(InputState input)
        {
            if (disposed || Volatile.Read(ref pendingCommands) > 0) return;
            double volume = State.VolumeDb;
            State = input.Clone();
            if (Dragging || pendingVolume.HasValue || Time.unscaledTime < protectUntil) State.VolumeDb = volume;
        }
        internal void BeginDrag() { Dragging = true; }
        internal void ChangeVolume(float position)
        {
            var config = runtime.Config;
            State.VolumeDb = FaderCurve.ToDb(position, config.VolumeMinDb, config.VolumeMaxDb, config.FaderTaper);
            pendingVolume = State.VolumeDb; protectUntil = Time.unscaledTime + .5f;
            if (!pumping) { pumping = true; volumeTask = PumpAsync(); runtime.Run(() => volumeTask); }
        }
        internal void EndDrag() { Dragging = false; FlushVolume(); }
        private async Task PumpAsync()
        {
            try
            {
                while (!disposed && pendingVolume.HasValue)
                {
                    FlushVolume(); await commands;
                    await Task.Delay(75, lifetime.Token);
                }
            }
            catch (OperationCanceledException) { }
            finally { pumping = false; }
        }
        private void FlushVolume()
        {
            if (!pendingVolume.HasValue || disposed) return;
            double db = pendingVolume.Value; pendingVolume = null;
            Queue("SetInputVolume", "inputVolumeMul", FaderCurve.Multiplier(db));
        }
        internal void ToggleMute() { State.Muted = !State.Muted; Queue("SetInputMute", "inputMuted", State.Muted); }
        internal void Offset(int amount) { State.OffsetMs = Math.Max(-950, Math.Min(20000, State.OffsetMs + amount)); Queue("SetInputAudioSyncOffset", "inputAudioSyncOffset", State.OffsetMs); }
        internal void Track(int number) { State.Tracks[number.ToString()] = !State.Track(number); Queue("SetInputAudioTracks", "inputAudioTracks", State.Tracks.DeepClone()); }
        internal void Monitor(string mode) { State.MonitorType = mode; Queue("SetInputAudioMonitorType", "monitorType", mode); }
        private void Queue(string request, string field, JToken value)
        {
            if (disposed) return;
            Interlocked.Increment(ref pendingCommands);
            commands = ExecuteAsync(commands, State.Clone(), request, field, value);
        }
        private async Task ExecuteAsync(Task previous, InputState input, string request, string field, JToken value)
        {
            try
            {
                await previous.ConfigureAwait(false);
                if (!lifetime.IsCancellationRequested) await runtime.Store.SetAsync(input, request, field, value, lifetime.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                string message = error is ObsRequestException ? error.Message : L10n.T("Commande OBS échouée : ") + error.GetType().Name;
                Plugin.Log.Warn(message);
                CP_SDK.Unity.MTMainThreadInvoker.Enqueue(() => { if (!disposed && runtime) runtime.LastError = message; });
            }
            finally { Interlocked.Decrement(ref pendingCommands); }
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; Dragging = false; lifetime.Cancel();
            _ = Task.WhenAll(commands, volumeTask).ContinueWith(_ => lifetime.Dispose(), TaskScheduler.Default);
        }
    }
}
