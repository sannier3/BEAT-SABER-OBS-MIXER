using System.IO;
using BeatSaberMarkupLanguage.MenuButtons;
using IPA;
using UnityEngine;
using Logger = IPA.Logging.Logger;

namespace OBSMixer
{
    [Plugin(RuntimeOptions.SingleStartInit)]
    public sealed class Plugin
    {
        internal static Logger Log { get; private set; }
        private MixerRuntime runtime;
        private MenuButton menuButton;
        [Init] public Plugin(Logger logger) { Log = logger; }
        [OnStart] public void Start()
        {
            Log.Info("OBS Mixer loaded; waiting for the main menu");
            CP_SDK.ChatPlexSDK.OnGenericMenuSceneLoaded += EnsureRuntime;
            L10n.Changed += RefreshMenuLanguage;
            if (CP_SDK.ChatPlexSDK.ActiveGenericScene == CP_SDK.EGenericScene.Menu) EnsureRuntime();
        }
        private void EnsureRuntime()
        {
            if (!runtime)
            {
                var root = new GameObject("OBSMixer.Runtime");
                Object.DontDestroyOnLoad(root);
                runtime = root.AddComponent<MixerRuntime>();
                try
                {
                    string path = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "UserData", "OBSMixer.json");
                    runtime.Initialize(MixerConfig.Load(path));
                }
                catch (System.Exception error)
                {
                    Log.Error("OBS Mixer initialization failed"); Log.Error(error);
                    Object.Destroy(root); runtime = null;
                    return;
                }
            }
            if (menuButton != null) return;
            try
            {
                var button = new MenuButton("OBS Mixer", L10n.T("Configurer le mixeur OBS"), () => MixerSettingsFlow.Instance().Present());
                MenuButtons.Instance.RegisterButton(button);
                menuButton = button;
            }
            catch (System.Exception error) { Log.Warn("OBS Mixer menu registration failed: " + error.GetType().Name); }
        }
        [OnExit] public void Exit()
        {
            L10n.Changed -= RefreshMenuLanguage;
            CP_SDK.ChatPlexSDK.OnGenericMenuSceneLoaded -= EnsureRuntime;
            if (menuButton != null)
            {
                try { MenuButtons.Instance.UnregisterButton(menuButton); }
                catch (System.InvalidOperationException) { }
                menuButton = null;
            }
            MixerSettingsFlow.Destroy();
            if (runtime) Object.Destroy(runtime.gameObject);
        }
        private void RefreshMenuLanguage()
        {
            if (menuButton != null) menuButton.HoverHint = L10n.T("Configurer le mixeur OBS");
        }
    }
}
