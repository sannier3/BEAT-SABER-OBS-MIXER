using System.Collections.Generic;
using CP_SDK.UI;
using CP_SDK.XUI;
using OBSMixer.Core;
using UnityEngine;

namespace OBSMixer
{
    public sealed class ChannelDetailsView : ViewController<ChannelDetailsView>
    {
        internal MixerRuntime Runtime;
        internal string SourceKey;
        private XUIText title, offset, label, trackTitle;
        private XUIPrimaryButton close;
        private readonly XUIPrimaryButton[] tracks = new XUIPrimaryButton[6], monitors = new XUIPrimaryButton[3];
        private readonly string[] modes = { ObsProtocol.MonitorOff, ObsProtocol.MonitorOnly, ObsProtocol.MonitorOutput };
        private readonly string[] labels = { "OFF", "ÉCOUTE SEULE", "ÉCOUTE + SORTIE" };
        private bool ready;
        private readonly List<XUIPrimaryButton> controls = new List<XUIPrimaryButton>();
        private CP_SDK.UI.Components.CText[] texts;
        protected override void OnViewCreation()
        {
            title = XUIText.Make("").SetFontSize(3.5f).SetWrapping(false).SetOverflowMode(TMPro.TextOverflowModes.Ellipsis).OnReady(x => x.TMProUGUI.richText = false);
            title.BuildUI(RTransform); MixerView.Place(title.RTransform, 3, 2, 102, 9);
            label = XUIText.Make(L10n.T("Décalage audio OBS (ms)")).SetFontSize(3);
            label.BuildUI(RTransform); MixerView.Place(label.RTransform, 3, 13, 102, 6);
            offset = XUIText.Make("").SetFontSize(3); offset.BuildUI(RTransform);
            MixerView.Place(offset.RTransform, 43, 21, 24, 8);
            int[] steps = { -100, -10, 10, 100 };
            float[] positions = { 3, 23, 68, 88 };
            for (int i = 0; i < steps.Length; i++)
            {
                int step = steps[i];
                var button = XUIPrimaryButton.Make(step > 0 ? "+" + step : step.ToString(), () => Runtime.Channel(SourceKey)?.Offset(step)).SetFontSize(2.8f);
                controls.Add(button);
                button.BuildUI(RTransform); MixerView.Place(button.RTransform, positions[i], 21, 18, 8);
            }
            trackTitle = XUIText.Make(L10n.T("Pistes audio")).SetFontSize(3); trackTitle.BuildUI(RTransform);
            MixerView.Place(trackTitle.RTransform, 3, 33, 102, 6);
            for (int i = 0; i < 6; i++)
            {
                int number = i + 1;
                tracks[i] = XUIPrimaryButton.Make(number.ToString(), () => Runtime.Channel(SourceKey)?.Track(number)).SetFontSize(3);
                controls.Add(tracks[i]);
                tracks[i].BuildUI(RTransform); MixerView.Place(tracks[i].RTransform, 3 + i * 17, 41, 15, 8);
            }
            for (int i = 0; i < 3; i++)
            {
                int selected = i;
                monitors[i] = XUIPrimaryButton.Make(L10n.T(labels[i]), () => Runtime.Channel(SourceKey)?.Monitor(modes[selected])).SetFontSize(2.6f);
                controls.Add(monitors[i]);
                monitors[i].BuildUI(RTransform); MixerView.Place(monitors[i].RTransform, 3 + i * 35, 55, 33, 10);
            }
            close = XUIPrimaryButton.Make(L10n.T("Fermer"), Runtime.CloseDetails).SetFontSize(3);
            close.BuildUI(RTransform); MixerView.Place(close.RTransform, 24, 72, 62, 9);
            texts = GetComponentsInChildren<CP_SDK.UI.Components.CText>(true);
            ready = true; ApplyAppearance();
        }
        internal void ApplyAppearance()
        {
            if (!ready) return;
            foreach (var text in texts) text.SetFontSize(Runtime.Config.TextSize);
            label.SetText(L10n.T("Décalage audio OBS (ms)"));
            trackTitle.SetText(L10n.T("Pistes audio"));
            close.SetText(L10n.T("Fermer"));
        }
        internal void RenderState()
        {
            if (!ready) return;
            var channel = Runtime.Channel(SourceKey);
            bool connected = Runtime.Connection.State == ConnectionState.Connected && channel != null;
            foreach (var control in controls) control.SetInteractable(connected);
            if (channel == null)
            {
                title.SetText(L10n.T("Source indisponible")); offset.SetText("-- ms");
                for (int i = 0; i < 6; i++) tracks[i].SetText((i + 1).ToString());
                for (int i = 0; i < 3; i++) monitors[i].SetText(L10n.T(labels[i]));
                return;
            }
            var state = channel.State;
            title.SetText(state.Name); offset.SetText(state.OffsetMs + " ms");
            for (int i = 0; i < 6; i++) tracks[i].SetText(state.Track(i + 1) ? "[" + (i + 1) + "]" : (i + 1).ToString());
            for (int i = 0; i < 3; i++) monitors[i].SetText((state.MonitorType == modes[i] ? "> " : "") + L10n.T(labels[i]));
        }
    }
}
