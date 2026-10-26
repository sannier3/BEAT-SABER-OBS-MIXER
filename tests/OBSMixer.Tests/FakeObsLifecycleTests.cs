using OBSMixer.Core;
using Xunit;

namespace OBSMixer.Tests;

public sealed class FakeObsLifecycleTests
{
    [Fact]
    public async Task DisposalIsIdempotentAfterTheRequestContextHasEnded()
    {
        await using var server = new FakeObs();
        await server.StartAsync();
        using var connection = new ObsConnection { Reconnect = false };
        connection.Start(server.Endpoint, server.Password);
        await ProtocolTests.Until(() => connection.State == ConnectionState.Connected);

        await connection.StopAsync();
        await ProtocolTests.Until(() => !server.HasActiveClient);
        await server.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        await server.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DisposalCancelsALiveConnectionAndPendingReply()
    {
        await using var server = new FakeObs();
        var replyEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseReply = new TaskCompletionSource<Newtonsoft.Json.Linq.JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.Handler = request =>
        {
            replyEntered.TrySetResult(true);
            return releaseReply.Task;
        };
        await server.StartAsync();
        using var connection = new ObsConnection { Reconnect = false };
        connection.Start(server.Endpoint, server.Password);
        await ProtocolTests.Until(() => connection.State == ConnectionState.Connected);
        var pending = connection.RequestAsync("PendingReply");

        try
        {
            await replyEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await server.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(server.HasActiveClient);
            await Assert.ThrowsAsync<IOException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
            await ProtocolTests.Until(() => connection.State == ConnectionState.Disconnected);
        }
        finally
        {
            releaseReply.TrySetResult(new Newtonsoft.Json.Linq.JObject());
            await connection.StopAsync();
        }
    }
}
