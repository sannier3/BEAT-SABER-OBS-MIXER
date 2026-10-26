using Newtonsoft.Json.Linq;
using OBSMixer.Core;
using Xunit;

namespace OBSMixer.Tests;

public sealed class MixerTests
{
    private static JObject Reply(JObject request)
    {
        string type = (string)request["requestType"];
        string scene = (string)request["requestData"]?["sceneName"];
        switch (type)
        {
            case "GetInputList": return JObject.Parse("{inputs:[{inputName:'Mic',inputUuid:'mic'},{inputName:'Music',inputUuid:'music'},{inputName:'Hidden',inputUuid:'hidden'},{inputName:'Desktop',inputUuid:'desktop'},{inputName:'Image',inputUuid:'image'}]}");
            case "GetSpecialInputs": return JObject.Parse("{desktop1:'Desktop',mic1:null}");
            case "GetCurrentProgramScene": return JObject.Parse("{currentProgramSceneName:'Main'}");
            case "GetSceneItemList":
                return scene == "Main" ? JObject.Parse("{sceneItems:[{sourceName:'Mic',sceneItemEnabled:true},{sourceName:'Group',isGroup:true,sceneItemEnabled:true},{sourceName:'Hidden',sceneItemEnabled:false},{sourceName:'Image',sceneItemEnabled:true}]}") : new JObject();
            case "GetGroupSceneItemList": return JObject.Parse("{sceneItems:[{sourceName:'Music',sceneItemEnabled:true},{sourceName:'Mic',sceneItemEnabled:true},{sourceName:'Main',sourceType:'OBS_SOURCE_TYPE_SCENE',sceneItemEnabled:true}]}");
            case "GetInputVolume":
                if ((string)request["requestData"]["inputName"] == "Image") throw new ObsRequestException(type, 604);
                return JObject.Parse("{inputVolumeDb:-8.1}");
            case "GetInputMute": return JObject.Parse("{inputMuted:false}");
            case "GetInputAudioSyncOffset": return JObject.Parse("{inputAudioSyncOffset:120}");
            case "GetInputAudioTracks": return JObject.Parse("{inputAudioTracks:{'1':true,'2':false,'3':false,'4':false,'5':false,'6':true}}");
            case "GetInputAudioMonitorType": return new JObject { ["monitorType"] = ObsProtocol.MonitorOff };
            default: return new JObject();
        }
    }
    [Fact] public async Task TraversesGroupsDeduplicatesIncludesGlobalsAndAppliesEvents()
    {
        await using var server = new FakeObs { Handler = request => Task.FromResult(Reply(request)) };
        await server.StartAsync();
        using var connection = new ObsConnection { Reconnect = false };
        using var store = new MixerStore(connection);
        connection.Start(server.Endpoint, server.Password);
        await ProtocolTests.Until(() => store.Snapshot().Length == 3);
        Assert.Equal(new[] { "Desktop", "Mic", "Music" }, store.Snapshot().Select(x => x.Name));
        var mic = store.Snapshot().Single(x => x.Key == "mic");
        Assert.Equal(-8.1, mic.VolumeDb); Assert.Equal(120, mic.OffsetMs); Assert.True(mic.Track(6));
        await server.EventAsync("InputVolumeChanged", JObject.Parse("{inputUuid:'mic',inputName:'Mic',inputVolumeDb:-3.5}"));
        await server.EventAsync("InputMuteStateChanged", JObject.Parse("{inputUuid:'mic',inputName:'Mic',inputMuted:true}"));
        await server.EventAsync("InputAudioSyncOffsetChanged", JObject.Parse("{inputUuid:'mic',inputName:'Mic',inputAudioSyncOffset:250}"));
        await server.EventAsync("InputAudioTracksChanged", JObject.Parse("{inputUuid:'mic',inputName:'Mic',inputAudioTracks:{'1':false,'6':true}}"));
        await server.EventAsync("InputAudioMonitorTypeChanged", new JObject { ["inputUuid"] = "mic", ["monitorType"] = ObsProtocol.MonitorOnly });
        await server.EventAsync("InputVolumeMeters", JObject.Parse("{inputs:[{inputUuid:'mic',inputName:'Mic',inputLevelsMul:[[0.2,0.3,1.0],[0.4,0.5,1.0]]}]}"));
        await ProtocolTests.Until(() => store.Meter("mic") != null);
        mic = store.Snapshot().Single(x => x.Key == "mic");
        Assert.Equal(-3.5, mic.VolumeDb); Assert.True(mic.Muted); Assert.Equal(250, mic.OffsetMs);
        Assert.False(mic.Track(1)); Assert.Equal(ObsProtocol.MonitorOnly, mic.MonitorType);
        Assert.Equal(.5, store.Meter("mic").RightPeak);
        await store.SetAsync(mic, "SetInputMute", "inputMuted", false);
        Assert.Contains(server.Requests, r => (string)r["requestType"] == "SetInputMute" && (bool)r["requestData"]["inputMuted"] == false);
        store.ActiveSceneOnly = false;
        await ProtocolTests.Until(() => store.Snapshot().Length == 4);
        await connection.StopAsync(); Assert.Empty(store.Snapshot());
    }

    [Fact] public async Task RecentAudioAndPinnedSourcesAreVisibleOutsideTheCurrentScene()
    {
        await using var server = new FakeObs { Handler = request => Task.FromResult(Reply(request)) };
        await server.StartAsync();
        using var connection = new ObsConnection { Reconnect = false };
        using var store = new MixerStore(connection);
        connection.Start(server.Endpoint, server.Password);
        await ProtocolTests.Until(() => store.Snapshot().Length == 3);
        Assert.Equal(4, store.AllSnapshot().Length);
        store.IncludeRecentlyAudible = true;
        await server.EventAsync("InputVolumeMeters", JObject.Parse("{inputs:[{inputUuid:'hidden',inputName:'Hidden',inputLevelsMul:[[0.02,0.03,0.03]]}]}"));
        await ProtocolTests.Until(() => store.Snapshot().Length == 4);
        Assert.Equal(1, store.Meter("hidden").Channels);
        await server.EventAsync("InputVolumeMeters", JObject.Parse("{inputs:[{inputUuid:'hidden',inputName:'Hidden',inputLevelsMul:[[0,0,0]]}]}"));
        store.SetPinned(new[] { "hidden" });
        await Task.Delay(1700);
        Assert.Equal(4, store.Snapshot().Length);
        store.SetPinned(Array.Empty<string>());
        Assert.Equal(3, store.Snapshot().Length);
        await connection.StopAsync();
    }
}
