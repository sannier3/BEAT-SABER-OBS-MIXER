using Newtonsoft.Json.Linq;
using OBSMixer.Core;
using Xunit;

namespace OBSMixer.Tests;

public sealed class PreviewTests
{
    private static JObject Image(string data = "AQID") => new() { ["imageData"] = "data:image/jpeg;base64," + data };
    private static JObject Program(string name = "Live") => new() { ["currentProgramSceneName"] = name };
    private static int Captures(FakeObs server) => server.Requests.Count(r => (string)r["requestType"] == "GetSourceScreenshot");

    [Fact]
    public async Task CapturesOnlyWhenVisibleAndUsesTheConfigurableInterval()
    {
        await using var server = new FakeObs();
        server.Handler = request => Task.FromResult((string)request["requestType"] == "GetCurrentProgramScene" ? Program() : Image());
        await server.StartAsync();
        using var connection = new ObsConnection { Reconnect = false };
        using var preview = new ObsPreview(connection);
        connection.Start(server.Endpoint, server.Password);
        await ProtocolTests.Until(() => connection.State == ConnectionState.Connected);
        var start = DateTime.UtcNow;
        Assert.Equal(100, preview.IntervalMs);
        preview.IntervalMs = 2000;
        await preview.RefreshAsync(start); Assert.Empty(server.Requests);
        preview.Active = true;
        await preview.RefreshAsync(start); Assert.Equal(1, Captures(server));
        Assert.Equal(new byte[] { 1, 2, 3 }, preview.Frame.Image);
        Assert.Equal("Live", preview.Frame.SceneName);
        await preview.RefreshAsync(start.AddMilliseconds(1999)); Assert.Equal(1, Captures(server));
        await preview.RefreshAsync(start.AddMilliseconds(2000)); Assert.Equal(2, Captures(server));
        preview.IntervalMs = 100;
        await preview.RefreshAsync(start.AddMilliseconds(2100)); Assert.Equal(3, Captures(server));
        await preview.RefreshAsync(start.AddMilliseconds(2199)); Assert.Equal(3, Captures(server));
        await preview.RefreshAsync(start.AddMilliseconds(2200)); Assert.Equal(4, Captures(server));
        preview.IntervalMs = 10000;
        await preview.RefreshAsync(start.AddMilliseconds(2300)); Assert.Equal(5, Captures(server));
        await preview.RefreshAsync(start.AddMilliseconds(12299)); Assert.Equal(5, Captures(server));
        await preview.RefreshAsync(start.AddMilliseconds(12300)); Assert.Equal(6, Captures(server));
        var data = server.Requests.First(r => (string)r["requestType"] == "GetSourceScreenshot")["requestData"];
        Assert.Equal("Live", (string)data["sourceName"]); Assert.Equal("jpg", (string)data["imageFormat"]);
        Assert.Equal(640, (int)data["imageWidth"]); Assert.Equal(360, (int)data["imageHeight"]);
        preview.Active = false; Assert.Null(preview.Frame);
        await preview.RefreshAsync(start.AddSeconds(60)); Assert.Equal(6, Captures(server));
        await connection.StopAsync();
    }

    [Fact]
    public async Task SlowCaptureCannotOverlapOrReappearAfterHiding()
    {
        await using var server = new FakeObs();
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        int count = 0;
        server.Handler = async request =>
        {
            if ((string)request["requestType"] == "GetCurrentProgramScene") return Program();
            if (Interlocked.Increment(ref count) != 1) return Image();
            entered.TrySetResult(true); return await release.Task;
        };
        await server.StartAsync();
        using var connection = new ObsConnection { Reconnect = false };
        using var preview = new ObsPreview(connection) { Active = true, IntervalMs = 100 };
        connection.Start(server.Endpoint, server.Password);
        await ProtocolTests.Until(() => connection.State == ConnectionState.Connected);
        var start = DateTime.UtcNow;
        var first = preview.RefreshAsync(start);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await preview.RefreshAsync(start.AddSeconds(5)); Assert.Equal(1, Captures(server));
            preview.Active = false;
            await first.WaitAsync(TimeSpan.FromSeconds(5));
            release.TrySetResult(Image("BAUG")); Assert.Null(preview.Frame);
            await preview.RefreshAsync(start.AddSeconds(10)); Assert.Equal(1, Captures(server));
            preview.Active = true;
            await preview.RefreshAsync(start.AddSeconds(10));
            Assert.Equal(2, Captures(server)); Assert.Equal(new byte[] { 1, 2, 3 }, preview.Frame.Image);
        }
        finally { release.TrySetResult(Image()); await connection.StopAsync(); }
    }

    [Fact]
    public async Task ProgramChangesAndReconnectDiscardThePreviousImage()
    {
        await using var server = new FakeObs();
        string name = "First";
        server.Handler = request => Task.FromResult((string)request["requestType"] == "GetCurrentProgramScene" ? Program(name) : Image());
        await server.StartAsync();
        using var connection = new ObsConnection { Reconnect = true };
        using var preview = new ObsPreview(connection) { Active = true };
        connection.Start(server.Endpoint, server.Password);
        await ProtocolTests.Until(() => connection.State == ConnectionState.Connected);
        var start = DateTime.UtcNow;
        await preview.RefreshAsync(start); Assert.Equal("First", preview.Frame.SceneName);
        name = "Second";
        await server.EventAsync("CurrentProgramSceneChanged", new JObject { ["sceneName"] = name });
        await ProtocolTests.Until(() => preview.Frame == null);
        await preview.RefreshAsync(start.AddMilliseconds(1)); Assert.Equal("Second", preview.Frame.SceneName);
        Assert.Equal("Second", (string)server.Requests.Last(r => (string)r["requestType"] == "GetSourceScreenshot")["requestData"]["sourceName"]);
        name = "After reconnect";
        await server.CloseAsync();
        await ProtocolTests.Until(() => preview.Frame == null);
        await ProtocolTests.Until(() => server.Connections >= 2 && connection.State == ConnectionState.Connected);
        await preview.RefreshAsync(start.AddMilliseconds(2)); Assert.Equal(name, preview.Frame.SceneName);
        await connection.StopAsync(); Assert.Null(preview.Frame);
    }

    [Fact]
    public async Task InvalidScreenshotDoesNotBreakTheMixerConnectionAndCanRecover()
    {
        await using var server = new FakeObs();
        bool invalid = true;
        server.Handler = request => Task.FromResult((string)request["requestType"] == "GetCurrentProgramScene" ? Program() : Image(invalid ? "!invalid!" : "AQID"));
        await server.StartAsync();
        using var connection = new ObsConnection { Reconnect = false };
        using var preview = new ObsPreview(connection) { Active = true };
        connection.Start(server.Endpoint, server.Password);
        await ProtocolTests.Until(() => connection.State == ConnectionState.Connected);
        var start = DateTime.UtcNow;
        await preview.RefreshAsync(start);
        Assert.True(preview.Failed); Assert.Null(preview.Frame);
        Assert.Equal(ConnectionState.Connected, connection.State);
        invalid = false; await preview.RefreshAsync(start.AddSeconds(2));
        Assert.False(preview.Failed); Assert.NotNull(preview.Frame);
        await connection.StopAsync();
    }
}
