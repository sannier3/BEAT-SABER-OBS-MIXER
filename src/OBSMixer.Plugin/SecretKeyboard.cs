using System;
using System.Collections.Generic;
using CP_SDK.UI;
using CP_SDK.XUI;
using UnityEngine;

namespace OBSMixer
{
    public sealed class SecretKeyboard : IModal
    {
        private string value = "";
        private Action<string> accepted;
        private bool uppercase, symbols;
        private XUIText masked;
        private XUIVLayout layout;
        private int languageRevision;
        internal void Begin(string current, Action<string> callback) { value = current ?? ""; accepted = callback; }
        public override void OnShow() => Build();
        private void Build()
        {
            languageRevision = L10n.Revision;
            if (layout != null && layout.RTransform) { layout.SetActive(false); UnityEngine.Object.Destroy(layout.RTransform.gameObject); }
            var elements = new List<IXUIElement> { XUIText.Make(L10n.T("Mot de passe OBS")).SetFontSize(4) };
            masked = XUIText.Make(new string('●', Math.Min(60, value.Length))).SetFontSize(4);
            elements.Add(masked);
            string keys = symbols ? "`~!@#$%^&*()-_=+[{]}\\|;:'\",<.>/?" : "1234567890qwertyuiopasdfghjklzxcvbnm";
            if (uppercase && !symbols) keys = keys.ToUpperInvariant();
            for (int offset = 0; offset < keys.Length; offset += 10)
            {
                var row = new List<IXUIElement>();
                for (int i = offset; i < Math.Min(keys.Length, offset + 10); i++)
                {
                    string key = keys[i].ToString();
                    row.Add(XUIPrimaryButton.Make(key, () => Append(key)).SetWidth(7.5f).SetHeight(6).SetFontSize(3));
                }
                elements.Add(XUIHLayout.Make(row.ToArray()).SetSpacing(1));
            }
            elements.Add(XUIHLayout.Make(
                XUIPrimaryButton.Make(L10n.T("Maj"), () => { uppercase = !uppercase; Build(); }).SetWidth(18),
                XUIPrimaryButton.Make(L10n.T("Symboles"), () => { symbols = !symbols; Build(); }).SetWidth(25),
                XUIPrimaryButton.Make(L10n.T("Espace"), () => Append(" ")).SetWidth(22),
                XUIPrimaryButton.Make(L10n.T("Effacer"), Backspace).SetWidth(22)).SetSpacing(1));
            elements.Add(XUIHLayout.Make(
                XUIPrimaryButton.Make(L10n.T("Valider"), Accept).SetWidth(40),
                XUIPrimaryButton.Make(L10n.T("Annuler"), () => VController.CloseModal(this)).SetWidth(40)).SetSpacing(2));
            layout = Templates.ModalRectLayout(elements.ToArray()).SetWidth(94).SetSpacing(2);
            layout.BuildUI(RTransform);
        }
        private void Append(string text) { if (value.Length < 1024) value += text; Refresh(); }
        private void Backspace() { if (value.Length > 0) value = value.Substring(0, value.Length - 1); Refresh(); }
        private void Refresh() => masked?.SetText(new string('●', Math.Min(60, value.Length)));
        private void Accept() { var callback = accepted; string password = value; VController.CloseModal(this); callback?.Invoke(password); }
        private void Update()
        {
            if (languageRevision != L10n.Revision) Build();
            foreach (char key in Input.inputString)
            {
                if (key == '\b') Backspace();
                else if (key == '\n' || key == '\r') { Accept(); break; }
                else if (!char.IsControl(key)) Append(key.ToString());
            }
        }
        public override void OnClose() { value = ""; accepted = null; Refresh(); }
    }
}
