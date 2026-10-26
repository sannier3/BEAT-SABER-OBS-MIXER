using System;
using System.Globalization;
using CP_SDK.XUI;
using OBSMixer.Core;
using UnityEngine;

namespace OBSMixer
{
    internal sealed class ChannelView : IDisposable
    {
        private readonly MixerRuntime runtime;
        private readonly ChannelController channel;
        private readonly XUIText volumeLabel;
        private readonly XUIPrimaryButton mute, details;
        private readonly VerticalFader fader;
        private readonly VolumeMeterView left, right;
        private readonly bool compact;
        public RectTransform Root { get; }
        internal ChannelView(MixerRuntime runtime, ChannelController channel, Transform parent, float width, float height)
        {
            this.runtime = runtime; this.channel = channel; compact = width < 18;
            Root = CurvedGraphics.Container("Channel " + channel.State.Name, parent);
            Root.sizeDelta = new Vector2(width, height);
            var bg = CurvedGraphics.Image("Channel background", Root, new Color(.04f, .07f, .11f, runtime.Config.BackgroundOpacity));
            MixerView.Place(bg.rectTransform, 0, 0, width, height);
            var name = XUIText.Make(channel.State.Name).SetFontSize(runtime.Config.TextSize).SetWrapping(true)
                .SetOverflowMode(TMPro.TextOverflowModes.Ellipsis)
                .OnReady(x => { FitText(x); x.TMProUGUI.richText = false; x.TMProUGUI.maxVisibleLines = 2; });
            name.BuildUI(Root); MixerView.Place(name.RTransform, 2, 1, width - 4, 12);
            float meterHeight = Mathf.Max(10, height - 46);
            float meterWidth = Mathf.Clamp(width * .1f, 1, 2);
            float gap = Mathf.Clamp(width * .04f, .45f, .8f);
            float touchWidth = Mathf.Clamp(width * .4f, 4.5f, 10);
            float start = (width - 2 * meterWidth - 2 * gap - touchWidth) / 2;
            left = new VolumeMeterView(Root, "L", meterWidth, meterHeight, runtime.Config.TextSize);
            right = new VolumeMeterView(Root, "R", meterWidth, meterHeight, runtime.Config.TextSize);
            MixerView.Place(left.Root, start, 14, meterWidth, meterHeight);
            MixerView.Place(right.Root, start + meterWidth + gap, 14, meterWidth, meterHeight);
            var faderRoot = CurvedGraphics.Container("Volume fader", Root);
            MixerView.Place(faderRoot, start + 2 * (meterWidth + gap), 14, touchWidth, meterHeight);
            fader = faderRoot.gameObject.AddComponent<VerticalFader>();
            fader.Initialize(channel.BeginDrag, channel.ChangeVolume, channel.EndDrag);
            volumeLabel = XUIText.Make("").SetFontSize(runtime.Config.TextSize).SetWrapping(false)
                .SetAlign(TMPro.TextAlignmentOptions.Center).OnReady(FitText);
            volumeLabel.BuildUI(Root); MixerView.Place(volumeLabel.RTransform, 1, height - 28, width - 2, 6);
            mute = XUIPrimaryButton.Make("", channel.ToggleMute).SetFontSize(runtime.Config.TextSize).OnReady(x => { FitText(x.TextC); x.TextC.SetMargins(.5f, 0, .5f, 0); });
            mute.BuildUI(Root); MixerView.Place(mute.RTransform, 1, height - 20, width - 2, 8);
            details = XUIPrimaryButton.Make(width < 16 ? "…" : L10n.T("Détails"), () => runtime.OpenDetails(channel.State.Key))
                .SetFontSize(runtime.Config.TextSize).SetTooltip(L10n.T("Détails de la source")).OnReady(x => { FitText(x.TextC); x.TextC.SetMargins(.5f, 0, .5f, 0); });
            details.BuildUI(Root); MixerView.Place(details.RTransform, 1, height - 10, width - 2, 8);
            Render();
        }
        private void FitText(CP_SDK.UI.Components.CText text)
        {
            text.LElement.minWidth = 0;
            text.LElement.preferredWidth = 0;
            text.LElement.flexibleWidth = 1;
            text.SetFontSizes(1.2f, runtime.Config.TextSize);
            text.TMProUGUI.enableAutoSizing = true;
        }
        internal void Render()
        {
            var state = channel.State;
            bool connected = runtime.Connection.State == ConnectionState.Connected;
            fader.SetInteractable(connected);
            fader.SetValue((float)FaderCurve.ToPosition(state.VolumeDb, runtime.Config.VolumeMinDb, runtime.Config.VolumeMaxDb, runtime.Config.FaderTaper));
            volumeLabel.SetText(state.VolumeDb <= -100 ? "-∞ dB" : state.VolumeDb.ToString("F1", CultureInfo.InvariantCulture) + " dB");
            mute.SetText(state.Muted ? L10n.T("MUET") : compact ? L10n.T("SON") : L10n.T("SON ON")).SetBackgroundColor(state.Muted ? new Color(.65f, .15f, .12f) : new Color(.12f, .3f, .4f));
            mute.SetInteractable(connected); details.SetInteractable(connected);
        }
        internal void RenderMeter(float elapsed)
        {
            var meter = runtime.Store.Meter(channel.State.Key);
            bool fresh = meter != null && (DateTime.UtcNow - meter.ReceivedUtc).TotalMilliseconds < 350;
            bool mono = meter != null && meter.Channels == 1;
            left.SetChannel(mono ? "M" : "L");
            right.Root.gameObject.SetActive(!mono);
            left.Render(fresh ? meter.LeftMagnitude : 0, fresh ? meter.LeftPeak : 0, elapsed);
            if (!mono) right.Render(fresh ? meter.RightMagnitude : 0, fresh ? meter.RightPeak : 0, elapsed);
        }
        public void Dispose() { channel.EndDrag(); if (Root) UnityEngine.Object.Destroy(Root.gameObject); }
    }
}
