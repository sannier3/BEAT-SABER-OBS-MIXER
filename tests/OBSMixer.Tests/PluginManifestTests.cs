#if BSIPA_MANIFEST_TESTS
using System.Reflection;
using Hive.Versioning;
using Newtonsoft.Json.Linq;
using Xunit;
using HiveVersion = Hive.Versioning.Version;

namespace OBSMixer.Tests;

public sealed class PluginManifestTests
{
    [Fact]
    public void DependencyRangesAreAcceptedByTheInstalledBsipaParser()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("OBSMixer.Tests.PluginManifest.json");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        var manifest = JObject.Parse(reader.ReadToEnd());
        var dependencies = Assert.IsType<JObject>(manifest["dependsOn"]);

        foreach (var dependency in dependencies.Properties())
        {
            Assert.Equal(Newtonsoft.Json.Linq.JTokenType.String, dependency.Value.Type);
            var requirement = new VersionRange((string)dependency.Value);
            Assert.NotNull(requirement);
        }

        var chatPlex = new VersionRange((string)dependencies["ChatPlexSDK_BS"]);
        foreach (string supported in new[] { "6.4.2", "6.4.4", "6.4.5" })
            Assert.True(chatPlex.Matches(new HiveVersion(supported)));
        foreach (string other in new[] { "6.3.3", "6.4.1", "6.4.3", "6.4.6", "6.5.0", "7.0.0", "6.4.5-beta.1" })
            Assert.False(chatPlex.Matches(new HiveVersion(other)));

        var bsipa = new VersionRange((string)dependencies["BSIPA"]);
        Assert.True(bsipa.Matches(new HiveVersion("4.3.6")));
    }
}
#endif
