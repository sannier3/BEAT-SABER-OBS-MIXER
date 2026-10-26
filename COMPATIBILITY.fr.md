# Cible Beat Saber 1.40.8

[English](COMPATIBILITY.md) | **Français**

## Ensemble retenu

| Élément | Cible |
| --- | --- |
| Beat Saber | PC Windows 1.40.8 |
| BSIPA | ^4.3.0 |
| ChatPlexSDK_BS | 6.4.2, identité d'assembly 6.4.2.0 |
| Plugin OBSMixer | net48 |
| Cœur OBSMixer.Core | netstandard2.0 |
| Tests | net8.0 |

Le SDK retenu vient du sous-module `ChatPlexSDK-BS` de la release [BeatSaberPlus V6.4.2](https://github.com/hardcpp/BeatSaberPlus/releases/tag/V6.4.2). L'asset officiel se nomme `BeatSaberPlus_v6.4.2-bs-1.40.0-to-1.40.8.zip` et les notes indiquent la compatibilité 1.40.7/1.40.8.

- BeatSaberPlus : `acb85e62987f1dc183b78ace5c70754b610d7a16`.
- ChatPlexSDK_BS : `9c6cc6e8be5cb1574d11f9ecf9c93a05a7b4e364`.
- ChatPlexSDK générique : `b36daf8cb10f211d38017931f3f5c7d2532061c7`.

Le manifeste de cette révision du SDK mentionne encore 1.40.0. Son rattachement à la release compatible 1.40.8 permet d'identifier le bon ensemble. Le projet du SDK cible explicitement .NET Framework 4.8 ; le plugin a été aligné sur cette cible.

## API vérifiées

| Partie du mod | API de la révision cible |
| --- | --- |
| Création du panneau | `UISystem.FloatingPanelFactory.Create(string, Transform)` |
| Déplacement et verrouillage | `CFloatingPanel.SetAllowMovement`, `GetAllowMovement`, `SetLockIcon` |
| Position selon la scène | `SetSceneTransform`, `OnSceneRelease` |
| Création/destruction des vues | `UISystem.CreateViewController<T>`, `DestroyUI` |
| Cycle de vie UI | `ViewController<T>.OnViewCreation`, `OnViewDestruction` |
| Contrôles | `XUISlider`, `XUIText`, `XUIPrimaryButton`, `XUIHLayout`, `XUIVLayout` |
| Défilement | `XUIVScrollView`, `CVScrollView.Container`, `UpdateContentSize` |
| API de module ChatPlex inspectées pendant le développement | `ModuleBase<T>`, `GetSettingsViewControllersImplementation` |
| Transfert vers Unity | `MTMainThreadInvoker.Enqueue(Action)` |

Seize fichiers couvrant les interfaces de panneau, vues, contrôles XUI, factories et module sont identiques à ceux de la révision initialement inspectée. Les changements du dispatcher dans les révisions plus récentes n'empêchent pas l'utilisation de la signature `Enqueue(Action)` déjà présente dans la révision cible. Le protocole OBS reste indépendant de ChatPlex et n'a pas été modifié.

## Changements apportés

- Le manifeste OBSMixer cible 1.40.8 et exige ChatPlexSDK_BS 6.4.2 exactement, avec la plage Hive `=6.4.2`.
- Le plugin et son package de références ciblent net48.
- PowerShell vérifie la version de la DLL ChatPlex avant de lancer les tests et le build.
- Le projet MSBuild vérifie aussi cette version pour les compilations directes depuis un IDE.
- Le chemin de collecte des DLL est passé à `bin/Release/net48`.
- Le README, les exemples de commandes et le rapport de validation ciblent la nouvelle instance.

Les gardes de version n'installent ni ne remplacent ChatPlex automatiquement. Si l'instance contient une autre version, le build s'arrête avec une erreur explicite.

## Limite de validation

Cette adaptation est vérifiée au niveau des sources et des métadonnées. Elle n'est pas encore une certification de fonctionnement en jeu. Il reste à compiler avec les DLL de l'installation 1.40.8 puis à tester le mod avec OBS, en FPFC et au casque. Le script ne déduit pas la version réelle du jeu depuis ses fichiers Unity : il faut lui fournir le chemin de la bonne instance.

## Révision console compacte

Deux dépendances explicites sont désormais utilisées en plus du SDK : `BeatSaberMarkupLanguage` (`^1.12.5`) pour le bouton de configuration du menu gauche, et `BS Utils` (`^1.14.2`, DLL `BS_Utils.dll`) pour les événements de pause/reprise. Ces deux mods figuraient déjà dans le journal utilisateur. L'absence de leur DLL arrête le script avant compilation ; le script ne modifie pas ces dépendances.

Les nouveaux VU/faders passent par `UISystem.ImageFactory` ; la version Beat Saber du SDK configure cette factory avec `HMUI.ImageView`. Le clavier masqué utilise une classe `IModal` du mod et les API publiques `CreateModal`, `ShowModal` et `CloseModal`, sans réflexion sur le clavier interne du SDK.

Le code ne référence plus directement un PauseController et ne recherche pas d'objets de pause chaque frame. Il s'abonne aux événements publics BS_Utils. La politique d'affichage est isolée dans le cœur et testée sans Unity, mais le timing des événements et le résultat visuel restent à valider dans Beat Saber.

## Fenêtre des scènes et détails au premier plan

La nouvelle fenêtre optionnelle utilise le même client obs-websocket v5 et les abonnements Scenes/Config déjà présents. GetSceneList, SetCurrentProgramScene et GetCurrentProgramScene ont été vérifiés dans le protocole officiel inspecté. La sélection cible Program, même si OBS est en mode studio. Aucun pont web ni dépendance supplémentaire n'est ajouté.

Les vues flottantes utilisent les factories publiques du SDK. Détails possède son propre Canvas avec un ordre de rendu supérieur et une pose centrée devant le mixeur, indépendante de sa largeur. La fenêtre des scènes dispose d'un cadenas et de trois poses accueil/sélection/pause propres ; elle reste désactivée par défaut. Le rayon commun par défaut est 200 et la configuration actuelle est en version 5.


La version avec langues FR/EN référence également `Beat Saber_Data/Managed/BGLib.Polyglot.dll` pour lire la langue sélectionnée dans Beat Saber. La DLL appartient au jeu et ne doit pas être copiée dans le dossier Plugins. Les trois contextes de position utilisent le menu générique ChatPlex, la hiérarchie native de sélection des morceaux et les événements de pause BS_Utils.


`Beat Saber_Data/Managed/BeatSaber.ViewSystem.dll` est référencée explicitement pour résoudre NavigationController et ses membres hérités. C’est une assembly du jeu, vérifiée avant compilation et exclue du package distribué (`Private=false`). Le journal Windows fourni confirme que les 42 tests passent avant cette correction ; la compilation du plugin corrigé reste à vérifier sur cette installation.


L’aperçu optionnel utilise GetCurrentProgramScene et GetSourceScreenshot de obs-websocket v5. Le décodage JPEG/PNG utilise UnityEngine.ImageConversionModule.dll, fournie par Beat Saber et exclue du package. Il montre Program, y compris en mode studio, avec un intervalle 100–10000 ms (100 par défaut). La configuration actuelle est en version 5.

## Documentation

[Guide utilisateur](README.fr.md) · [Développement](DEVELOPMENT.fr.md) · [Compatibilité](COMPATIBILITY.fr.md) · [Validation](VALIDATION.fr.md)
