using OBSMixer.Core;
using Xunit;

namespace OBSMixer.Tests;

public sealed class SourceSelectionTests
{
    private static InputState Input(string key) => new() { Key = key, Name = key.ToUpperInvariant() };

    [Fact]
    public void LimitRespectsPinsHiddenInputsAndUserOrder()
    {
        var inputs = new[] { Input("a"), Input("b"), Input("c"), Input("d") };
        var hidden = new[] { "c" };
        var pinned = new[] { "c", "b" };
        var order = new[] { "d", "a", "b", "c" };
        var selected = SourceSelection.Select(inputs, hidden, pinned, order, 2);
        Assert.Equal(new[] { "b", "d" }, selected.Select(x => x.Key));
        Assert.Equal(4, inputs.Length);
        Assert.Equal(new[] { "b", "d", "a" }, SourceSelection.Select(inputs, hidden, pinned, order, 0).Select(x => x.Key));
    }

    [Fact]
    public void AllHasNoArtificialSourceCountCeiling()
    {
        var inputs = Enumerable.Range(0, 80).Select(i => Input(i.ToString())).ToArray();
        Assert.Equal(80, SourceSelection.Select(inputs, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), 0).Length);
        Assert.Single(SourceSelection.Select(inputs, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), 1));
    }

    [Fact]
    public void MissingSavedSourcesDoNotUseUpTheLimit()
    {
        var selected = SourceSelection.Select(new[] { Input("a"), Input("b") }, Array.Empty<string>(),
            new[] { "deleted", "b" }, new[] { "deleted", "a" }, 5);
        Assert.Equal(new[] { "b", "a" }, selected.Select(x => x.Key));
    }
}
