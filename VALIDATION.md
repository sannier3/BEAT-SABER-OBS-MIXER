# Validation on 26 October 2026: Beat Saber 1.40.8 target

**English** | [Français](VALIDATION.fr.md)

## Latest reported Windows result

The latest build log supplied by the user builds OBSMixer.Core and OBSMixer.Tests and runs **42 tests, all passing**. Plugin compilation then fails because BeatSaber.ViewSystem.dll was not referenced. The eight diagnostics concern this missing assembly and inherited NavigationController members.

The BeatSaber.ViewSystem reference has now been added with Private=false. The script also checks for BeatSaber.ViewSystem.dll and BGLib.Polyglot.dll; direct MSBuild builds have dedicated errors if they are missing. The UnityEngine.Object conversion and inherited-member errors follow from the missing reference. The corrected plugin build still needs confirmation on the user's instance.

The following sections preserve the revision history and local checks.

## Checks performed

- C# syntax analysis using tree-sitter-c-sharp: passed.
- XML parsing of all three projects and JSON parsing of the manifest: passed.
- OBS request names checked against the retrieved official protocol: present.
- ChatPlexSDK_BS 6.4.2 located through the submodule of the official BeatSaberPlus V6.4.2 release compatible with 1.40.8.
- `.NET Framework 4.8` target read from its project; assembly identity `6.4.2.0` read from assembly attributes.
- Sixteen ChatPlex API files compared: identical contents between the initially inspected revision and the target revision. The public `MTMainThreadInvoker.Enqueue(Action)` signature is present in the target revision.
- Static checks of the manifest, net48 reference package, build output path and ChatPlex version checks: consistent with the target.
- PowerShell and MSBuild version checks could not be executed locally and initially required a Windows build for validation.
- Authentication formula and test vector independently checked in Python.
- Archive structure checked: sources, tests and documentation present; no proprietary DLLs or personal configuration included.

## Checks not performed locally

- C# core compilation: not performed. The .NET 8.0.427 SDK was downloaded, but its runtime fails to start with `Failed to create CoreCLR, HRESULT: 0x8007000E` in this environment.
- xUnit execution: not performed for the same reason. Providing tests does not establish that they pass.
- Plugin compilation: not performed; the Beat Saber/BSIPA/ChatPlex assemblies are not available locally.
- Connection to a real OBS instance, Unity UI, FPFC and VR interaction: not performed.

## Included automated tests

Tests cover an authentication vector, stereo/mono meter decoding, out-of-order network responses, timeouts, request rejection, cancellation of pending requests, incorrect passwords, reconnection, group traversal, scene cycles, deduplication, global inputs, exclusion of inputs without audio, property events and outgoing commands.

The initial next step on the target PC was to obtain the full `scripts/build.ps1` output, followed by a Beat Saber + OBS test. Further compilation or integration fixes may still be needed.

## Test server lifecycle fix

The first reported Windows build compiled `OBSMixer.Core` and `OBSMixer.Tests`. Three of six tests passed; three failed in `FakeObs.DisposeAsync` with `ObjectDisposedException: IFeatureCollection has been disposed`. The script stopped before plugin compilation.

The fix removes `Abort()` on the last WebSocket reference, which could belong to an already completed HTTP context. A shutdown token now cancels reads, sends and response waits. The HTTP callback awaits its responses before completing and clears its connection reference in `finally`. Shutdown awaits Kestrel before releasing resources; repeated calls share one shutdown task.

Two regression tests were added: repeated shutdown after a connection ends, and shutdown with an active connection and a pending response. The suite then contained eight tests. Local execution was attempted but .NET still failed at startup with `Failed to create CoreCLR, HRESULT: 0x8007000E`. At that revision, all eight tests still required Windows execution and no passing result was claimed locally.

## Windows result and logger fix

The next build log confirms eight of eight tests passed without failures under .NET 8.0.31. The net48 plugin build then reported two CS0104 errors in `Plugin.cs`: `Logger` was ambiguous between `IPA.Logging.Logger` and `UnityEngine.Logger`.

The import was replaced with the explicit alias `using Logger = IPA.Logging.Logger;`, making both the logger property and BSIPA-injected parameter use the intended type. The rest of the plugin was unchanged. Syntax was checked locally; the full plugin build still required Windows confirmation.

## First game log diagnosis

The supplied log shows BSIPA 4.3.6 detecting `OBSMixer.dll` but rejecting its metadata with `ArgumentException: 6.4.2` in `Hive.Versioning.VersionRange`. The mod therefore never initializes. ChatPlexSDK_BS 6.4.2 is present and loaded; errors from its own OBS integration are unrelated to OBSMixer.

The dependency was corrected to `"ChatPlexSDK_BS": "=6.4.2"`. The equality operator was confirmed in the `Atlas-Rhythm/Hive` parser and tests, and in BSIPA's range converter, which constructs a `VersionRange` directly from this string. The build-time assembly identity check remains 6.4.2.0.

The script now passes the game path to tests. A ninth test references the instance's actual `Libs/Hive.Versioning.dll`, parses the embedded manifest and checks accepted/rejected versions. The original eight tests remain usable without a game instance. The new test and corrected mod loading still needed Windows validation at that stage.

## Compact mixer, left menu and pause revision

This revision adds a paginated vertical-strip mixer, meters using ChatPlex's image factory, nonlinear vertical faders and a Details panel for tracks, monitoring and offset. Settings move out of the floating mixer to a BSML OBS Mixer button on the left menu.

Appearance options include width, height, scale, text size, background opacity and curvature. Existing Menu/Playing positions are retained. The panel is allowed only in menus and pause; pause/resume events come from BS_Utils, already present in the supplied log. Release saves are protected against context changes during movement.

The model now retains every controllable audio input to support pinned and recently audible sources without rebuilding the model on each meter event. Displayed sources remain filtered by scene, pins, hidden sources and recent-audibility settings. Meter readings are post-fader, without multiplying by gain a second time; channel count distinguishes mono from stereo.

Checks performed:

- Syntax analysis of 23 C# files and XML parsing of three projects.
- Manifest JSON, `=6.4.2` range and BSML/BS Utils dependencies checked.
- Public ChatPlex APIs inspected for ImageFactory, CImage, FlowCoordinator, IModal/CreateModal, XUI and floating panels.
- BSML RegisterButton/UnregisterButton and MenuButton constructor inspected; SDK 6.4.2 uses the same APIs.
- Public songPaused/songUnpaused declarations and their connection to PauseController events inspected in BS_Utils.
- Source ZIP structure and integrity checked.

New tests cover fader curve and inverse conversion, precision near 0 dB, menu/pause/gameplay visibility, transitions and enable state, pagination, audible off-scene sources and pins. This revision provided 20 cases without the game and 21 with the BSIPA manifest test enabled by the build script.

Execution was attempted locally, but .NET still failed at startup with `Failed to create CoreCLR, HRESULT: 0x8007000E`. The new tests, plugin compilation and Unity rendering were not claimed as validated. The earlier user-reported eight passing tests applied to the earlier revision.

Checks needed on the real instance: left-menu button, masked keyboard, curved meters/faders, pointer interaction, readability at different sizes, padlock, separate menu/pause persistence, gameplay hiding and resume. OBSMixer adds no new Harmony patch.

## Source limit and label spacing

MaxDisplayedSources was added: 0 means All; 1–64 caps the total after hidden-source filtering, pin priority and source ordering. This is separate from Sources per page. Previous configurations remain unlimited by default. If fewer sources are selected than fit on a page, strips fill the available width.

L/R or M labels move above the bars; dB stays below the fader. Bars and fader are shortened to leave room before the value. Calculated vertical bounds at every configurable height preserve at least three units between meter ends and the dB label. Visual output still requires Unity validation.

Three selection tests were added: limits with pins/hidden sources/custom order, no cap with All, and deleted-source references not consuming the limit. The suite reached 23 cases without the game and 24 with the BSIPA manifest test. Syntax and static checks passed; the new tests were not run locally because the .NET runtime remained unavailable.

## Stable Details, explicit pagination and slimmer faders

Details was previously a child of the main panel outside its rectangle, while the floating-panel factory installs a RectMask2D. It is now created at the root with its own lifecycle, following mixer position, rotation and scale in LateUpdate. It no longer inherits the mixer's clipping mask. The source is assigned before activating the view. An unavailable source no longer closes the panel automatically, and Close remains usable when disconnected. Menu/pause/gameplay rules are explicitly applied to both panels.

Sources per page is now an explicit capacity from 1 to 8, independent of width. Strips and labels compress as needed. ContentSizeFitter is disabled on manually positioned components so it cannot overwrite their calculated dimensions.

The fader rail is 0.65 units wide; the thumb changes from 12×4 to at most 6×2.5 units. The hit area remains wider than the rail with an almost invisible background. The meter/fader group is centered in each strip and adapts to narrow strips.

The pagination test was updated. A regression case checks widths 100, 160, 184, 185 and 260 with six/eight sources without creating a second page. The suite reached 24 cases without the game and 25 with the manifest test. Syntax, XML/JSON and layout-bound calculations passed. New tests and Unity rendering still could not be executed locally.

In-game checks needed: repeated Details open/close, changing sources, opening while paused then resuming, following mixer movement/resizing, stable page count around width 185, and laser readability/interaction with the slimmer fader.

## Foreground Details and optional scene window

Details is now centered on the mixer with a 12 cm offset along its front normal, retaining rotation and scale and using a Canvas with a higher sorting order. This no longer depends on mixer width. Visual output and raycasts require Unity validation.

ObsSceneStore was added independently of Unity: GetSceneList supplies the initial state; CurrentProgramSceneChanged updates the active scene; list/create/remove/rename events and reconnection trigger synchronization. A state revision prevents an old GetSceneList response from overwriting a newer event. Selection sends SetCurrentProgramScene and confirms with GetCurrentProgramScene, without optimistic highlighting.

The optional scene window starts disabled, with its own padlock and, at that revision, two menu/pause positions. It can stay visible when only the mixer is hidden but never during gameplay. Selector operations stop when disabled; buttons and state update without rebuilding objects on every active-scene change.

Configuration moved to version 3. The default curvature radius and invalid-value fallback are 200. Older configurations with 140 migrate to 200; other settings and existing positions are retained.

Four scene-selector network tests were added, covering opt-in activation, bidirectional synchronization and list changes, rejection without false highlighting, stale list responses, reconnection and disabling. The suite reached 28 cases without the game and 29 with the BSIPA manifest test. New code was syntax-checked; request and event names are present in the inspected official obs-websocket protocol.

These C# tests and Unity compilation still could not run locally because CoreCLR failed to start. No in-game success was claimed for the new window. Target-instance checks: centered Details after movement/rotation, scene window disabled on first launch, Program changes in both directions, lists larger than eight, rename/remove/reconnect, padlock and separate menu/pause position saves.

## Details opacity, scene order and credits

Details uses max(0.95, general opacity) for its background, independently of mixer transparency. Following behavior and visibility rules are unchanged.

Official obs-websocket code (Obs_ArrayHelper.cpp, GetSceneList) numbers scenes from the bottom and reverses the array before sending it. The parser now sorts sceneIndex in descending order to restore the OBS dock's top-to-bottom order. Rendering already fills each row from left to right. GetSceneList and SceneListChanged share this parser; when indices are missing, the stable sort preserves incoming order. A new network test covers ten scenes, reordering and the active scene after reordering. Total: 29 cases without the game, 30 with the optional manifest test.

At that revision, a dedicated view occupied the right settings panel, with blocks for project source code, the related Streamer Assistant project and Hardcpp's SDK. Application.OpenURL opens links; center and right controllers are destroyed with the flow. XUI signatures and the right-hand tuple position were checked in SDK 6.4.2 sources.

Local validation: C# syntax, XML and JSON analysis. Compilation, tests and VR rendering were not validated locally because CoreCLR was unavailable. Headset checks needed: readable Details at general opacity 0 and 1; OBS order across pages and after reordering; readable, complete right-hand credits and correct link destinations.

## Three contexts, left-hand credits and language choice

The flow now uses (settings, credits, null), placing About on the left. MenuContextTracker identifies song selection through native LevelSelectionNavigationController instances, isActiveAndEnabled, isInViewControllerHierarchy and isInTransition. These members appear in the inspected SDK 6.4.2 sources. Instances are cached and rediscovered every two seconds in menus; displayed state is checked every 150 ms. Navigation animations are ignored until stable. No UI objects are searched during gameplay.

Menu/SongSelection/Playing and ScenesMenu/ScenesSongSelection/ScenesPlaying store three distinct positions. Playing remains reserved for pause. Menu-context changes save the previous pose before applying the next. Releases after a context change are rejected and the new context's pose is restored. Configuration migration to version 4 creates independent selection-pose copies from existing menu poses without removing old settings.

Automatic language reads BGLib.Polyglot.Localization.Instance.SelectedLanguage, whose use was checked in SiraLocalizer's LocalizationManager sources. French resolves to fr; any other or unavailable language resolves to en. Manual fr/en choices take priority. No SiraLocalizer dependency is added: only the game's BGLib.Polyglot DLL is referenced. The language model is polled every 500 ms on Unity's thread. Language revisions refresh settings, credits, keyboard, Details and both floating windows without restarting. OBS names are unchanged.

The catalog then contained 85 FR/EN entries. Ten language-resolution cases, one translation round-trip test and one SongSelection visibility test were added. They were not claimed as executed: the local SDK still failed at CoreCLR startup (0x8007000E, code 137). C# syntax, XML/JSON and catalog audits were performed separately.

Checks needed on 1.40.8: move and rotate both windows in all three contexts, navigate home/selection, start gameplay then pause/resume, release after a transition and check persistence after restarting. Check automatic mode with French and English/another game language, both manual choices, persistence and refreshed tabs/credits/Details/scenes/keyboard. Final rendering and Unity paths still require headset validation.

## Community documentation and packaging

README.md became the English user guide and README.fr.md its French counterpart. They cover requirements, release ZIPs versus GitHub Source code archives, installation into the correct BSManager instance, OBS WebSocket, mixer controls, Program scenes, three positions, languages, updates and troubleshooting. DEVELOPMENT.md contains build and publication preparation instructions. No BSMG approval or actual publication is claimed.

The script includes both guides as README-OBSMixer.md and README-OBSMixer.fr.md. It adjusts reciprocal links and routes technical links to the repository. PowerShell reads/writes explicitly use UTF-8. Project references were compared with the script's required files; source/package links, C# syntax and XML were checked. The PowerShell script was not run on Windows locally; no release DLL is generated in this environment.

## Optional Program preview and configurable interval

ObsPreview was added using GetCurrentProgramScene / GetSourceScreenshot, JPEG bounded to 640 × 360 and quality 60. Request names and fields were checked in the official obs-websocket protocol and sources available in the workspace. At that revision, the interval defaulted to 2000 ms, configurable from 100 to 10000 ms in 100 ms steps, with no overlapping refreshes. No calls are made while inactive or disconnected.

The opt-in window is independent of the mixer, with a graphical eye button at the top left, a padlock and one persistent position shared across home, song selection and pause. It stays hidden during gameplay. Hiding the image clears the current frame, cancels the wait and rejects old responses. Program changes or reconnection also invalidate the image. Invalid images produce a preview-local message without breaking the mixer connection. Textures and sprites are replaced and destroyed on Unity's thread.

Configuration moved to version 5, initially with PreviewPanelEnabled=false, PreviewImageVisible=true, PreviewIntervalMs=2000 and PreviewPose. Existing settings are preserved. UnityEngine.ImageConversionModule.dll is explicitly referenced, checked by the script and not redistributed. New labels are translated into French and English; both community README files describe the feature and its shared-position exception.

Four network tests were added: no hidden captures and configurable timing at 100/10000 and 2000; no overlap and rejection of a late response after hiding; invalidation on Program change and reconnection; recovery from invalid captures. The suite now declares 46 cases including the manifest test. The previous 42 tests were confirmed by the user's Windows log; the four new tests are not claimed as executed. Local dotnet test still fails at CoreCLR startup (0x8007000E, exit 137).

Headset checks needed: eye open/close, no captures while hidden, shared position through all three contexts, padlock, no window during gameplay, OBS reconnection, Program changes, differing video aspect ratios, Studio Mode and short/long intervals. Plugin compilation and Unity rendering of this feature still require confirmation on the target installation.

## New defaults supplied by JBSAN

MixerConfig defaults match the supplied JSON: mixer/scenes/preview padlocks locked; scenes and preview disabled; eye closed; interval 100 ms; window 180 × 130; scale 0.75; text size 3.0; eight sources per page. All seven poses are copied field by field with float literals matching Unity storage. Other settings (connection, language, filtering, volume, curvature and empty lists) retain the supplied values. Configuration remains at version 5.

Numeric fallbacks and null-pose fallbacks were aligned. F9 restores the new pose for the current context. Missing legacy selection positions are copied from menu poses only in configurations older than version 4 with a saved menu pose. Existing files are not overwritten; explicit saved settings retain priority. ObsPreview's initial interval is also 100 ms; the timing test still checks a custom 2000 ms interval.

English/French README files and the developer guide describe the new defaults. Static checks covered fields, coordinates, C# syntax and project files; Unity compilation and C# tests remain unavailable locally because CoreCLR cannot start. Previous sections are historical and may describe defaults that have since changed.

## Bilingual documentation

All four documents now have an English default file and a French `.fr.md` counterpart. Each includes a language switch and documentation links that stay in the selected language. Release README links are rewritten to the matching packaged README or the matching technical document in the repository. Static checks cover all eight files, counterpart links, language routing and packaged link destinations. This documentation update does not change plugin behavior or add an in-game validation result.

## About artwork and GitHub links

The supplied Streamer Assistant PNG is included unchanged at `src/OBSMixer.Plugin/Resources/StreamerAssistant.png` and embedded as `OBSMixer.Resources.StreamerAssistant.png`. The About view loads it once per view instance using ImageConversion, preserves transparency and aspect ratio, and releases its texture/sprite when destroyed. Language changes reuse the loaded artwork. It is placed at the top center of the SDK's 120 × 80 side panel. Explicit placement keeps the credits within the panel without scrolling.

The personal website link has been removed. Separate buttons now open the sannier3 GitHub profile and the project repository; the Streamer Assistant project and Hardcpp SDK credits remain. Both README languages have matching credit links. Static checks cover the resource declaration, exact PNG bytes, C# syntax, layout bounds, language links and removal of the personal website URL. Final compilation and headset rendering remain unverified locally.

## Documentation

[User guide](README.md) · [Development](DEVELOPMENT.md) · [Compatibility](COMPATIBILITY.md) · [Validation](VALIDATION.md)

## SDK allowlist update, version 0.1.0 unchanged

The manifest and both build guards now allow exactly 6.4.2, 6.4.4 and 6.4.5. The optional Hive manifest test checks all three and rejects 6.3.3, 6.4.1, 6.4.3, 6.4.6, 6.5.0, 7.0.0 and 6.4.5-beta.1. Official binary inspection found all 35 required SDK types and 110 member references in 6.4.4 and 6.4.5. In-game tests have not been run for those versions.

CoreCLR still fails to initialize in this environment (0x8007000E); neither compilation nor .NET tests are claimed. The distributed DLL is updated in place only within its embedded JSON manifest, using padding to preserve resource size. It is unsigned. PE headers, CLI metadata, all bytes outside that JSON resource, the logo and the Core DLL are verified unchanged. Both archives contain the updated manifest and keep assembly/plugin version 0.1.0.
