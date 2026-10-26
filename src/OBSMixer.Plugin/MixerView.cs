using System.Collections.Generic;
using System.Linq;
using CP_SDK.UI;
using CP_SDK.XUI;
using OBSMixer.Core;
using UnityEngine;

namespace OBSMixer
{
    public sealed class MixerView : ViewController<MixerView>
    {
        internal MixerRuntime Runtime;
        private XUIText status, pageLabel, empty;
        private XUIPrimaryButton previous, next;
        private readonly List<ChannelView> channels = new List<ChannelView>();
        private int page, revision = -1;
        private string signature;
        private bool ready;
        protected override void OnViewCreation()
        {
            status = XUIText.Make("").OnReady(x => x.TMProUGUI.richText = false);
            status.BuildUI(RTransform);
            empty = XUIText.Make(L10n.T("Aucune source audio")); empty.BuildUI(RTransform);
            previous = XUIPrimaryButton.Make("<", () => { page--; signature = null; RenderState(); });
            next = XUIPrimaryButton.Make(">", () => { page++; signature = null; RenderState(); });
            previous.BuildUI(RTransform); next.BuildUI(RTransform);
            pageLabel = XUIText.Make(""); pageLabel.BuildUI(RTransform);
            ready = true;
        }
        internal static void Place(RectTransform rect, float left, float top, float width, float height)
        {
            var fitter = rect.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            if (fitter) fitter.enabled = false;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(left, -top); rect.sizeDelta = new Vector2(width, height);
        }
        internal void RenderState()
        {
            if (!ready) return;
            var config = Runtime.Config;
            var inputs = Runtime.VisibleInputs();
            int capacity = MixerLayout.Capacity(config.ChannelsPerPage);
            page = MixerLayout.ClampPage(page, inputs.Length, capacity);
            string current = page + "|" + string.Join("|", inputs.Select(x => x.Key + ":" + x.Name));
            if (signature != current || revision != Runtime.UiRevision)
            {
                foreach (var channel in channels) channel.Dispose(); channels.Clear();
                signature = current; revision = Runtime.UiRevision;
                float width = MixerLayout.ChannelWidth(config.PanelWidth, config.ChannelsPerPage, inputs.Length);
                var selected = inputs.Skip(page * capacity).Take(capacity).ToArray();
                for (int i = 0; i < selected.Length; i++)
                {
                    var channel = new ChannelView(Runtime, Runtime.Channel(selected[i].Key), RTransform, width, config.PanelHeight - 30);
                    Place(channel.Root, 4 + i * (width + 2), 15, width, config.PanelHeight - 30); channels.Add(channel);
                }
                status.SetFontSize(config.TextSize * .9f); empty.SetFontSize(config.TextSize);
                pageLabel.SetFontSize(config.TextSize * .85f);
                previous.SetFontSize(config.TextSize); next.SetFontSize(config.TextSize);
                Place(status.RTransform, 4, 2, config.PanelWidth - 18, 9);
                Place(empty.RTransform, 6, 30, config.PanelWidth - 12, 15);
                Place(previous.RTransform, config.PanelWidth - 34, config.PanelHeight - 12, 14, 9);
                Place(next.RTransform, config.PanelWidth - 18, config.PanelHeight - 12, 14, 9);
                Place(pageLabel.RTransform, 4, config.PanelHeight - 11, config.PanelWidth - 40, 8);
            }
            status.SetText("OBS MIXER · " + Runtime.ConnectionLabel + (Runtime.InPause ? " · PAUSE" : ""));
            empty.SetActive(inputs.Length == 0);
            empty.SetText(Runtime.LastError.Length > 0 ? Runtime.LastError : L10n.T("Aucune source audio sélectionnée"));
            pageLabel.SetText((page + 1) + " / " + MixerLayout.PageCount(inputs.Length, capacity) + " · " + inputs.Length + L10n.T(" sources") + (Runtime.LastError.Length > 0 ? L10n.T(" · ERREUR OBS") : ""));
            previous.SetInteractable(page > 0); next.SetInteractable(page + 1 < MixerLayout.PageCount(inputs.Length, capacity));
            foreach (var channel in channels) channel.Render();
        }
        internal void RenderMeters(float elapsed) { if (ready) foreach (var channel in channels) channel.RenderMeter(elapsed); }
        protected override void OnViewDestruction()
        {
            ready = false;
            foreach (var channel in channels) channel.Dispose(); channels.Clear();
        }
    }
}
