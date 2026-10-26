using System.Collections.Generic;
using System.Linq;
using CP_SDK.UI;
using CP_SDK.XUI;
using OBSMixer.Core;
using UnityEngine;

namespace OBSMixer
{
    public sealed class ScenesView : ViewController<ScenesView>
    {
        internal MixerRuntime Runtime;
        private XUIText title, status, empty, pager;
        private XUIPrimaryButton previous, next;
        private readonly List<(string Key, XUIPrimaryButton Button)> buttons = new List<(string, XUIPrimaryButton)>();
        private string signature;
        private int page, revision = -1;
        private bool ready;
        protected override void OnViewCreation()
        {
            title = XUIText.Make(L10n.T("SCÈNES OBS")); title.BuildUI(RTransform);
            status = XUIText.Make("").SetWrapping(false).SetOverflowMode(TMPro.TextOverflowModes.Ellipsis)
                .OnReady(x => x.TMProUGUI.richText = false);
            status.BuildUI(RTransform);
            empty = XUIText.Make(""); empty.BuildUI(RTransform);
            previous = XUIPrimaryButton.Make("<", () => { page--; signature = null; RenderState(); });
            next = XUIPrimaryButton.Make(">", () => { page++; signature = null; RenderState(); });
            previous.BuildUI(RTransform); next.BuildUI(RTransform);
            pager = XUIText.Make(""); pager.BuildUI(RTransform);
            MixerView.Place(title.RTransform, 4, 2, 90, 7);
            MixerView.Place(status.RTransform, 4, 11, 102, 8);
            MixerView.Place(empty.RTransform, 4, 35, 102, 20);
            MixerView.Place(previous.RTransform, 4, 82, 16, 10);
            MixerView.Place(next.RTransform, 90, 82, 16, 10);
            MixerView.Place(pager.RTransform, 25, 82, 60, 10);
            ready = true;
        }
        internal void RenderState()
        {
            if (!ready) return;
            title.SetText(L10n.T("SCÈNES OBS"));
            const int capacity = 8;
            var scenes = Runtime.Scenes.Snapshot();
            page = MixerLayout.ClampPage(page, scenes.Length, capacity);
            string current = page + "|" + string.Join("|", scenes.Select(s => s.Key + ":" + s.Name));
            if (current != signature || revision != Runtime.UiRevision)
            {
                foreach (var button in buttons) { button.Button.SetActive(false); UnityEngine.Object.Destroy(button.Button.RTransform.gameObject); }
                buttons.Clear(); signature = current; revision = Runtime.UiRevision;
                var selected = scenes.Skip(page * capacity).Take(capacity).ToArray();
                for (int i = 0; i < selected.Length; i++)
                {
                    string key = selected[i].Key;
                    var button = XUIPrimaryButton.Make(selected[i].Name, () => Runtime.Run(() => Runtime.SelectSceneAsync(key)))
                        .SetFontSize(Runtime.Config.TextSize).OnReady(x => {
                            x.TextC.SetFontSizes(1.8f, Runtime.Config.TextSize);
                            x.TextC.TMProUGUI.enableAutoSizing = true;
                            x.TextC.TMProUGUI.richText = false;
                            x.TextC.SetWrapping(false);
                        });
                    button.BuildUI(RTransform);
                    MixerView.Place(button.RTransform, 4 + i % 2 * 52, 23 + i / 2 * 14, 50, 11);
                    buttons.Add((key, button));
                }
                title.SetFontSize(Runtime.Config.TextSize * 1.1f);
                status.SetFontSize(Runtime.Config.TextSize * .9f);
                empty.SetFontSize(Runtime.Config.TextSize);
                pager.SetFontSize(Runtime.Config.TextSize);
                previous.SetFontSize(Runtime.Config.TextSize); next.SetFontSize(Runtime.Config.TextSize);
            }
            bool connected = Runtime.Connection.State == ConnectionState.Connected;
            var active = scenes.FirstOrDefault(s => s.Active);
            status.SetText(Runtime.Scenes.IsSwitching ? L10n.T("Changement de scène…") :
                !connected ? Runtime.ConnectionLabel : L10n.T("ACTIVE : ") + (active?.Name ?? L10n.T("aucune")));
            empty.SetActive(scenes.Length == 0);
            empty.SetText(connected ? L10n.T("Chargement des scènes OBS…") : L10n.T("OBS déconnecté"));
            foreach (var entry in buttons)
            {
                var scene = scenes.FirstOrDefault(s => s.Key == entry.Key);
                if (scene == null) continue;
                entry.Button.SetText((scene.Active ? "● " : "") + scene.Name)
                    .SetBackgroundColor(scene.Active ? new Color(.1f, .5f, .85f) : new Color(.12f, .18f, .25f))
                    .SetInteractable(connected && !Runtime.Scenes.IsSwitching);
            }
            previous.SetInteractable(page > 0);
            next.SetInteractable(page + 1 < MixerLayout.PageCount(scenes.Length, capacity));
            pager.SetText((page + 1) + " / " + MixerLayout.PageCount(scenes.Length, capacity));
        }
    }
}
