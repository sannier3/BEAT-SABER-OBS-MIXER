# Beat Saber 1.40.8 target

**English** | [Français](COMPATIBILITY.fr.md)

## Target versions

| Component | Target |
| --- | --- |
| Beat Saber | PC Windows 1.40.8 |
| BSIPA | ^4.3.0 |
| ChatPlexSDK_BS | 6.4.2, assembly identity 6.4.2.0 |
| OBSMixer plugin | net48 |
| OBSMixer.Core | netstandard2.0 |
| Tests | net8.0 |

The selected SDK comes from the `ChatPlexSDK-BS` submodule of [BeatSaberPlus V6.4.2](https://github.com/hardcpp/BeatSaberPlus/releases/tag/V6.4.2). Its official asset is named `BeatSaberPlus_v6.4.2-bs-1.40.0-to-1.40.8.zip`, and its release notes list 1.40.7/1.40.8 compatibility.

- BeatSaberPlus: `acb85e62987f1dc183b78ace5c70754b610d7a16`.
- ChatPlexSDK_BS: `9c6cc6e8be5cb1574d11f9ecf9c93a05a7b4e364`.
- Generic ChatPlexSDK: `b36daf8cb10f211d38017931f3f5c7d2532061c7`.

The SDK manifest at this revision still mentions 1.40.0. Its association with the 1.40.8-compatible release identifies the intended SDK set. The SDK project explicitly targets .NET Framework 4.8; the plugin uses the same target.

## Inspected APIs

| Mod feature | API in the target revision |
| --- | --- |
| Panel creation | `UISystem.FloatingPanelFactory.Create(string, Transform)` |
| Movement and locking | `CFloatingPanel.SetAllowMovement`, `GetAllowMovement`, `SetLockIcon` |
| Scene-specific position | `SetSceneTransform`, `OnSceneRelease` |
| View creation/destruction | `UISystem.CreateViewController<T>`, `DestroyUI` |
| UI lifecycle | `ViewController<T>.OnViewCreation`, `OnViewDestruction` |
| Controls | `XUISlider`, `XUIText`, `XUIPrimaryButton`, `XUIHLayout`, `XUIVLayout` |
| Scrolling | `XUIVScrollView`, `CVScrollView.Container`, `UpdateContentSize` |
| ChatPlex module APIs inspected during development | `ModuleBase<T>`, `GetSettingsViewControllersImplementation` |
| Dispatch to Unity | `MTMainThreadInvoker.Enqueue(Action)` |

Sixteen files covering panel interfaces, views, XUI controls, factories and modules match the initially inspected revision. Dispatcher changes in newer revisions do not prevent use of the `Enqueue(Action)` signature already present in the target revision. The OBS protocol remains independent of ChatPlex and was not changed.

## Compatibility changes

- The OBSMixer manifest targets 1.40.8 and requires exactly ChatPlexSDK_BS 6.4.2 using the Hive range `=6.4.2`.
- The plugin and its reference package target net48.
- PowerShell checks the ChatPlex DLL version before running tests and building.
- The MSBuild project also checks this version for direct IDE builds.
- Build output is collected from `bin/Release/net48`.
- The README, command examples and validation report target this game instance.

Version checks do not install or replace ChatPlex. If the instance contains another version, the build stops with an explicit error.

## Validation limits

This adaptation has been checked at source and metadata level. It is not a certification of in-game behavior. The plugin still needs to be built with the target installation's DLLs and tested with OBS, FPFC and a headset. The script does not determine the actual game version from Unity files: supply the path to the correct instance.

## Compact mixer revision

Two dependencies are used alongside the SDK: `BeatSaberMarkupLanguage` (`^1.12.5`) for the settings button on the home screen's left menu, and `BS Utils` (`^1.14.2`, DLL `BS_Utils.dll`) for pause/resume events. Both mods were present in the supplied game log. Missing DLLs stop the script before compilation; the script does not modify these dependencies.

Meters and faders use `UISystem.ImageFactory`; the Beat Saber SDK configures it with `HMUI.ImageView`. The masked keyboard uses the mod's own `IModal` class and the public `CreateModal`, `ShowModal` and `CloseModal` APIs, without reflection into the SDK's internal keyboard.

The mod no longer references PauseController directly or searches for pause objects every frame. It subscribes to public BS_Utils events. Visibility rules are isolated in the core and covered by tests without Unity, while event timing and visual results still need in-game validation.

## Scene window and foreground details

The optional scene window uses the existing obs-websocket v5 client and Scenes/Config subscriptions. GetSceneList, SetCurrentProgramScene and GetCurrentProgramScene were checked against the inspected official protocol. Selection targets Program even in Studio Mode. No web bridge or additional dependency is introduced for scene switching.

The floating views use the SDK's public factories. Details has its own Canvas with a higher sorting order and a pose centered in front of the mixer, independent of mixer width. The scene window has its own padlock and three positions for home/song selection/pause; it is disabled by default. The shared curvature radius defaults to 200 and the current configuration version is 5.

The FR/EN language feature references `Beat Saber_Data/Managed/BGLib.Polyglot.dll` to read the game's selected language. This DLL belongs to the game and must not be copied to Plugins. The three position contexts use ChatPlex's generic menu scene, the native song selection hierarchy and BS_Utils pause events.

`Beat Saber_Data/Managed/BeatSaber.ViewSystem.dll` is explicitly referenced to resolve NavigationController and its inherited members. This game assembly is checked before compilation and excluded from the distributed package (`Private=false`). The supplied Windows log confirms that all 42 tests passed before this reference correction; the corrected plugin build still needs confirmation on that installation.

The optional preview uses GetCurrentProgramScene and GetSourceScreenshot from obs-websocket v5. JPEG/PNG decoding uses UnityEngine.ImageConversionModule.dll, provided by Beat Saber and excluded from the package. The preview shows Program, including in Studio Mode, at an interval of 100–10000 ms (100 by default). The current configuration version is 5.

## Documentation

[User guide](README.md) · [Development](DEVELOPMENT.md) · [Compatibility](COMPATIBILITY.md) · [Validation](VALIDATION.md)
