using System;
using CP_SDK.UI;
using CP_SDK.XUI;
using OBSMixer.Core;
using UnityEngine;
using UnityEngine.UI;

namespace OBSMixer
{
    public sealed class PreviewView : ViewController<PreviewView>
    {
        internal MixerRuntime Runtime;
        private XUIText title, status, placeholder;
        private XUIPrimaryButton eye;
        private Image image;
        private Sprite openEye, closedEye, picture;
        private Texture2D texture;
        private ObsPreviewFrame displayed;
        private bool ready, decodeFailed;
        private int revision = -1;
        protected override void OnViewCreation()
        {
            openEye = CreateEye(true); closedEye = CreateEye(false);
            eye = XUIPrimaryButton.Make("", Runtime.TogglePreviewImage).SetIconSprite(openEye);
            eye.BuildUI(RTransform); MixerView.Place(eye.RTransform, 3, 2, 11, 9);
            title = XUIText.Make(""); title.BuildUI(RTransform);
            MixerView.Place(title.RTransform, 17, 2, 106, 9);
            status = XUIText.Make("").SetWrapping(false).SetOverflowMode(TMPro.TextOverflowModes.Ellipsis)
                .OnReady(x => x.TMProUGUI.richText = false);
            status.BuildUI(RTransform); MixerView.Place(status.RTransform, 4, 13, 132, 7);
            var background = CurvedGraphics.Image("Preview image background", RTransform, new Color(.01f, .015f, .02f, 1));
            MixerView.Place(background.rectTransform, 4, 23, 132, 74.25f);
            image = CurvedGraphics.Image("OBS screenshot", RTransform, Color.white);
            image.type = Image.Type.Simple; image.preserveAspect = true;
            MixerView.Place(image.rectTransform, 4, 23, 132, 74.25f);
            placeholder = XUIText.Make("").SetAlign(TMPro.TextAlignmentOptions.Center).SetWrapping(true);
            placeholder.BuildUI(RTransform); MixerView.Place(placeholder.RTransform, 8, 40, 124, 36);
            ready = true; RenderState();
        }
        internal void RenderState()
        {
            if (!ready) return;
            if (revision != Runtime.UiRevision)
            {
                revision = Runtime.UiRevision;
                title.SetText(L10n.T("APERÇU OBS · PROGRAM")).SetFontSize(Runtime.Config.TextSize);
                status.SetFontSize(Runtime.Config.TextSize * .9f); placeholder.SetFontSize(Runtime.Config.TextSize);
            }
            bool shown = Runtime.Config.PreviewImageVisible;
            eye.SetIconSprite(shown ? openEye : closedEye)
                .SetTooltip(L10n.T(shown ? "Masquer l'aperçu" : "Afficher l'aperçu"))
                .SetBackgroundColor(shown ? new Color(.1f, .5f, .85f) : new Color(.12f, .18f, .25f));
            var frame = Runtime.Preview.Frame;
            if (!ReferenceEquals(frame, displayed))
            {
                ClearPicture(); displayed = frame; decodeFailed = false;
                if (frame != null && shown)
                {
                    try
                    {
                        texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (!ImageConversion.LoadImage(texture, frame.Image, true)) throw new FormatException("Invalid OBS image");
                        texture.wrapMode = TextureWrapMode.Clamp; texture.filterMode = FilterMode.Bilinear;
                        picture = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
                        image.sprite = picture;
                    }
                    catch (Exception error)
                    { ClearPicture(); decodeFailed = true; Plugin.Log.Debug("OBS image decode failed: " + error.GetType().Name); }
                }
            }
            bool available = shown && picture;
            image.gameObject.SetActive(available); placeholder.SetActive(!available);
            bool connected = Runtime.Connection.State == ConnectionState.Connected;
            placeholder.SetText(!shown ? L10n.T("Aperçu masqué") : !connected ? L10n.T("OBS déconnecté")
                : Runtime.Preview.Failed || decodeFailed ? L10n.T("Aperçu indisponible") : L10n.T("Chargement de l'aperçu…"));
            status.SetText(!connected ? Runtime.ConnectionLabel : !shown ? L10n.T("Captures suspendues") :
                frame?.SceneName ?? L10n.T("Sortie Program OBS"));
        }
        private void ClearPicture()
        {
            if (image) image.sprite = null;
            if (picture) UnityEngine.Object.Destroy(picture);
            if (texture) UnityEngine.Object.Destroy(texture);
            picture = null; texture = null;
        }
        private static Sprite CreateEye(bool open)
        {
            const int size = 64;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x + .5f - size / 2f) / (size * .43f);
                float ny = (y + .5f - size / 2f) / (size * .43f);
                float outline = .48f * (1 - nx * nx);
                bool white = Mathf.Abs(nx) <= 1 && (Mathf.Abs(Mathf.Abs(ny) - outline) < .065f
                    || nx * nx + ny * ny < .12f);
                if (!open && Mathf.Abs(nx) < 1 && Mathf.Abs(ny + nx * .7f) < .075f) white = true;
                pixels[y * size + x] = white ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
            var icon = new Texture2D(size, size, TextureFormat.RGBA32, false);
            icon.SetPixels32(pixels); icon.Apply(false, true); icon.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(icon, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
        }
        protected override void OnViewDestruction()
        {
            ready = false; ClearPicture();
            if (openEye) { UnityEngine.Object.Destroy(openEye.texture); UnityEngine.Object.Destroy(openEye); }
            if (closedEye) { UnityEngine.Object.Destroy(closedEye.texture); UnityEngine.Object.Destroy(closedEye); }
        }
    }
}
