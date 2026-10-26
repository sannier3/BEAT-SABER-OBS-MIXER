using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CP_SDK;
using CP_SDK.UI;
using CP_SDK.UI.Components;
using OBSMixer.Core;
using UnityEngine;

namespace OBSMixer
{
    [DefaultExecutionOrder(100)]
    public sealed partial class MixerRuntime : MonoBehaviour
    {
        internal static MixerRuntime Current { get; private set; }
        internal MixerConfig Config { get; private set; }
        internal ObsConnection Connection { get; private set; }
        internal MixerStore Store { get; private set; }
        internal ObsSceneStore Scenes { get; private set; }
        private readonly MixerVisibility visibility = new MixerVisibility();
        private readonly Dictionary<string, ChannelController> channels = new Dictionary<string, ChannelController>();
        private CFloatingPanel panel, detailsPanel, scenesPanel;
        private MixerView view;
        private ChannelDetailsView details;
        private ScenesView scenesView;
        private PauseTracker pauseTracker;
        private readonly MenuContextTracker menuContext = new MenuContextTracker();
        private volatile bool stopped;
        private float nextState, nextMeter, lastMeter, saveAt, nextLanguage;
        private bool dirty, detailsRequested;
        private MixerScene? grabContext, scenesGrabContext;
        internal int UiRevision { get; private set; }
        internal string LastError = "";
        internal bool InPause => visibility.Scene == MixerScene.Playing && visibility.Paused;
        internal string ConnectionLabel => !Config.Enabled ? L10n.T("DÉSACTIVÉ") : Connection.State == ConnectionState.Connected ? L10n.T("CONNECTÉ") :
            Connection.State == ConnectionState.Connecting ? L10n.T("CONNEXION…") : Connection.State == ConnectionState.AuthenticationFailed ? L10n.T("MOT DE PASSE REFUSÉ") : L10n.T("DÉCONNECTÉ");
        public void Initialize(MixerConfig config)
        {
            Current = this; Config = config;
            L10n.Refresh(config.Language);
            Connection = new ObsConnection { Reconnect = config.AutoReconnect };
            InitializePreview();
            Store = new MixerStore(Connection);
            Scenes = new ObsSceneStore(Connection);
            Connection.Diagnostic += LogDiagnostic; Store.Diagnostic += LogDiagnostic; Scenes.Diagnostic += LogDiagnostic;
            Connection.StateChanged += LogConnectionState;
            panel = UISystem.FloatingPanelFactory.Create("OBS Audio Mixer", null);
            DontDestroyOnLoad(panel.gameObject);
            panel.SetLockIcon(CFloatingPanel.ECorner.TopRight);
            panel.SetSceneTransform(EGenericScene.Menu, config.Menu.Position, config.Menu.Rotation);
            panel.SetSceneTransform(EGenericScene.Playing, config.Playing.Position, config.Playing.Rotation);
            panel.OnGrab(_ => grabContext = visibility.Scene);
            panel.OnSceneRelease(EGenericScene.Menu, (p, r) => Released(false, p, r));
            panel.OnSceneRelease(EGenericScene.Playing, (p, r) => Released(false, p, r));
            panel.OnRelease(_ => grabContext = null);
            view = UISystem.CreateViewController<MixerView>(); view.Runtime = this; panel.SetViewController(view);
            pauseTracker = new PauseTracker(this);
            ChatPlexSDK.OnGenericSceneChange += SceneChanged;
            visibility.Enabled = config.Enabled; visibility.Requested = config.PanelVisible;
            ApplySettings(); SceneChanged(ChatPlexSDK.ActiveGenericScene);
            if (config.Enabled && config.AutoConnect) Run(ReconnectAsync);
            Plugin.Log.Info("OBS Mixer initialized: menu / pause only");
        }
        internal void ApplySettings()
        {
            Config.Normalize();
            visibility.Enabled = Config.Enabled; visibility.Requested = Config.PanelVisible;
            Connection.Reconnect = Config.AutoReconnect;
            Store.ActiveSceneOnly = Config.ActiveSceneOnly;
            Store.IncludeRecentlyAudible = Config.IncludeRecentlyAudible;
            Store.SetPinned(Config.PinnedSources);
            panel.SetSize(new Vector2(Config.PanelWidth, Config.PanelHeight)).SetRadius(Config.CurvatureRadius)
                .SetBackground(true, new Color(.025f, .04f, .07f, Config.BackgroundOpacity));
            panel.RTransform.localScale = Vector3.one * (.02f * Config.PanelScale);
            if (panel.GetAllowMovement() == Config.PanelLocked) panel.SetAllowMovement(!Config.PanelLocked);
            PositionDetails(); details?.ApplyAppearance();
            Scenes.Enabled = Config.ScenesPanelEnabled && Config.Enabled;
            if (Config.ScenesPanelEnabled) EnsureScenesPanel();
            if (scenesPanel)
            {
                scenesPanel.SetRadius(Config.CurvatureRadius).SetBackground(true, new Color(.025f, .04f, .07f, Config.BackgroundOpacity));
                scenesPanel.RTransform.localScale = Vector3.one * (.02f * Config.PanelScale);
                if (scenesPanel.GetAllowMovement() == Config.ScenesPanelLocked) scenesPanel.SetAllowMovement(!Config.ScenesPanelLocked);
            }
            ApplyPreviewSettings();
            UiRevision++; SaveSoon(); ApplyVisibility();
        }
        internal void SetLanguage(string preference)
        {
            Config.Language = preference; Config.Normalize();
            RefreshLanguage(); SaveSoon();
        }
        private void RefreshLanguage()
        {
            if (!L10n.Refresh(Config.Language)) return;
            UiRevision++; details?.ApplyAppearance();
        }
        internal void SetEnabled(bool enabled)
        {
            SavePose(); Config.Enabled = enabled; visibility.Enabled = enabled; Scenes.Enabled = Config.ScenesPanelEnabled && enabled; SaveSoon(); ApplyVisibility();
            if (!enabled) { foreach (var channel in channels.Values) channel.EndDrag(); Run(Connection.StopAsync); }
            else if (Config.AutoConnect) Run(ReconnectAsync);
        }
        internal void SetScenesEnabled(bool enabled)
        {
            SavePose(); Config.ScenesPanelEnabled = enabled; ApplySettings();
        }
        internal void Show(bool visible)
        {
            if (!visible) SavePose();
            Config.PanelVisible = visible; visibility.Requested = visible; SaveSoon(); ApplyVisibility();
        }
        private void SceneChanged(EGenericScene scene)
        {
            foreach (var channel in channels.Values) channel.EndDrag();
            CloseDetails();
            if (scene == EGenericScene.Menu) menuContext.Reset();
            visibility.Enter(scene == EGenericScene.Menu ? (menuContext.IsSelecting() == true ? MixerScene.SongSelection : MixerScene.Menu)
                : scene == EGenericScene.Playing ? MixerScene.Playing : MixerScene.None);
            if (panel.GetAllowMovement() == Config.PanelLocked) panel.SetAllowMovement(!Config.PanelLocked);
            if (scenesPanel && scenesPanel.GetAllowMovement() == Config.ScenesPanelLocked) scenesPanel.SetAllowMovement(!Config.ScenesPanelLocked);
            ApplyPreviewPose();
            if (visibility.ContextVisible && Config.ScenesPanelEnabled) Scenes.Refresh();
            ApplyContextPose(); ApplyVisibility();
        }
        internal void Pause()
        {
            visibility.Pause();
            if (!InPause) return;
            panel.SetAllowMovement(!Config.PanelLocked);
            panel.SetSceneTransform(EGenericScene.Playing, Config.Playing.Position, Config.Playing.Rotation);
            if (scenesPanel)
            {
                scenesPanel.SetSceneTransform(EGenericScene.Playing, Config.ScenesPlaying.Position, Config.ScenesPlaying.Rotation);
                scenesPanel.SetAllowMovement(!Config.ScenesPanelLocked);
                Scenes.Refresh();
            }
            ApplyVisibility();
        }
        internal void Resume()
        {
            SavePose(); foreach (var channel in channels.Values) channel.EndDrag();
            CloseDetails(); visibility.Resume(); ApplyVisibility();
        }
        private void ApplyVisibility()
        {
            if (panel) panel.gameObject.SetActive(visibility.Visible);
            if (detailsPanel) detailsPanel.gameObject.SetActive(visibility.Visible && detailsRequested);
            if (scenesPanel) scenesPanel.gameObject.SetActive(visibility.ContextVisible && Config.ScenesPanelEnabled);
            ApplyPreviewVisibility();
        }
        private void SavePose()
        {
            SavePreviewPose();
            if (panel && panel.gameObject.activeInHierarchy && visibility.Visible && (!grabContext.HasValue || grabContext == visibility.Scene))
            {
                var pose = CurrentPose(false);
                pose.Set(panel.RTransform.localPosition, panel.RTransform.localEulerAngles); SaveSoon();
            }
            if (scenesPanel && scenesPanel.gameObject.activeInHierarchy && visibility.ContextVisible
                && (!scenesGrabContext.HasValue || scenesGrabContext == visibility.Scene))
            {
                var pose = CurrentPose(true);
                pose.Set(scenesPanel.RTransform.localPosition, scenesPanel.RTransform.localEulerAngles); SaveSoon();
            }
        }
        private PanelPose CurrentPose(bool scenes)
        {
            if (visibility.Scene == MixerScene.Playing) return scenes ? Config.ScenesPlaying : Config.Playing;
            if (visibility.Scene == MixerScene.SongSelection) return scenes ? Config.ScenesSongSelection : Config.SongSelection;
            return scenes ? Config.ScenesMenu : Config.Menu;
        }
        private void Released(bool scenes, Vector3 position, Vector3 rotation)
        {
            var grabbed = scenes ? scenesGrabContext : grabContext;
            bool visible = scenes ? visibility.ContextVisible && Config.ScenesPanelEnabled : visibility.Visible;
            if (visible && (!grabbed.HasValue || grabbed == visibility.Scene))
            { CurrentPose(scenes).Set(position, rotation); SaveSoon(); }
            else ApplyContextPose();
        }
        private void ApplyContextPose()
        {
            var scene = visibility.Scene == MixerScene.Playing ? EGenericScene.Playing : EGenericScene.Menu;
            var pose = CurrentPose(false);
            panel.SetSceneTransform(scene, pose.Position, pose.Rotation);
            if (scenesPanel)
            {
                pose = CurrentPose(true);
                scenesPanel.SetSceneTransform(scene, pose.Position, pose.Rotation);
            }
        }
        private void UpdateMenuContext()
        {
            if (ChatPlexSDK.ActiveGenericScene != EGenericScene.Menu) return;
            bool? selecting = menuContext.IsSelecting();
            if (!selecting.HasValue) return;
            var target = selecting.Value ? MixerScene.SongSelection : MixerScene.Menu;
            if (visibility.Scene == target) return;
            SavePose();
            foreach (var channel in channels.Values) channel.EndDrag();
            CloseDetails(); visibility.Enter(target); ApplyContextPose(); ApplyVisibility();
        }
        internal void SaveSoon() { dirty = true; saveAt = Time.unscaledTime + .5f; }
        internal void ResetPosition()
        {
            if (!visibility.ContextVisible) return;
            var pose = MixerConfig.DefaultMixerPose(visibility.Scene);
            var scene = InPause ? EGenericScene.Playing : EGenericScene.Menu;
            panel.SetSceneTransform(scene, pose.Position, pose.Rotation);
            if (InPause) Config.Playing = pose;
            else if (visibility.Scene == MixerScene.SongSelection) Config.SongSelection = pose;
            else Config.Menu = pose;
            SaveSoon();
        }
        internal ChannelController Channel(string key) => key != null && channels.TryGetValue(key, out var channel) ? channel : null;
        internal InputState[] VisibleInputs() => SourceSelection.Select(Store.Snapshot(), Config.HiddenSources,
            Config.PinnedSources, Config.SourceOrder, Config.MaxDisplayedSources);
        private void UpdateChannels()
        {
            var inputs = Store.AllSnapshot(); var keys = new HashSet<string>(inputs.Select(x => x.Key));
            foreach (var key in channels.Keys.Where(k => !keys.Contains(k)).ToArray()) { channels[key].Dispose(); channels.Remove(key); }
            foreach (var input in inputs)
            {
                if (!channels.TryGetValue(input.Key, out var channel)) channels[input.Key] = new ChannelController(this, input);
                else channel.Update(input);
            }
        }
        internal void OpenDetails(string key)
        {
            if (!visibility.Visible || Channel(key) == null) return;
            if (!detailsPanel)
            {
                detailsPanel = UISystem.FloatingPanelFactory.Create("OBS source details", null);
                DontDestroyOnLoad(detailsPanel.gameObject);
                detailsPanel.gameObject.SetActive(false);
                detailsPanel.SetSize(new Vector2(110, 85)).SetAllowMovement(false);
                details = UISystem.CreateViewController<ChannelDetailsView>();
                details.Runtime = this;
                details.SourceKey = key;
                detailsPanel.SetViewController(details);
                var canvas = detailsPanel.GetComponent<Canvas>();
                var mainCanvas = panel.GetComponent<Canvas>();
                if (canvas) { canvas.overrideSorting = true; canvas.sortingOrder = (mainCanvas ? mainCanvas.sortingOrder : 3) + 10; }
            }
            details.SourceKey = key;
            detailsRequested = true;
            PositionDetails(); ApplyVisibility(); details.RenderState();
        }
        private void PositionDetails()
        {
            if (!detailsPanel) return;
            detailsPanel.SetRadius(Config.CurvatureRadius).SetBackground(true, new Color(.025f, .04f, .07f, Mathf.Max(.95f, Config.BackgroundOpacity)));
            FollowDetails();
        }
        private void FollowDetails()
        {
            if (!panel || !detailsPanel) return;
            var target = detailsPanel.RTransform;
            target.position = panel.RTransform.position - panel.RTransform.forward * .12f;
            target.rotation = panel.RTransform.rotation;
            target.localScale = panel.RTransform.lossyScale;
        }
        private void LateUpdate()
        {
            if (!stopped && detailsRequested && visibility.Visible) FollowDetails();
        }
        internal void CloseDetails()
        {
            detailsRequested = false;
            if (detailsPanel) detailsPanel.gameObject.SetActive(false);
        }
        private void EnsureScenesPanel()
        {
            if (scenesPanel) return;
            scenesPanel = UISystem.FloatingPanelFactory.Create("OBS scenes", null);
            DontDestroyOnLoad(scenesPanel.gameObject);
            scenesPanel.gameObject.SetActive(false);
            scenesPanel.SetSize(new Vector2(110, 96)).SetLockIcon(CFloatingPanel.ECorner.TopRight);
            scenesPanel.SetSceneTransform(EGenericScene.Menu, Config.ScenesMenu.Position, Config.ScenesMenu.Rotation);
            scenesPanel.SetSceneTransform(EGenericScene.Playing, Config.ScenesPlaying.Position, Config.ScenesPlaying.Rotation);
            scenesPanel.OnGrab(_ => scenesGrabContext = visibility.Scene);
            scenesPanel.OnSceneRelease(EGenericScene.Menu, (p, r) => Released(true, p, r));
            scenesPanel.OnSceneRelease(EGenericScene.Playing, (p, r) => Released(true, p, r));
            scenesPanel.OnRelease(_ => scenesGrabContext = null);
            scenesView = UISystem.CreateViewController<ScenesView>(); scenesView.Runtime = this;
            scenesPanel.SetViewController(scenesView);
            ApplyContextPose();
        }
        internal async Task SelectSceneAsync(string key)
        {
            await Scenes.SelectAsync(key).ConfigureAwait(false);
            Store.Refresh();
        }
        private void LogConnectionState(ConnectionState state) => Plugin.Log.Info("OBS " + state);
        private void LogDiagnostic(string message) => Plugin.Log.Debug(message);
        internal async Task ReconnectAsync()
        {
            LastError = ""; await Connection.StopAsync().ConfigureAwait(false);
            if (!stopped && Config.Enabled) Connection.Start(Config.Endpoint, Config.Password);
        }
        internal void Run(Func<Task> operation) { _ = ObserveAsync(operation); }
        private async Task ObserveAsync(Func<Task> operation)
        {
            try { await operation().ConfigureAwait(false); }
            catch (Exception ex)
            {
                string message = ex is ObsRequestException ? ex.Message : "OBS : " + ex.GetType().Name;
                Plugin.Log.Warn(message);
                CP_SDK.Unity.MTMainThreadInvoker.Enqueue(() => { if (!stopped && this) LastError = message; });
            }
        }
        private void Update()
        {
            if (stopped || !panel || !view) return;
            if (Input.GetKeyDown(KeyCode.F8)) Show(!Config.PanelVisible);
            if (Input.GetKeyDown(KeyCode.F9)) { ResetPosition(); Show(true); }
            if (visibility.Visible && Config.PanelLocked == panel.GetAllowMovement()) { Config.PanelLocked = !panel.GetAllowMovement(); SaveSoon(); }
            if (scenesPanel && scenesPanel.gameObject.activeInHierarchy && Config.ScenesPanelLocked == scenesPanel.GetAllowMovement())
            { Config.ScenesPanelLocked = !scenesPanel.GetAllowMovement(); SaveSoon(); }
            float now = Time.unscaledTime;
            UpdatePreview();
            if (now >= nextLanguage) { nextLanguage = now + .5f; RefreshLanguage(); }
            if (dirty && now >= saveAt) { dirty = false; Config.Save(); }
            if (now >= nextState)
            {
                nextState = now + .15f; UpdateMenuContext(); UpdateChannels();
                if (visibility.Visible) { view.RenderState(); if (detailsPanel && detailsPanel.gameObject.activeInHierarchy) details.RenderState(); }
                if (scenesPanel && scenesPanel.gameObject.activeInHierarchy) scenesView.RenderState();
            }
            if (visibility.Visible && now >= nextMeter)
            {
                nextMeter = now + 1f / Config.MeterRefreshRate;
                view.RenderMeters(lastMeter <= 0 ? .05f : now - lastMeter); lastMeter = now;
            }
        }
        private void OnDestroy()
        {
            stopped = true;
            if (Current == this) Current = null;
            ChatPlexSDK.OnGenericSceneChange -= SceneChanged; pauseTracker?.Dispose();
            if (Config != null && dirty) Config.Save();
            foreach (var channel in channels.Values) channel.Dispose(); channels.Clear();
            if (Connection != null) { Connection.Diagnostic -= LogDiagnostic; Connection.StateChanged -= LogConnectionState; }
            if (Store != null) Store.Diagnostic -= LogDiagnostic;
            if (Scenes != null) Scenes.Diagnostic -= LogDiagnostic;
            DestroyPreview();
            Scenes?.Dispose(); Store?.Dispose(); Connection?.Dispose();
            if (scenesPanel) UISystem.DestroyUI(ref scenesPanel, ref scenesView);
            if (detailsPanel) UISystem.DestroyUI(ref detailsPanel, ref details);
            if (panel) UISystem.DestroyUI(ref panel, ref view);
        }
    }
}
