using Newtonsoft.Json.Linq;
using OBSMixer.Core;
using Xunit;

namespace OBSMixer.Tests;

public sealed class ProtocolTests
{
    [Fact] public void AuthenticationMatchesIndependentSha256Vector()
    {
        Assert.Equal("zTM5ki6L2vVvBQiTG9ckH1Lh64AbnCf6XZ226UmnkIA=", ObsProtocol.Authenticate("password", "salt", "challenge"));
        var frame = ObsProtocol.Identify(new JObject { ["rpcVersion"] = 1 }, "unused");
        Assert.Null(frame["d"]["authentication"]);
        Assert.NotEqual(0, (int)frame["d"]["eventSubscriptions"] & (1 << 16));
    }
    [Fact] public void MetersUseMagnitudeAndPostFaderPeakAndMirrorMono()
    {
        var stereo = MeterState.Parse(JArray.Parse("[[0.1,0.2,0.9],[0.3,0.4,0.8]]"));
        Assert.Equal(.1, stereo.LeftMagnitude); Assert.Equal(.2, stereo.LeftPeak);
        Assert.Equal(.3, stereo.RightMagnitude); Assert.Equal(.4, stereo.RightPeak);
        var mono = MeterState.Parse(JArray.Parse("[[0.2,0.6,1.0]]"));
        Assert.Equal(mono.LeftPeak, mono.RightPeak);
        Assert.Equal(0, MeterState.Parse(new JArray()).RightPeak);
        Assert.Equal(-20, ObsProtocol.ToDb(.1), 5); Assert.Equal(-100, ObsProtocol.ToDb(0));
    }
    [Fact] public async Task AuthenticatesCorrelatesOutOfOrderResponsesAndTimesOut()
    {
        await using var server = new FakeObs();
        server.Handler = async request => {
            string type = (string)request["requestType"];
            if (type == "Slow") await Task.Delay(150);
            if (type == "Timeout") return null;
            if (type == "Reject") throw new ObsRequestException(type, 604);
            return new JObject { ["value"] = type };
        };
        await server.StartAsync();
        using var connection = new ObsConnection { Reconnect = false, RequestTimeoutMs = 600 };
        connection.Start(server.Endpoint, server.Password);
        await Until(() => connection.State == ConnectionState.Connected);
        var slow = connection.RequestAsync("Slow"); var fast = connection.RequestAsync("Fast");
        Assert.Equal("Fast", (string)(await fast)["value"]);
        Assert.Equal("Slow", (string)(await slow)["value"]);
        Assert.Equal(1, (int)server.Identification["op"]);
        var error = await Assert.ThrowsAsync<ObsRequestException>(() => connection.RequestAsync("Reject"));
        Assert.Equal(604, error.Code);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connection.RequestAsync("Timeout"));
        var pending = connection.RequestAsync("Timeout");
        await connection.StopAsync();
        await Assert.ThrowsAnyAsync<Exception>(async () => await pending);
        Assert.Equal(ConnectionState.Disconnected, connection.State);
    }
    [Fact] public async Task BadPasswordStopsAutomaticReconnect()
    {
        await using var server = new FakeObs(); await server.StartAsync();
        using var connection = new ObsConnection { Reconnect = true };
        connection.Start(server.Endpoint, "wrong");
        await Until(() => connection.State == ConnectionState.AuthenticationFailed);
        await Task.Delay(700); Assert.Equal(1, server.Connections);
        await connection.StopAsync();
    }
    [Fact] public async Task RemoteRestartTriggersNewHandshake()
    {
        await using var server = new FakeObs(); await server.StartAsync();
        using var connection = new ObsConnection { Reconnect = true };
        int ready = 0; connection.Identified += () => Interlocked.Increment(ref ready);
        connection.Start(server.Endpoint, server.Password);
        await Until(() => Volatile.Read(ref ready) == 1);
        await server.CloseAsync();
        await Until(() => Volatile.Read(ref ready) == 2);
        Assert.Equal(2, server.Connections); await connection.StopAsync();
    }
    internal static async Task Until(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        while (!predicate()) await Task.Delay(20, timeout.Token);
    }
}
