using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OBSMixer.Core;
using UnityEngine;

namespace OBSMixer
{
    public sealed class PanelPose
    {
        public float X = -0.55f, Y = 1.35f, Z = 1.7f, Pitch, Yaw = -12f, Roll;
        [JsonIgnore] public Vector3 Position => new Vector3(X, Y, Z);
        [JsonIgnore] public Vector3 Rotation => new Vector3(Pitch, Yaw, Roll);
        public void Set(Vector3 position, Vector3 rotation)
        { X = position.x; Y = position.y; Z = position.z; Pitch = rotation.x; Yaw = rotation.y; Roll = rotation.z; }
        public PanelPose Copy() => new PanelPose { X = X, Y = Y, Z = Z, Pitch = Pitch, Yaw = Yaw, Roll = Roll };
    }
    public sealed class MixerConfig
    {
        public string Host = "127.0.0.1", Password = "";
        public int Port = 4455, ConfigVersion = 5;
        public string Language = "auto";
        public bool Enabled = true, PanelVisible = true;
        public bool ScenesPanelEnabled, ScenesPanelLocked = true;
        public bool PreviewPanelEnabled, PreviewPanelLocked = true, PreviewImageVisible = false;
        public int PreviewIntervalMs = 100;
        public PanelPose PreviewPose = DefaultPreviewPose();
        public bool AutoConnect = true, AutoReconnect = true, ActiveSceneOnly = true, IncludeRecentlyAudible, PanelLocked = true;
        public float VolumeMinDb = -100, VolumeMaxDb = 0, FaderTaper = 2.3f;
        public float PanelWidth = 180, PanelHeight = 130, PanelScale = .75f, TextSize = 3f, BackgroundOpacity = .85f, CurvatureRadius = 200;
        public int MeterRefreshRate = 20, ChannelsPerPage = 8, MaxDisplayedSources;
        public List<string> PinnedSources = new List<string>(), SourceOrder = new List<string>(), HiddenSources = new List<string>();
        public PanelPose Menu = DefaultMixerPose(MixerScene.Menu), SongSelection = DefaultMixerPose(MixerScene.SongSelection), Playing = DefaultMixerPose(MixerScene.Playing);
        public PanelPose ScenesMenu = DefaultScenesPose(MixerScene.Menu), ScenesSongSelection = DefaultScenesPose(MixerScene.SongSelection), ScenesPlaying = DefaultScenesPose(MixerScene.Playing);
        internal static PanelPose DefaultMixerPose(MixerScene scene)
        {
            if (scene == MixerScene.SongSelection) return new PanelPose {
                X = 3.911031f, Y = 1.47807693f, Z = -.7570708f,
                Pitch = .704390347f, Yaw = 105.227989f, Roll = -2.66824429E-08f
            };
            if (scene == MixerScene.Playing) return new PanelPose {
                X = 2.17660785f, Y = 1.29881346f, Z = .504087031f,
                Pitch = 1.32769728f, Yaw = 82.4513f, Roll = 0
            };
            return new PanelPose {
                X = 3.30075979f, Y = 1.45926321f, Z = 2.369189f,
                Pitch = 1.01765883f, Yaw = 55.7804871f, Roll = -2.66846349E-08f
            };
        }
        internal static PanelPose DefaultScenesPose(MixerScene scene = MixerScene.Menu)
        {
            if (scene == MixerScene.SongSelection) return new PanelPose {
                X = 3.70216346f, Y = 3.136981f, Z = -.7668611f,
                Pitch = 6.92931366f, Yaw = 106.5681f, Roll = -2.15013927E-07f
            };
            if (scene == MixerScene.Playing) return new PanelPose {
                X = 2.04868126f, Y = 3.12417817f, Z = -.621705651f,
                Pitch = 352.5849f, Yaw = 95.09795f, Roll = 0
            };
            return new PanelPose {
                X = 3.75293756f, Y = 1.51107013f, Z = .121926419f,
                Pitch = 3.63191438f, Yaw = 92.46504f, Roll = 4.27745931E-07f
            };
        }
        internal static PanelPose DefaultPreviewPose() => new PanelPose {
            X = 2.72247887f, Y = 3.786717f, Z = 2.05873442f,
            Pitch = 330.638275f, Yaw = 60.0408478f, Roll = 9.796131E-07f
        };
        [JsonIgnore] public string FilePath;
        [JsonIgnore] public Uri Endpoint => new UriBuilder("ws", Host, Port).Uri;
        public static MixerConfig Load(string path)
        {
            MixerConfig config;
            try
            {
                if (!File.Exists(path)) config = new MixerConfig();
                else
                {
                    var json = JObject.Parse(File.ReadAllText(path));
                    config = json.ToObject<MixerConfig>() ?? new MixerConfig();
                    if (json["ConfigVersion"] == null && config.VolumeMinDb == -60 && config.VolumeMaxDb == 0)
                        config.VolumeMinDb = -100;
                    if (((int?)json["ConfigVersion"] ?? 0) < 3 && config.CurvatureRadius == 140)
                        config.CurvatureRadius = 200;
                    if (((int?)json["ConfigVersion"] ?? 0) < 4)
                    {
                        if (json["SongSelection"] == null && json["Menu"] is JObject)
                            config.SongSelection = (config.Menu ?? DefaultMixerPose(MixerScene.Menu)).Copy();
                        if (json["ScenesSongSelection"] == null && json["ScenesMenu"] is JObject)
                            config.ScenesSongSelection = (config.ScenesMenu ?? DefaultScenesPose()).Copy();
                    }
                    config.ConfigVersion = 5;
                }
            }
            catch (Exception)
            {
                try { File.Copy(path, path + ".invalid-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), false); }
                catch (Exception) { Plugin.Log.Warn("Could not back up invalid configuration."); }
                Plugin.Log.Warn("Configuration unreadable, using defaults."); config = new MixerConfig();
            }
            config = config ?? new MixerConfig(); config.FilePath = path;
            config.Normalize();
            return config;
        }
        internal void Normalize()
        {
            Host = string.IsNullOrWhiteSpace(Host) ? "127.0.0.1" : Host.Trim();
            Port = Math.Max(1, Math.Min(65535, Port));
            MeterRefreshRate = Math.Max(5, Math.Min(30, MeterRefreshRate));
            ChannelsPerPage = Math.Max(1, Math.Min(8, ChannelsPerPage));
            MaxDisplayedSources = Math.Max(0, Math.Min(64, MaxDisplayedSources));
            VolumeMinDb = Limit(VolumeMinDb, -100, -1, -100); VolumeMaxDb = Limit(VolumeMaxDb, 0, 26, 0);
            FaderTaper = Limit(FaderTaper, 1, 4, 2.3f);
            PanelWidth = Limit(PanelWidth, 100, 260, 180); PanelHeight = Limit(PanelHeight, 85, 180, 130);
            PanelScale = Limit(PanelScale, .4f, 1.5f, .75f); TextSize = Limit(TextSize, 2.2f, 4.5f, 3f);
            BackgroundOpacity = Limit(BackgroundOpacity, 0, 1, .85f); CurvatureRadius = Limit(CurvatureRadius, 80, 1000, 200);
            Menu = Menu ?? DefaultMixerPose(MixerScene.Menu); Playing = Playing ?? DefaultMixerPose(MixerScene.Playing);
            SongSelection = SongSelection ?? DefaultMixerPose(MixerScene.SongSelection);
            ScenesMenu = ScenesMenu ?? DefaultScenesPose(MixerScene.Menu); ScenesPlaying = ScenesPlaying ?? DefaultScenesPose(MixerScene.Playing);
            ScenesSongSelection = ScenesSongSelection ?? DefaultScenesPose(MixerScene.SongSelection);
            PreviewPose = PreviewPose ?? DefaultPreviewPose();
            PreviewIntervalMs = Math.Max(100, Math.Min(10000, PreviewIntervalMs));
            Language = Language == "fr" || Language == "en" ? Language : "auto";
            PinnedSources = PinnedSources ?? new List<string>(); SourceOrder = SourceOrder ?? new List<string>(); HiddenSources = HiddenSources ?? new List<string>();
        }
        private static float Limit(float value, float min, float max, float fallback) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                string temporary = FilePath + ".tmp";
                File.WriteAllText(temporary, JsonConvert.SerializeObject(this, Formatting.Indented));
                if (File.Exists(FilePath)) File.Replace(temporary, FilePath, null); else File.Move(temporary, FilePath);
            }
            catch (Exception ex) { Plugin.Log.Warn("Configuration save failed: " + ex.GetType().Name); }
        }
    }
}
