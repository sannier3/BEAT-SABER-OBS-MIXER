using System;
using BGLib.Polyglot;
using OBSMixer.Core;

namespace OBSMixer
{
    internal static class L10n
    {
        internal static string Language { get; private set; } = "en";
        internal static int Revision { get; private set; }
        internal static event Action Changed;
        internal static string T(string french) => MixerText.Translate(french, Language);
        internal static bool Refresh(string preference)
        {
            string gameLanguage = null;
            if (preference != "fr" && preference != "en")
            {
                try { gameLanguage = Localization.Instance?.SelectedLanguage.ToString(); }
                catch (Exception) { /* The game localization model may not be ready yet. Retry on the next poll. */ }
            }
            string language = MixerLanguage.Resolve(preference, gameLanguage);
            if (language == Language) return false;
            Language = language; Revision++; Changed?.Invoke();
            return true;
        }
    }
}
