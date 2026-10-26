using Newtonsoft.Json.Linq;
using OBSMixer.Core;
using Xunit;

namespace OBSMixer.Tests;

public sealed class SceneStoreTests
{
    private static JObject List(string active, string aName = "A", bool extra = false)
    {
        var scenes = new JArray(new JObject { ["sceneName"] = aName, ["sceneUuid"] = "a" },
            new JObject { ["sceneName"] = "B", ["sceneUuid"] = "b" });
        if (extra) scenes.Add(new JObject { ["sceneName"] = "C", ["sceneUuid"] = "c" });
        return new JObject { ["scenes"] = scenes, ["currentProgramSceneUuid"] = active,
            ["currentProgramSceneName"] = active == "a" ? aName : "B" };
    }
    private static JObject Program(string key, string name) => new() { ["sceneUuid"] = key, ["sceneName"] = name };

    [Fact]
    public async Task SceneIndicesRestoreObsDockOrderAfterSnapshotAndReorderEvent()
    {
        await using var server = new FakeObs();
        // obs-websocket sends the bottom scene first (index 0).
        var items = new JArray(Enumerable.Range(0, 10).Select(i => new JObject {
            ["sceneName"] = "Scene " + i, ["sceneUuid"] = "s" + i, ["sceneIndex"] = i
        }));
        server.Handler = request => Task.FromResult(new JObject { ["scenes"] = items.DeepClone(),
            ["currentProgramSceneUuid"] = "s9", ["currentProgramSceneName"] = "Scene 9" });
        await server.StartAsync();
        using var connection = new ObsConnection { Reconnect = false };
        using var store = new ObsSceneStore(connection) { Enabled = true };
        connection.Start(server.Endpoint, server.Password);
        await ProtocolTests.Until(() => store.Snapshot().Length == 10);
        Assert.Equal(Enumerable.Range(0, 10).Reverse().Select(i => "s" + i), store.Snapshot().Select(s => s.Key));
        // A reorder must also update the order across the eight-button page boundary.
        items[0]["sceneIndex"] = 9;
        items[9]["sceneIndex"] = 0;
        await server.EventAsync("SceneListChanged", new JObject { ["scenes"] = items.DeepClone() });
        await ProtocolTests.Until(() => store.Snapshot().First().Key == "s0");
        Assert.Equal(new[] { "s0", "s8", "s7", "s6", "s5", "s4", "s3", "s2", "s1", "s9" },
            store.Snapshot().Select(s => s.Key));
        Assert.True(store.Snapshot().Last().Active);
        await connection.StopAsync();
    }

    [Fact]
    public async Task OptionalSelectorTracksExternalChangesAndConfirmsCommands()
    {
        await using var server = new FakeObs();
        string active = "a", aName = "A"; bool extra = false;
        server.Handler = async request =>
        {
            switch ((string)request["requestType"])
            {
                case "GetSceneList": return List(active, aName, extra);
                case "GetCurrentProgramScene": return new JObject { ["currentProgramSceneUuid"] = active,
                    ["currentProgramSceneName"] = active == "a" ? aName : "B" };
                case "SetCurrentProgramScene":
                    active = (string)request["requestData"]["sceneName"] == aName ? "a" : "b";
                    await server.EventAsync("CurrentProgramSceneChanged", Program(active, active == "a" ? aName : "B"));
                    return new JObject();
                default: throw new InvalidOperationException("Unexpected scene request");
            }
        };
        await server.StartAsync();
        using var connection = new ObsConnection { Reconnect = false };
        using var store = new ObsSceneStore(connection);
        connection.Start(server.Endpoint, server.Password);
        await ProtocolTests.Until(() => connection.State == ConnectionState.Connected);
        await Task.Delay(200);
        Assert.Empty(server.Requests); Assert.Empty(store.Snapshot());
        store.Enabled = true;
        await ProtocolTests.Until(() => store.Snapshot().Length == 2);
        Assert.True(store.Snapshot().Single(s => s.Key == "a").Active);

        active = "b";
        await server.EventAsync("CurrentProgramSceneChanged", Program("b", "B"));
        await ProtocolTests.Until(() => store.Snapshot().Single(s => s.Key == "b").Active);
        await store.SelectAsync("a");
        Assert.True(store.Snapshot().Single(s => s.Key == "a").Active);
        Assert.Contains(server.Requests, r => (string)r["requestType"] == "SetCurrentProgramScene" && (string)r["requestData"]["sceneName"] == "A");

        aName = "Renamed";
        await server.EventAsync("SceneNameChanged", new JObject { ["sceneUuid"] = "a", ["oldSceneName"] = "A", ["sceneName"] = aName });
        await ProtocolTests.Until(() => store.Snapshot().Any(s => s.Key == "a" && s.Name == "Renamed" && s.Active));
        extra = true;
        await server.EventAsync("SceneCreated", Program("c", "C"));
        await ProtocolTests.Until(() => store.Snapshot().Length == 3);
        extra = false;
        await server.EventAsync("SceneRemoved", Program("c", "C"));
        await ProtocolTests.Until(() => store.Snapshot().Length == 2);
        await connection.StopAsync(); Assert.Empty(store.Snapshot());
    }

    [Fact]
    public async Task OlderSceneListCannotOverwriteANewerProgramEvent()
    {
        await using var server = new FakeObs();
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        int lists = 0;
        string active = "a";
        server.Handler = async request =>
        {
            Assert.Equal("GetSceneList", (string)request["requestType"]);
            if (Interlocked.Increment(ref lists) == 2)
            {
                entered.TrySetResult(true);
                return await release.Task;
            }
            return List(active);
        };
        await server.StartAsync();
        using var connection = new ObsConnection { Reconnect = false };
        using var store = new ObsSceneStore(connection) { Enabled = true };
        connection.Start(server.Endpoint, server.Password);
        try
        {
            await ProtocolTests.Until(() => store.Snapshot().Length == 2);
            store.Refresh(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            active = "b";
            await server.EventAsync("CurrentProgramSceneChanged", Program("b", "B"));
            await ProtocolTests.Until(() => store.Snapshot().Any(s => s.Key == "b" && s.Active));
            release.TrySetResult(List("a"));
            await ProtocolTests.Until(() => Volatile.Read(ref lists) >= 3);
            await ProtocolTests.Until(() => store.Snapshot().Any(s => s.Key == "b" && s.Active));
        }
        finally { release.TrySetResult(List("a")); await connection.StopAsync(); }
    }

    [Fact]
    public async Task RejectedSceneSelectionDoesNotChangeTheActiveHighlight()
    {
        await using var server = new FakeObs();
        server.Handler = request => (string)request["requestType"] == "SetCurrentProgramScene"
            ? throw new ObsRequestException("SetCurrentProgramScene", 600)
            : Task.FromResult(List("a"));
        await server.StartAsync();
        using var connection = new ObsConnection { Reconnect = false };
        using var store = new ObsSceneStore(connection) { Enabled = true };
        connection.Start(server.Endpoint, server.Password);
        await ProtocolTests.Until(() => store.Snapshot().Length == 2);
        await Assert.ThrowsAsync<ObsRequestException>(() => store.SelectAsync("b"));
        Assert.False(store.IsSwitching);
        Assert.True(store.Snapshot().Single(s => s.Key == "a").Active);
        Assert.False(store.Snapshot().Single(s => s.Key == "b").Active);
        await connection.StopAsync();
    }

    [Fact]
    public async Task ReconnectResynchronizesAndDisablingStopsSceneRefreshes()
    {
        await using var server = new FakeObs();
        string active = "a";
        server.Handler = request => Task.FromResult(List(active));
        await server.StartAsync();
        using var connection = new ObsConnection { Reconnect = true };
        using var store = new ObsSceneStore(connection) { Enabled = true };
        connection.Start(server.Endpoint, server.Password);
        await ProtocolTests.Until(() => store.Snapshot().Length == 2);
        active = "b"; await server.CloseAsync();
        await ProtocolTests.Until(() => server.Connections >= 2 && store.Snapshot().Any(s => s.Key == "b" && s.Active));
        store.Enabled = false;
        int requests = server.Requests.Count;
        await server.EventAsync("CurrentProgramSceneChanged", Program("a", "A"));
        await server.EventAsync("SceneCreated", Program("c", "C"));
        await Task.Delay(250);
        Assert.Empty(store.Snapshot()); Assert.Equal(requests, server.Requests.Count);
        await connection.StopAsync();
    }
}
