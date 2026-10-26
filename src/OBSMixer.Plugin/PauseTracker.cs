using System;
using BS_Utils.Utilities;

namespace OBSMixer
{
    internal sealed class PauseTracker : IDisposable
    {
        private readonly MixerRuntime runtime;
        internal PauseTracker(MixerRuntime runtime)
        {
            this.runtime = runtime;
            BSEvents.songPaused += Paused;
            BSEvents.songUnpaused += Resumed;
        }
        private void Paused() => runtime.Pause();
        private void Resumed() => runtime.Resume();
        public void Dispose()
        {
            BSEvents.songPaused -= Paused;
            BSEvents.songUnpaused -= Resumed;
        }
    }
}
