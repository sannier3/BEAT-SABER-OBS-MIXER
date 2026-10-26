using OBSMixer.Core;
using Xunit;

namespace OBSMixer.Tests;

public sealed class PresentationTests
{
    [Theory]
    [InlineData(0)] [InlineData(.01)] [InlineData(.2)] [InlineData(.5)] [InlineData(.8)] [InlineData(.99)] [InlineData(1)]
    public void FaderCurveRoundTrips(double position)
    {
        double db = FaderCurve.ToDb(position);
        Assert.InRange(db, -100, 0);
        Assert.Equal(position, FaderCurve.ToPosition(db), 10);
    }
    [Fact]
    public void FaderCurveHasFineControlNearZeroAndClampsEndpoints()
    {
        Assert.Equal(-100, FaderCurve.ToDb(-1)); Assert.Equal(0, FaderCurve.ToDb(2));
        Assert.Equal(0, FaderCurve.ToPosition(-200)); Assert.Equal(1, FaderCurve.ToPosition(10));
        Assert.True(FaderCurve.ToDb(.02) - FaderCurve.ToDb(.01) > FaderCurve.ToDb(.99) - FaderCurve.ToDb(.98));
        Assert.Equal(1, FaderCurve.Multiplier(0), 10); Assert.Equal(.1, FaderCurve.Multiplier(-20), 10);
        Assert.Throws<ArgumentOutOfRangeException>(() => FaderCurve.ToDb(.5, taper: 0));
    }
    [Fact]
    public void GameplayIsHiddenEvenWhenTheUserRequestsVisibility()
    {
        var state = new MixerVisibility();
        Assert.False(state.Visible);
        state.Enter(MixerScene.Menu); Assert.True(state.Visible);
        state.Enter(MixerScene.Playing); Assert.False(state.Visible);
        state.Requested = true; Assert.False(state.Visible);
        state.Pause(); Assert.True(state.Visible);
        state.Resume(); Assert.False(state.Visible);
        state.Enter(MixerScene.Menu); Assert.True(state.Visible);
    }
    [Fact]
    public void SongSelectionHasItsOwnVisibleContextAndCannotRetainPauseState()
    {
        var state = new MixerVisibility();
        state.Enter(MixerScene.Menu); Assert.True(state.Visible);
        state.Enter(MixerScene.SongSelection); Assert.True(state.Visible);
        Assert.Equal(MixerScene.SongSelection, state.Scene);
        state.Pause(); Assert.False(state.Paused);
        state.Enter(MixerScene.Playing); Assert.False(state.Visible);
        state.Pause(); Assert.True(state.Visible);
        state.Enter(MixerScene.SongSelection); Assert.True(state.Visible); Assert.False(state.Paused);
        state.Enabled = false; Assert.False(state.ContextVisible);
        state.Enabled = true; state.Requested = false;
        Assert.True(state.ContextVisible); Assert.False(state.Visible);
        state.Enter(MixerScene.Menu); Assert.Equal(MixerScene.Menu, state.Scene);
    }
    [Fact]
    public void SceneTransitionsAndEnableDisableCannotLeakPauseState()
    {
        var state = new MixerVisibility();
        state.Pause(); Assert.False(state.Paused);
        state.Enter(MixerScene.Playing); state.Pause();
        state.Enabled = false; Assert.False(state.Visible);
        state.Enabled = true; Assert.True(state.Visible);
        state.Requested = false; Assert.False(state.Visible); Assert.True(state.ContextVisible);
        state.Enter(MixerScene.Menu); state.Pause(); Assert.False(state.Paused);
        state.Requested = true; Assert.True(state.Visible);
        state.Enter(MixerScene.Playing); Assert.False(state.Visible);
        state.Enter(MixerScene.None); Assert.False(state.Visible);
    }
    [Fact]
    public void PagesRespectTheRequestedCapacityAndShrinkWhenSourcesDisappear()
    {
        Assert.Equal(8, MixerLayout.Capacity(8)); Assert.Equal(2, MixerLayout.Capacity(2));
        Assert.Equal(1, MixerLayout.Capacity(0)); Assert.Equal(8, MixerLayout.Capacity(20));
        Assert.Equal(2, MixerLayout.PageCount(6, 5)); Assert.Equal(1, MixerLayout.PageCount(0, 5));
        Assert.Equal(0, MixerLayout.ClampPage(3, 2, 5)); Assert.Equal(1, MixerLayout.ClampPage(1, 6, 5));
    }

    [Fact]
    public void NarrowWindowsCompressColumnsWithoutAddingPages()
    {
        foreach (float width in new[] { 100f, 160f, 184f, 185f, 260f })
        {
            int capacity = MixerLayout.Capacity(8);
            Assert.Equal(1, MixerLayout.PageCount(6, capacity));
            Assert.Equal(1, MixerLayout.PageCount(8, capacity));
            foreach (int sources in new[] { 6, 8 })
            {
                float column = MixerLayout.ChannelWidth(width, 8, sources);
                Assert.True(column > 0);
                Assert.InRange(Math.Abs((column + 2) * sources - (width - 8)), 0, .0001f);
            }
        }
        Assert.Equal(2, MixerLayout.PageCount(9, MixerLayout.Capacity(8)));
    }
}
