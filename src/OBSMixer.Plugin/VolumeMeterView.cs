using CP_SDK.XUI;
using OBSMixer.Core;
using UnityEngine;
using UnityEngine.UI;

namespace OBSMixer
{
    internal sealed class VolumeMeterView
    {
        private const float FloorDb = -60, WarningDb = -18, ClipDb = -6;
        private readonly Image fill, peak;
        private readonly XUIText label;
        private readonly float barHeight;
        private float displayedDb = FloorDb, displayedPeak = FloorDb;
        public RectTransform Root { get; }
        public VolumeMeterView(Transform parent, string channel, float width, float height, float textSize)
        {
            Root = CurvedGraphics.Container("Meter " + channel, parent);
            Root.sizeDelta = new Vector2(width, height);
            barHeight = Mathf.Max(1, height - 6);
            var background = CurvedGraphics.Image("Meter background", Root, new Color(.08f, .12f, .15f, 1));
            MixerView.Place(background.rectTransform, 0, 6, width, barHeight);
            fill = CurvedGraphics.Image("Level", background.rectTransform, Color.green);
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            peak = CurvedGraphics.Image("Peak", background.rectTransform, Color.white);
            peak.rectTransform.anchorMin = new Vector2(0, 0); peak.rectTransform.anchorMax = new Vector2(1, 0);
            peak.rectTransform.sizeDelta = new Vector2(0, .6f);
            label = XUIText.Make(channel).SetFontSize(Mathf.Min(2.8f, Mathf.Min(textSize, width * 1.6f)))
                .SetAlign(TMPro.TextAlignmentOptions.Center);
            label.BuildUI(Root); MixerView.Place(label.RTransform, 0, 0, width, 4);
        }
        public void SetChannel(string name) => label.SetText(name);
        public void Render(double magnitude, double peakValue, float elapsed)
        {
            float db = Mathf.Clamp((float)ObsProtocol.ToDb(magnitude), FloorDb, 0);
            float peakDb = Mathf.Clamp((float)ObsProtocol.ToDb(peakValue), FloorDb, 0);
            float smoothing = 1 - Mathf.Exp(-Mathf.Max(.001f, elapsed) / .08f);
            displayedDb = Mathf.Lerp(displayedDb, db, smoothing);
            displayedPeak = Mathf.Lerp(displayedPeak, peakDb, smoothing);
            fill.rectTransform.anchorMax = new Vector2(1, (displayedDb - FloorDb) / -FloorDb);
            fill.color = db >= ClipDb ? Color.red : db >= WarningDb ? new Color(1, .55f, .05f) : new Color(.15f, .9f, .3f);
            peak.rectTransform.anchoredPosition = new Vector2(0, (displayedPeak - FloorDb) / -FloorDb * barHeight);
            peak.enabled = peakValue > 0;
        }
    }
}
