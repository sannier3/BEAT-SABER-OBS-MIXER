using System;
using System.IO;
using System.Reflection;
using CP_SDK.UI;
using CP_SDK.XUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OBSMixer
{
    public sealed class MixerCreditsView : ViewController<MixerCreditsView>
    {
        private XUIVLayout layout;
        private Texture2D logoTexture;
        private Sprite logo;
        private int languageRevision = -1;
        protected override void OnViewCreation() { LoadLogo(); Build(); }
        private void Update() { if (languageRevision != L10n.Revision) Build(); }
        private void LoadLogo()
        {
            try
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("OBSMixer.Resources.StreamerAssistant.png"))
                using (var bytes = new MemoryStream())
                {
                    if (stream == null) throw new FileNotFoundException("Embedded Streamer Assistant logo not found");
                    stream.CopyTo(bytes);
                    logoTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!ImageConversion.LoadImage(logoTexture, bytes.ToArray(), true)) throw new FormatException("Invalid logo image");
                    logoTexture.wrapMode = TextureWrapMode.Clamp; logoTexture.filterMode = FilterMode.Bilinear;
                    logo = Sprite.Create(logoTexture, new Rect(0, 0, logoTexture.width, logoTexture.height), new Vector2(.5f, .5f));
                }
            }
            catch (Exception error)
            {
                if (logoTexture) UnityEngine.Object.Destroy(logoTexture);
                logoTexture = null;
                Plugin.Log.Warn("Could not load Streamer Assistant logo: " + error.GetType().Name);
            }
        }
        private void Build()
        {
            if (layout != null && layout.RTransform)
            { layout.SetActive(false); UnityEngine.Object.Destroy(layout.RTransform.gameObject); }
            languageRevision = L10n.Revision;
            // The SDK's side screens are 120 x 80. Explicit placement keeps every
            // credit visible while reserving the top center for the supplied logo.
            layout = Templates.FullRectLayout().SetBackground(true, null, true)
                .OnReady(x => x.HOrVLayoutGroup.enabled = false);
            layout.BuildUI(RTransform);
            var image = XUIImage.Make(logo).SetColor(Color.white).SetType(Image.Type.Simple)
                .OnReady(x => { x.ImageC.preserveAspect = true; x.ImageC.raycastTarget = false; });
            image.BuildUI(layout.RTransform); MixerView.Place(image.RTransform, 50, 1, 20, 20);
            image.SetActive(logo);
            Text(L10n.T("À propos & crédits"), 22, 5, 3.4f);
            Text("OBS MIXER · JBSAN", 27, 4, 3);
            Heading("GITHUB", 33);
            Link("github.com/sannier3", "https://github.com/sannier3", 4, 38, 55);
            Link("BEAT-SABER-OBS-MIXER", "https://github.com/sannier3/BEAT-SABER-OBS-MIXER", 61, 38, 55);
            Heading(L10n.T("PROJET ASSOCIÉ · STREAMER ASSISTANT"), 47);
            Link("streamerassistant.jbsan.fr", "https://streamerassistant.jbsan.fr", 4, 52, 112);
            Heading(L10n.T("SDK & REMERCIEMENTS"), 61);
            Text(L10n.T("Interface réalisée avec ChatPlexSDK_BS,\nle SDK de Hardcpp. Merci pour son travail !"), 66, 11, 2.8f);
        }
        private XUIText Text(string text, float top, float height, float size)
        {
            var label = XUIText.Make(text).SetFontSize(size).SetAlign(TextAlignmentOptions.Center).SetWrapping(true)
                .OnReady(x => { x.SetFontSizes(1.8f, size); x.TMProUGUI.enableAutoSizing = true; });
            label.BuildUI(layout.RTransform); MixerView.Place(label.RTransform, 4, top, 112, height);
            return label;
        }
        private void Heading(string text, float top) => Text(text, top, 4, 2.6f).SetColor(new Color(.4f, .8f, 1f));
        private void Link(string label, string url, float left, float top, float width)
        {
            var button = XUIPrimaryButton.Make(label, () => Application.OpenURL(url)).SetFontSize(2.8f)
                .SetTooltip(L10n.T("Ouvrir dans le navigateur du PC"))
                .OnReady(x => { x.TextC.SetFontSizes(1.8f, 2.8f); x.TextC.TMProUGUI.enableAutoSizing = true; });
            button.BuildUI(layout.RTransform); MixerView.Place(button.RTransform, left, top, width, 7);
        }
        protected override void OnViewDestruction()
        {
            if (logo) UnityEngine.Object.Destroy(logo);
            if (logoTexture) UnityEngine.Object.Destroy(logoTexture);
        }
    }
}
