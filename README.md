# OBS Mixer for Beat Saber

Control your OBS audio mix and switch scenes without taking off your VR headset.

**English** | [Français](README.fr.md)  
[Download releases](https://github.com/sannier3/BEAT-SABER-OBS-MIXER/releases) · [Report an issue](https://github.com/sannier3/BEAT-SABER-OBS-MIXER/issues)

OBS Mixer adds a floating audio mixer to **Beat Saber PC 1.40.8**. Adjust source volume, mute audio, watch live level meters and access audio tracks, monitoring and sync offset. An optional scene window lets you switch OBS scenes and see which one is active. A separate optional preview window shows OBS Program output.

The windows appear on the home screen, during song selection and while a song is paused. They stay hidden while you play.

## Requirements

| Component | Required version |
| --- | --- |
| Beat Saber | **PC Windows 1.40.8**, with mods enabled |
| BSIPA | 4.3.x |
| ChatPlexSDK_BS | **6.4.2** |
| BeatSaberMarkupLanguage (BSML) | 1.12.5 or a later version compatible with Beat Saber 1.40.8 |
| BS_Utils | 1.14.2 or a later version compatible with Beat Saber 1.40.8 |
| OBS Studio | obs-websocket **v5** enabled; included with OBS Studio 28 and newer |

Install the required Beat Saber mods and their dependencies in the same game instance. ChatPlexSDK_BS must be version 6.4.2 for this build. Other Beat Saber versions and standalone Quest installations are not supported by this package.

OBS Mixer connects directly to OBS. No website account or Streamer Assistant installation is required.

## Installation

1. Close Beat Saber.
2. Open the [Releases page](https://github.com/sannier3/BEAT-SABER-OBS-MIXER/releases) and choose a release for **Beat Saber 1.40.8**.
3. Download the attached mod archive, for example **OBSMixer-0.1.0.zip**. GitHub's **Source code** archives are for developers and cannot be installed directly.
4. Extract the archive into the folder containing **Beat Saber.exe**. Merge the `Plugins` and `Libs` folders with the existing ones.
5. Start the game and select **OBS Mixer** in the mods menu on the left of the home screen.

The installed mod files should be:

```text
Plugins/OBSMixer.dll
Libs/OBSMixer.Core.dll
```

With BSManager, open the folder of your **1.40.8 instance** and install there. You do not need PowerShell or a .NET SDK to install a compiled release.

## Connect to OBS

1. Start OBS Studio.
2. Open **Tools → WebSocket Server Settings** and enable the WebSocket server.
3. Keep authentication enabled and copy the server password. Note the port, normally **4455**. See the [official OBS setup guide](https://obsproject.com/kb/remote-control-guide).
4. In Beat Saber, open **OBS Mixer → OBS**.
5. For OBS running on the same PC, enter host **127.0.0.1**, the OBS port and the same password.
6. Select **Connect / apply**. The status should change to **CONNECTED**.

Connection on startup and automatic reconnection can be enabled in the same tab. After correcting a rejected password, select **Connect / apply** again.

## Use the mixer

Each audio strip contains the source name, live mono or stereo meters, a volume fader, its value in dB, a mute button and **Details**. On narrow strips, Details is shown as **…**.

- Drag a fader to change the source volume in OBS.
- Select the mute button to mute or unmute that source.
- Open **Details** to change audio tracks 1–6, monitoring and the OBS audio sync offset. The offset adjusts the source's audio timing.
- Use the page arrows when more sources are available.

These controls change OBS itself and affect its audio output. Source names and scene names match those configured in OBS.

By default, the mixer shows controllable audio sources from the active scene and global audio devices. In **Sources**, pin a source to keep it available outside the active scene, hide unwanted sources or change their order. In **Appearance**, **Sources to display** limits the total shown and **Sources per page** sets how many strips appear side by side.

## Optional scene window

Enable **General → OBS scenes window** to show scene buttons. This option is off by default.

The active scene is highlighted in blue. Buttons follow the order of the OBS Scenes list, from left to right and then onto the next row, with eight scenes per page. Changes made in OBS are reflected in the window.

**Selecting a scene changes Program, including in Studio Mode. If you are live, it changes the scene sent to your audience.**

## Optional OBS preview

Enable **General → OBS preview window** to show a floating image of OBS **Program output**, the picture sent to your audience. It does not show the separate Preview scene in Studio Mode. The window is off by default. When first enabled, its eye is closed; select it to start the image refresh.

- Select the **eye button at the top left** to show or hide the image. Hiding it stops screenshot requests while keeping the window and its controls visible.
- Set **Preview interval (ms)** in General from **100 ms to 10,000 ms**. The default is **100 ms (0.1 second)**. This is a periodically refreshed image, not a video feed.
- Captures run only while the image is visible and OBS is connected. A slow request is allowed to finish before another starts; requests do not build up.
- The preview has its own padlock and **one shared saved position** across the home screen, song selection and pause. It stays hidden during active gameplay.
- Its visibility is independent of the mixer's visibility. Turning off the preview option hides the entire preview window. Disabling the mod hides all its windows.

## Position, appearance and language

Windows are locked by default. Unlock a window with its padlock, move and rotate it with your VR controller, then release it to save the position. Lock it again to keep it in place.

The mixer and scene windows each remember **three separate positions**: home screen, song selection and pause. Move it in the relevant context to set that position. The Details panel follows the mixer. The preview instead uses a single shared position.

Open the mod settings to adjust:

| Tab | Options |
| --- | --- |
| General | Enable the mod, show the mixer, enable scene/preview windows, set the preview interval, choose the language and source filtering |
| Appearance | Window width and height, scale, text size, background opacity, curvature and source counts |
| OBS | Connection address, port, password, connection and reconnection options |
| Sources | Pin, hide and reorder audio sources |

Language defaults to **Automatic (Beat Saber)**: French when the game is in French, English otherwise. You can select **Français** or **English** at any time. Your choice is saved and applied without restarting.

**F8** shows or hides the mixer. **F9** resets the mixer's position in the current context and shows it. Neither shortcut displays the mixer during active gameplay.

## Default appearance

The mixer starts with **8 sources per page**, no total source limit, a **180 × 130** window, **0.75** scale, **3.0** text size, **85%** background opacity and a curvature radius of **200**. All windows start locked. The scene and preview windows start disabled, with the preview image hidden and its interval set to **100 ms**.

These defaults apply to new installations and settings that have not been saved yet. Updating the mod preserves your existing configuration and saved positions.

## Updates and removal

To update, close the game and replace the two mod DLLs with those from the new release for your game version. Keep **UserData/OBSMixer.json** to preserve your connection settings, language and window positions.

To temporarily disable the mod, turn it off in **General**. Its settings remain accessible from the home screen. To uninstall it, close the game and delete `Plugins/OBSMixer.dll` and `Libs/OBSMixer.Core.dll`.

## Troubleshooting

| Problem | What to check |
| --- | --- |
| OBS Mixer is missing from the mods menu | Check the game version, required mods and DLL locations, then restart the game. |
| The mixer is not visible | Enable the mod and mixer in General. Look on the home screen, during song selection or while paused. F9 restores the mixer position in that context. |
| OBS will not connect | Keep OBS open, enable its WebSocket server and check the host, port and password. Select Connect / apply after changing them. |
| A source is missing | Check whether it is in the active OBS scene, hidden or excluded by the source limit. Pin it in Sources if needed. |
| There are no scene buttons | Enable the optional scene window in General and check that OBS is connected. |
| The preview is missing or blank | Enable its window in General, open the eye and check the OBS connection. It is hidden during gameplay. |
| The language is unexpected | Choose Français or English in General, or check the language selected in Beat Saber when using Automatic. |

For help, [open an issue](https://github.com/sannier3/BEAT-SABER-OBS-MIXER/issues) with your game and mod versions, steps to reproduce the problem and the relevant Beat Saber log. **Do not share your OBS password or the configuration file containing it.**

## Credits and links

Created by **JBSAN**, using **Hardcpp's ChatPlexSDK_BS** for the VR interface.

- [Source code and releases](https://github.com/sannier3/BEAT-SABER-OBS-MIXER)
- [JBSAN website](https://jbsan.fr)
- [Related project: Streamer Assistant](https://streamerassistant.jbsan.fr)
- [ChatPlexSDK_BS by Hardcpp](https://github.com/hardcpp/ChatPlexSDK_BS)
- [BeatSaberMarkupLanguage](https://github.com/monkeymanboy/BeatSaberMarkupLanguage), [BS_Utils](https://github.com/Kylemc1413/Beat-Saber-Utils) and [obs-websocket](https://github.com/obsproject/obs-websocket)

For building from source, see [DEVELOPMENT.md](DEVELOPMENT.md). Compatibility notes and validation records are in [COMPATIBILITY.md](COMPATIBILITY.md) and [VALIDATION.md](VALIDATION.md).

## Documentation

[User guide](README.md) · [Development](DEVELOPMENT.md) · [Compatibility](COMPATIBILITY.md) · [Validation](VALIDATION.md)
