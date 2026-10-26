using CP_SDK.UI;
using CP_SDK.XUI;
using TMPro;
using UnityEngine;

namespace OBSMixer
{
    public sealed class MixerCreditsView : ViewController<MixerCreditsView>
    {
        private XUIVLayout layout;
        private int languageRevision = -1;
        protected override void OnViewCreation() => Build();
        private void Update() { if (languageRevision != L10n.Revision) Build(); }
        private void Build()
        {
            if (layout != null && layout.RTransform)
            { layout.SetActive(false); UnityEngine.Object.Destroy(layout.RTransform.gameObject); }
            languageRevision = L10n.Revision;
            layout = Templates.FullRectLayout(
                Templates.TitleBar(L10n.T("À propos & crédits")),
                XUIVLayout.Make(
                    XUIText.Make("OBS MIXER").SetFontSize(5).SetAlign(TextAlignmentOptions.Center),
                    XUIText.Make(L10n.T("Un projet de JBSAN")).SetFontSize(3).SetAlign(TextAlignmentOptions.Center),
                    Link(L10n.T("CODE SOURCE · GITHUB"), "BEAT-SABER-OBS-MIXER",
                        "https://github.com/sannier3/BEAT-SABER-OBS-MIXER"),
                    Link(L10n.T("SITE DE JBSAN"), "jbsan.fr", "https://jbsan.fr"),
                    Link(L10n.T("PROJET ASSOCIÉ · STREAMER ASSISTANT"), "streamerassistant.jbsan.fr",
                        "https://streamerassistant.jbsan.fr"),
                    XUIVLayout.Make(
                        Heading(L10n.T("SDK & REMERCIEMENTS")),
                        XUIText.Make(L10n.T("Interface réalisée avec ChatPlexSDK_BS,\nle SDK de Hardcpp. Merci pour son travail !"))
                            .SetFontSize(2.8f).SetWrapping(true).SetAlign(TextAlignmentOptions.Center)
                    ).SetPadding(0).SetSpacing(1)
                ).SetPadding(2).SetSpacing(2)
            ).SetBackground(true, null, true);
            layout.BuildUI(RTransform);
        }

        private static XUIText Heading(string text) => XUIText.Make(text).SetFontSize(2.6f)
            .SetColor(new Color(.4f, .8f, 1f)).SetAlign(TextAlignmentOptions.Center);

        private static XUIVLayout Link(string heading, string button, string url) =>
            XUIVLayout.Make(
                Heading(heading),
                XUIPrimaryButton.Make(button, () => Application.OpenURL(url)).SetHeight(7).SetFontSize(2.8f)
                    .SetTooltip(L10n.T("Ouvrir dans le navigateur du PC"))
            ).SetPadding(0).SetSpacing(1);
    }
}
