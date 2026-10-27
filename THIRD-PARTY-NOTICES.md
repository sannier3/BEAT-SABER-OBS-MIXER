# Third-party notices

**English** | [Français](THIRD-PARTY-NOTICES.fr.md)

The full OBSMixer 0.1.0 release package includes **ChatPlexSDK_BS 6.4.2** by **HardCPP**, without the BeatSaberPlus modules. The mod remains version 0.1.0. Its dependency metadata also allows SDK 6.4.4 and 6.4.5; its executable code is unchanged. The without-SDK package omits the SDK and preserves the installed version.

## Origin

- SDK source: [ChatPlexSDK_BS](https://github.com/hardcpp/ChatPlexSDK_BS), revision `9c6cc6e8be5cb1574d11f9ecf9c93a05a7b4e364`.
- The DLL is copied unchanged from the [official BeatSaberPlus V6.4.2 release](https://github.com/hardcpp/BeatSaberPlus/releases/tag/V6.4.2), asset `BeatSaberPlus_v6.4.2-bs-1.40.0-to-1.40.8.zip`.
- Official archive SHA-256: `c5b74f73c37536aa5623a5017df6e383d2bbec8d66a9cc2923b427365dc32955`.
- `ChatPlexSDK_BS.dll` SHA-256: `5a76011ef7d3bce5ae95d5480e3a8cab64e4c967b0486802bee8f64cea9f072f`.

## License files

The SDK's MIT license credits **Copyright (c) 2023 HardCPP**. The following full notices are distributed beside the SDK in `Plugins/`. They are also kept in the repository under `third-party/ChatPlexSDK_BS/`:

- `LICENSE.ChatPlexSDK_BS.txt`
- `LICENSE.BeatSaberMarkupLanguage.txt`
- `LICENSE.ChatCore.txt`
- `LICENSE.EnhancedStreamChat-V3.txt`
- `LICENSE.Mirror.txt`
- `LICENSE.websocket-sharp.txt`

The five component notices are preserved unchanged from the official archive. Their inclusion preserves upstream attribution; it does not mean the package installs each component as a separate plugin. BSIPA, BSML, BS_Utils and their dependencies remain separate requirements.

[User guide](README.md)
