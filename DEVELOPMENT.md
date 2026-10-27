# Development and release preparation

**English** | [Français](DEVELOPMENT.fr.md)

This document covers building the project. For user installation instructions, see [README.md](README.md).

## Environment

- Windows, PowerShell and the .NET 8 SDK.
- A modded Beat Saber PC 1.40.8 instance with the manifest dependencies installed.
- ChatPlexSDK_BS 6.4.2, assembly identity 6.4.2.0.

The plugin targets .NET Framework 4.8, the core targets netstandard2.0 and the tests target .NET 8. Game assemblies are compilation references and are not redistributed. BeatSaber.ViewSystem.dll provides the base class for selection controllers; BGLib.Polyglot.dll provides the game's language model.

## Build and install locally

From the project folder, replace the example path with your game instance:

```powershell
.\scripts\build.ps1 -BeatSaberDir 'C:\Games\BeatSaber\1.40.8' -Install
```

The script checks required files, runs the tests, builds the plugin and creates **dist/OBSMixer-0.1.0.zip**. With `-Install`, it copies both DLLs into the instance. Omit `-Install` to create the package only. The existing configuration file is preserved.

If Windows displays a security prompt, review the script before running it. `-SkipTests` is available for local diagnostics; prepare releases with tests enabled.

## Tests

Without a game instance:

```powershell
dotnet test .\tests\OBSMixer.Tests\OBSMixer.Tests.csproj -c Release
```

Using the BSIPA manifest parser from the game instance:

```powershell
dotnet test .\tests\OBSMixer.Tests\OBSMixer.Tests.csproj -c Release '-p:BeatSaberDir=C:\Games\BeatSaber\1.40.8'
```

The build script enables the additional manifest test automatically. Tests use a fake OBS server and do not require OBS to be running. They do not replace headset testing.

## Release package

The generated ZIP contains:

```text
Plugins/OBSMixer.dll
Libs/OBSMixer.Core.dll
README-OBSMixer.md
README-OBSMixer.fr.md
```

The user guides are renamed during packaging and their links are adjusted for the distributed files. Technical links point to the matching language in the repository. Do not publish a source ZIP as an installable mod archive. Do not copy the entire bin folder: Unity, game and dependency DLLs must remain those provided by the user's installation. The package must not contain UserData/OBSMixer.json or any password.

Before publishing, confirm a successful build, then test in game: OBS connection and reconnection, volume/mute/details, scenes, visibility restricted to home/song selection/pause, three positions for the mixer and scene windows, padlocks, and automatic/manual languages. Also check the preview's shared position and eye toggle. [VALIDATION.md](VALIDATION.md) records checks and known validation limits.

Attach **dist/OBSMixer-0.1.0.zip** to the GitHub release and list Beat Saber **1.40.8**, ChatPlexSDK_BS **6.4.2** and the other dependencies in its notes. The BSMG announcement should link to that release. This does not imply validation or approval by BSMG.

## Project structure

- `src/OBSMixer.Core`: obs-websocket v5 connection, source and scene state, audio selection, pagination and translations.
- `src/OBSMixer.Plugin`: BSIPA integration, ChatPlex windows, BSML settings, positions and menu/pause tracking.
- `tests/OBSMixer.Tests`: network, state, presentation, language and manifest tests.
- [COMPATIBILITY.md](COMPATIBILITY.md): target versions and APIs.

Language accepts `auto`, `fr` and `en`. Configuration version 5 preserves existing Menu/Playing positions and initializes missing legacy SongSelection positions with independent copies. Settings are saved in UserData/OBSMixer.json.

## Program preview

ObsPreview shares the mod's OBS connection. Each refresh calls GetCurrentProgramScene, followed by GetSourceScreenshot (JPEG, bounded to 640 × 360, quality 60). PreviewIntervalMs defaults to 100 and is clamped to 100–10000. The slider uses 100 ms steps. Only one refresh may be in flight. Hiding the image, changing Program, disconnecting or disposing invalidates the response. Images are decoded on Unity's main thread; replaced sprites and textures are destroyed.

Rendering uses UnityEngine.ImageConversionModule.dll, a game reference that is not redistributed. PreviewPanelEnabled defaults to false; PreviewImageVisible defaults to false and remembers the eye toggle. PreviewPose is shared by the Menu and Playing generic scenes. Visibility follows the same home/song selection/pause policy as the rest of the mod, independently of PanelVisible.

## Initial values supplied by JBSAN

The seven initial poses (mixer and scenes for home/song selection/pause, plus the shared preview) use the coordinates supplied by JBSAN. All three padlocks start locked. Mixer window: 180 × 130, scale 0.75, text size 3.0, eight sources per page and no total source limit. Preview starts disabled with the image hidden and a 100 ms interval. F9 restores the mixer's initial pose for the current context. Invalid numeric values and null poses fall back to these same settings. Configuration remains at version 5 and no existing user file is overwritten.

## About artwork

The supplied PNG is stored in `src/OBSMixer.Plugin/Resources/StreamerAssistant.png` and embedded in the plugin DLL as `OBSMixer.Resources.StreamerAssistant.png`. It is not a separate installation dependency. The About view decodes it with the existing ImageConversion module reference, preserves transparency and aspect ratio, caches it across language changes and disposes its sprite/texture with the view. Keep the original asset when building from source.

## Documentation

[User guide](README.md) · [Development](DEVELOPMENT.md) · [Compatibility](COMPATIBILITY.md) · [Validation](VALIDATION.md)
