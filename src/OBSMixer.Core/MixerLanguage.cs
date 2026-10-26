using System;

namespace OBSMixer.Core
{
    public static class MixerLanguage
    {
        public static string Resolve(string preference, string gameLanguage)
        {
            if (preference == "fr" || preference == "en") return preference;
            return string.Equals(gameLanguage, "French", StringComparison.OrdinalIgnoreCase)
                || string.Equals(gameLanguage, "fr", StringComparison.OrdinalIgnoreCase)
                || gameLanguage != null && (gameLanguage.StartsWith("fr-", StringComparison.OrdinalIgnoreCase)
                    || gameLanguage.StartsWith("fr_", StringComparison.OrdinalIgnoreCase)) ? "fr" : "en";
        }
    }
}
