using System;

namespace OBSMixer.Core
{
    public enum MixerScene { None, Menu, Playing, SongSelection }

    public sealed class MixerVisibility
    {
        public bool Enabled { get; set; } = true;
        public bool Requested { get; set; } = true;
        public MixerScene Scene { get; private set; }
        public bool Paused { get; private set; }
        public bool ContextVisible => Enabled && (Scene == MixerScene.Menu || Scene == MixerScene.SongSelection || Scene == MixerScene.Playing && Paused);
        public bool Visible => Requested && ContextVisible;
        public void Enter(MixerScene scene) { Scene = scene; Paused = false; }
        public void Pause() { if (Scene == MixerScene.Playing) Paused = true; }
        public void Resume() { Paused = false; }
    }

    public static class FaderCurve
    {
        public static double ToDb(double position, double minimum = -100, double maximum = 0, double taper = 2.3)
        {
            Validate(minimum, maximum, taper);
            return maximum + (minimum - maximum) * Math.Pow(1 - Clamp(position), taper);
        }
        public static double ToPosition(double db, double minimum = -100, double maximum = 0, double taper = 2.3)
        {
            Validate(minimum, maximum, taper);
            return 1 - Math.Pow(Clamp((db - maximum) / (minimum - maximum)), 1 / taper);
        }
        public static double Multiplier(double db) => Math.Pow(10, db / 20);
        private static double Clamp(double value) => double.IsNaN(value) ? 0 : Math.Max(0, Math.Min(1, value));
        private static void Validate(double min, double max, double taper)
        {
            if (double.IsNaN(min) || double.IsInfinity(min) || double.IsNaN(max) || double.IsInfinity(max)
                || min >= max || double.IsNaN(taper) || double.IsInfinity(taper) || taper <= 0)
                throw new ArgumentOutOfRangeException(nameof(taper));
        }
    }

    public static class MixerLayout
    {
        public static int Capacity(int requested) => Math.Max(1, Math.Min(8, requested));
        public static float ChannelWidth(float width, int requested, int sources)
        {
            int slots = Math.Min(Capacity(requested), Math.Max(1, sources));
            return Math.Max(1, (width - 8) / slots - 2);
        }
        public static int PageCount(int sources, int capacity) => Math.Max(1, (Math.Max(0, sources) + Math.Max(1, capacity) - 1) / Math.Max(1, capacity));
        public static int ClampPage(int page, int sources, int capacity) => Math.Max(0, Math.Min(page, PageCount(sources, capacity) - 1));
    }
}
