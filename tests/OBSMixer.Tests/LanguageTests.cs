using OBSMixer.Core;
using Xunit;

namespace OBSMixer.Tests;

public sealed class LanguageTests
{
    [Theory]
    [InlineData("auto", "French", "fr")]
    [InlineData("auto", "fr-FR", "fr")]
    [InlineData("auto", "fr_CA", "fr")]
    [InlineData("auto", "English", "en")]
    [InlineData("auto", "German", "en")]
    [InlineData("auto", null, "en")]
    [InlineData("fr", "English", "fr")]
    [InlineData("en", "French", "en")]
    [InlineData(null, "French", "fr")]
    [InlineData("unknown", "Japanese", "en")]
    public void AutomaticLanguageUsesFrenchOnlyForFrenchAndManualChoiceWins(string preference, string game, string expected)
        => Assert.Equal(expected, MixerLanguage.Resolve(preference, game));

    [Fact]
    public void UserInterfaceTranslationsSwitchInBothDirections()
    {
        foreach (var pair in new[] {
            ("Général", "General"), ("Détails", "Details"), ("Fermer", "Close"),
            ("SCÈNES OBS", "OBS SCENES"), ("À propos & crédits", "About & credits"),
            ("Mot de passe OBS", "OBS password"), ("Sources par page", "Sources per page"),
            ("ÉCOUTE SEULE", "MONITOR ONLY"), ("DÉCONNECTÉ", "DISCONNECTED")
        })
        {
            Assert.Equal(pair.Item2, MixerText.Translate(pair.Item1, MixerLanguage.Resolve("auto", "English")));
            Assert.Equal(pair.Item1, MixerText.Translate(pair.Item1, MixerLanguage.Resolve("fr", "English")));
            Assert.Equal(pair.Item2, MixerText.Translate(pair.Item1, MixerLanguage.Resolve("en", "French")));
        }
        Assert.Equal("Unrecognized text", MixerText.Translate("Unrecognized text", "en"));
    }
}
