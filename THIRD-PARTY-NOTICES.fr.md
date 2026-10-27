# Mentions des composants tiers

[English](THIRD-PARTY-NOTICES.md) | **Français**

Le package complet de la release OBSMixer 0.1.0 inclut **ChatPlexSDK_BS 6.4.2** de **HardCPP**, sans les modules BeatSaberPlus. Le mod reste en version 0.1.0. Ses métadonnées de dépendance autorisent aussi les SDK 6.4.4 et 6.4.5 ; son code exécutable reste inchangé. Le package sans SDK omet le SDK et conserve la version installée.

## Provenance

- Sources du SDK : [ChatPlexSDK_BS](https://github.com/hardcpp/ChatPlexSDK_BS), révision `9c6cc6e8be5cb1574d11f9ecf9c93a05a7b4e364`.
- La DLL est reprise sans modification depuis la [release officielle BeatSaberPlus V6.4.2](https://github.com/hardcpp/BeatSaberPlus/releases/tag/V6.4.2), fichier `BeatSaberPlus_v6.4.2-bs-1.40.0-to-1.40.8.zip`.
- SHA-256 de l’archive officielle : `c5b74f73c37536aa5623a5017df6e383d2bbec8d66a9cc2923b427365dc32955`.
- SHA-256 de `ChatPlexSDK_BS.dll` : `5a76011ef7d3bce5ae95d5480e3a8cab64e4c967b0486802bee8f64cea9f072f`.

## Fichiers de licence

La licence MIT du SDK indique **Copyright (c) 2023 HardCPP**. Les mentions intégrales suivantes sont distribuées à côté du SDK dans `Plugins/`. Elles sont également conservées dans le dépôt sous `third-party/ChatPlexSDK_BS/` :

- `LICENSE.ChatPlexSDK_BS.txt`
- `LICENSE.BeatSaberMarkupLanguage.txt`
- `LICENSE.ChatCore.txt`
- `LICENSE.EnhancedStreamChat-V3.txt`
- `LICENSE.Mirror.txt`
- `LICENSE.websocket-sharp.txt`

Les cinq mentions des composants sont conservées sans modification depuis l’archive officielle. Leur présence conserve les crédits d’origine ; elle ne signifie pas que chaque composant est installé comme un plugin distinct. BSIPA, BSML, BS_Utils et leurs dépendances restent à installer séparément.

[Guide utilisateur](README.fr.md)
