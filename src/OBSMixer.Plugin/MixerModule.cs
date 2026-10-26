using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CP_SDK.UI;
using CP_SDK.XUI;
using OBSMixer.Core;
using UnityEngine;

namespace OBSMixer
{
    public sealed class MixerSettingsFlow : FlowCoordinator<MixerSettingsFlow>
    {
        private MixerSettingsView settings;
        private MixerCreditsView credits;
        public override string Title => "OBS Mixer";
        public override void Init()
        {
            settings = UISystem.CreateViewController<MixerSettingsView>();
            credits = UISystem.CreateViewController<MixerCreditsView>();
        }
        protected override (IViewController, IViewController, IViewController) GetInitialViewsController() => (settings, credits, null);
        private void OnDestroy()
        {
            if (settings) UISystem.DestroyUI(ref settings);
            if (credits) UISystem.DestroyUI(ref credits);
        }
    }

    public sealed class MixerSettingsView : ViewController<MixerSettingsView>
    {
        private XUIVScrollView scroll;
        private XUIVLayout content;
        private XUIText status;
        private readonly List<Action> refreshers = new List<Action>();
        private readonly List<IXUIElement> elements = new List<IXUIElement>();
        private string section = "Général", sourceSignature;
        private float nextRefresh;
        private SecretKeyboard secretKeyboard;
        private readonly List<(string Key, XUIPrimaryButton Button)> tabButtons = new List<(string, XUIPrimaryButton)>();
        private int languageRevision = -1;
        private MixerRuntime Runtime => MixerRuntime.Current;
        protected override void OnViewCreation()
        {
            var tabs = XUIHLayout.Make(new[] { "Général", "Apparence", "OBS", "Sources" }.Select(label =>
            {
                var button = XUIPrimaryButton.Make(L10n.T(label), () => { section = label; BuildSection(); }).SetWidth(25).SetHeight(8).SetFontSize(3);
                tabButtons.Add((label, button));
                return (IXUIElement)button;
            }).ToArray()).SetSpacing(2);
            tabs.BuildUI(RTransform);
            tabs.RTransform.anchorMin = new Vector2(0, 1); tabs.RTransform.anchorMax = Vector2.one;
            tabs.RTransform.pivot = new Vector2(.5f, 1); tabs.RTransform.anchoredPosition = Vector2.zero; tabs.RTransform.sizeDelta = new Vector2(0, 9);
            scroll = XUIVScrollView.Make(); scroll.BuildUI(RTransform);
            scroll.RTransform.anchorMin = Vector2.zero; scroll.RTransform.anchorMax = Vector2.one;
            scroll.RTransform.offsetMin = new Vector2(1, 2); scroll.RTransform.offsetMax = new Vector2(-1, -12);
            BuildSection();
        }
        protected override void OnViewActivation() { if (scroll != null) BuildSection(); }
        private void BuildSection()
        {
            languageRevision = L10n.Revision;
            foreach (var tab in tabButtons) tab.Button.SetText(L10n.T(tab.Key));
            if (content != null && content.RTransform) { content.SetActive(false); UnityEngine.Object.Destroy(content.RTransform.gameObject); }
            elements.Clear(); refreshers.Clear(); sourceSignature = null;
            if (!Runtime) { elements.Add(XUIText.Make(L10n.T("OBS Mixer n'est pas encore initialisé."))); FinishSection(); return; }
            var config = Runtime.Config;
            status = XUIText.Make("").SetFontSize(3).OnReady(x => x.TMProUGUI.richText = false); elements.Add(status);
            if (section == "Général")
            {
                elements.Add(XUIText.Make(L10n.T("Langue / Language")).SetFontSize(3));
                elements.Add(XUIHLayout.Make(new[] { "auto", "fr", "en" }.Select(key =>
                    (IXUIElement)XUIPrimaryButton.Make((config.Language == key ? "● " : "") +
                        (key == "auto" ? L10n.T("Automatique (Beat Saber)") : key == "fr" ? "Français" : "English"),
                        () => { Runtime.SetLanguage(key); BuildSection(); })
                        .SetWidth(key == "auto" ? 50 : 25).SetHeight(8).SetFontSize(2.8f)
                        .SetBackgroundColor(config.Language == key ? new Color(.1f, .5f, .85f) : new Color(.12f, .18f, .25f))
                    ).ToArray()).SetSpacing(2));
                elements.Add(XUIText.Make(L10n.T("La langue automatique suit Beat Saber : français si le jeu est en français, anglais dans les autres cas. Le choix manuel est enregistré et appliqué immédiatement.")).SetFontSize(3).SetWrapping(true));
                Toggle(L10n.T("Mod activé"), () => config.Enabled, value => Runtime.SetEnabled(value));
                Toggle(L10n.T("Panneau du mixeur visible"), () => config.PanelVisible, value => Runtime.Show(value));
                Toggle(L10n.T("Fenêtre des scènes OBS"), () => config.ScenesPanelEnabled, value => Runtime.SetScenesEnabled(value));
                Toggle(L10n.T("Fenêtre d'aperçu OBS"), () => config.PreviewPanelEnabled, value => Runtime.SetPreviewEnabled(value));
                Slider(L10n.T("Intervalle aperçu (ms)"), 100, 10000, 100, config.PreviewIntervalMs,
                    value => config.PreviewIntervalMs = Mathf.RoundToInt(value));
                elements.Add(XUIText.Make(L10n.T("L'aperçu montre la sortie Program. L'œil masque l'image et arrête les captures. Sa position est commune à l'accueil, à la sélection des morceaux et à la pause ; il reste masqué pendant une map.")).SetFontSize(3).SetWrapping(true));
                Toggle(L10n.T("Scène active uniquement"), () => config.ActiveSceneOnly, value => { config.ActiveSceneOnly = value; Apply(); });
                Toggle(L10n.T("Inclure les sources récemment audibles"), () => config.IncludeRecentlyAudible, value => { config.IncludeRecentlyAudible = value; Apply(); });
                elements.Add(XUIText.Make(L10n.T("Le mixeur et les scènes ont trois positions indépendantes : accueil de Beat Saber, sélection des morceaux et pause. Elles restent masquées pendant une map.\nOuvre le cadenas et déplace chaque fenêtre dans le contexte voulu pour enregistrer sa position.")).SetFontSize(3).SetWrapping(true));
            }
            else if (section == "Apparence")
            {
                Slider(L10n.T("Largeur de fenêtre"), 100, 260, 5, config.PanelWidth, value => config.PanelWidth = value);
                Slider(L10n.T("Hauteur de fenêtre"), 85, 180, 5, config.PanelHeight, value => config.PanelHeight = value);
                Slider(L10n.T("Échelle globale"), .4f, 1.5f, .05f, config.PanelScale, value => config.PanelScale = value);
                Slider(L10n.T("Taille du texte"), 2.2f, 4.5f, .1f, config.TextSize, value => config.TextSize = value);
                Slider(L10n.T("Opacité du fond"), 0, 1, .05f, config.BackgroundOpacity, value => config.BackgroundOpacity = value);
                Slider(L10n.T("Rayon de courbure"), 80, 1000, 20, config.CurvatureRadius, value => config.CurvatureRadius = value);
                Slider(L10n.T("Sources à afficher"), 0, 64, 1, config.MaxDisplayedSources,
                    value => config.MaxDisplayedSources = Mathf.RoundToInt(value),
                    value => Mathf.RoundToInt(value) == 0 ? L10n.T("Toutes") : Mathf.RoundToInt(value).ToString());
                Slider(L10n.T("Sources par page"), 1, 8, 1, config.ChannelsPerPage, value => config.ChannelsPerPage = Mathf.RoundToInt(value));
                Slider(L10n.T("Courbe des faders"), 1, 4, .1f, config.FaderTaper, value => config.FaderTaper = value);
                elements.Add(XUIText.Make(L10n.T("Sources à afficher limite le total (Toutes = aucune limite). Les sources épinglées et leur ordre déterminent la sélection. Sources par page fixe le nombre de tranches côte à côte. Réduire la largeur les resserre sans réduire leur nombre. Un rayon plus élevé réduit la courbure. Les positions se règlent directement sur le panneau.")).SetFontSize(3).SetWrapping(true));
            }
            else if (section == "OBS")
            {
                TextInput(L10n.T("Hôte OBS"), config.Host, value => {
                    value = value.Trim();
                    if (Uri.CheckHostName(value) == UriHostNameType.Unknown) { Runtime.LastError = L10n.T("Hôte invalide"); return; }
                    config.Host = value; Runtime.SaveSoon();
                });
                TextInput(L10n.T("Port OBS"), config.Port.ToString(), value => {
                    if (!int.TryParse(value, out int port) || port < 1 || port > 65535) { Runtime.LastError = L10n.T("Port invalide"); return; }
                    config.Port = port; Runtime.SaveSoon();
                });
                elements.Add(XUIPrimaryButton.Make(L10n.T("Modifier le mot de passe (masqué)"), () => {
                    if (!secretKeyboard) secretKeyboard = CreateModal<SecretKeyboard>();
                    secretKeyboard.Begin(config.Password, value => { config.Password = value; Runtime.SaveSoon(); });
                    ShowModal(secretKeyboard);
                }).SetHeight(8));
                Toggle(L10n.T("Connexion au lancement"), () => config.AutoConnect, value => { config.AutoConnect = value; Runtime.SaveSoon(); });
                Toggle(L10n.T("Reconnexion automatique"), () => config.AutoReconnect, value => { config.AutoReconnect = value; Runtime.Connection.Reconnect = value; Runtime.SaveSoon(); });
                elements.Add(XUIHLayout.Make(
                    XUIPrimaryButton.Make(L10n.T("Connecter / appliquer"), () => Runtime.Run(Runtime.ReconnectAsync)).SetWidth(55).SetHeight(8),
                    XUIPrimaryButton.Make(L10n.T("Déconnecter"), () => Runtime.Run(Runtime.Connection.StopAsync)).SetWidth(45).SetHeight(8)).SetSpacing(2));
                elements.Add(XUIText.Make(L10n.T("Les nouveaux paramètres de connexion s'appliquent avec Connecter / appliquer. Le mot de passe reste enregistré dans le fichier local du mod.")).SetFontSize(3).SetWrapping(true));
            }
            else BuildSources();
            FinishSection(); RefreshLabels();
        }
        private void FinishSection()
        {
            content = XUIVLayout.Make(elements.ToArray()).SetWidth(108).SetPadding(2).SetSpacing(3);
            content.BuildUI(scroll.Element.Container); scroll.Element.UpdateContentSize();
        }
        private void Apply() { Runtime.ApplySettings(); }
        private void Toggle(string label, Func<bool> read, Action<bool> write)
        {
            var button = XUIPrimaryButton.Make("", () => { write(!read()); RefreshLabels(); }).SetHeight(8).SetFontSize(3);
            elements.Add(button); refreshers.Add(() => button.SetText(label + " : " + (read() ? L10n.T("OUI") : L10n.T("NON"))));
        }
        private void Slider(string label, float min, float max, float step, float value, Action<float> write, Func<float, string> formatter = null)
        {
            var slider = XUISlider.Make().SetMinValue(min).SetMaxValue(max).SetIncrements(step).SetValue(value, false)
                .SetFormatter(formatter ?? (v => v.ToString("0.##", CultureInfo.InvariantCulture))).OnValueChanged(v => { write(v); Apply(); })
                .OnReady(x => { x.LElement.preferredWidth = 58; x.LElement.minWidth = 58; });
            elements.Add(XUIHLayout.Make(XUIText.Make(label).SetFontSize(3).OnReady(x => x.LElement.preferredWidth = 44), slider).SetHeight(8).SetSpacing(1));
        }
        private void TextInput(string label, string value, Action<string> write)
        {
            elements.Add(XUIText.Make(label).SetFontSize(3));
            elements.Add(XUITextInput.Make(label).SetValue(value, false).OnValueChanged(write).OnReady(x => { x.LElement.preferredWidth = 100; x.LElement.preferredHeight = 8; }));
        }
        private string SourceSignature(InputState[] inputs) => string.Join("|", inputs.Select(x => x.Key + ":" + x.Name));
        private void BuildSources()
        {
            var config = Runtime.Config;
            var inputs = Runtime.Store.AllSnapshot().OrderBy(x => { int n = config.SourceOrder.IndexOf(x.Key); return n < 0 ? int.MaxValue : n; }).ThenBy(x => x.Name).ToArray();
            sourceSignature = SourceSignature(inputs.OrderBy(x => x.Name).ToArray());
            elements.Add(XUIText.Make(L10n.T("Épingler garde une source même hors scène. Masquer l'exclut du panneau. Les flèches changent l'ordre dans chaque groupe de sources épinglées ou normales.")).SetFontSize(3).SetWrapping(true));
            if (inputs.Length == 0) elements.Add(XUIText.Make(L10n.T("Connecte OBS pour choisir les sources.")).SetFontSize(3));
            foreach (var input in inputs)
            {
                string key = input.Key;
                var name = XUIText.Make(input.Name).SetFontSize(3).OnReady(x => { x.LElement.preferredWidth = 35; x.TMProUGUI.richText = false; });
                elements.Add(XUIHLayout.Make(name,
                    XUIPrimaryButton.Make(config.PinnedSources.Contains(key) ? L10n.T("Épinglée") : L10n.T("Épingler"), () => { Switch(config.PinnedSources, key); Apply(); BuildSection(); }).SetWidth(24).SetHeight(8).SetFontSize(2.6f),
                    XUIPrimaryButton.Make(config.HiddenSources.Contains(key) ? L10n.T("Masquée") : L10n.T("Visible"), () => { Switch(config.HiddenSources, key); Apply(); BuildSection(); }).SetWidth(23).SetHeight(8).SetFontSize(2.6f),
                    XUIPrimaryButton.Make("↑", () => Move(key, -1, inputs)).SetWidth(10).SetHeight(8),
                    XUIPrimaryButton.Make("↓", () => Move(key, 1, inputs)).SetWidth(10).SetHeight(8)).SetSpacing(1).SetHeight(8));
            }
        }
        private static void Switch(List<string> values, string key) { if (!values.Remove(key)) values.Add(key); }
        private void Move(string key, int direction, InputState[] inputs)
        {
            var order = inputs.Select(x => x.Key).ToList(); int index = order.IndexOf(key), next = index + direction;
            if (index < 0 || next < 0 || next >= order.Count) return;
            order.RemoveAt(index); order.Insert(next, key); Runtime.Config.SourceOrder = order;
            Apply(); BuildSection();
        }
        private void RefreshLabels()
        {
            if (!Runtime) return;
            status?.SetText("OBS : " + Runtime.ConnectionLabel + (Runtime.LastError.Length > 0 ? "\n" + Runtime.LastError : ""));
            foreach (var refresh in refreshers) refresh();
        }
        private void Update()
        {
            if (Time.unscaledTime < nextRefresh || !Runtime || scroll == null) return;
            nextRefresh = Time.unscaledTime + .5f; RefreshLabels();
            if (languageRevision != L10n.Revision) { BuildSection(); return; }
            if (section == "Sources" && sourceSignature != SourceSignature(Runtime.Store.AllSnapshot())) BuildSection();
        }
    }
}
