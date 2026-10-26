# Développement et préparation des releases

[English](DEVELOPMENT.md) | **Français**

Ce document concerne la compilation. Le guide d’installation pour les utilisateurs est [README.fr.md](README.fr.md).

## Environnement

- Windows, PowerShell et SDK .NET 8.
- Instance modifiée de Beat Saber PC 1.40.8 avec les dépendances du manifeste installées.
- ChatPlexSDK_BS 6.4.2, identité d’assembly 6.4.2.0.

Le plugin cible .NET Framework 4.8, le cœur netstandard2.0 et les tests .NET 8. Les assemblies du jeu servent de références de compilation et ne sont pas redistribuées. BeatSaber.ViewSystem.dll fournit la classe de base des contrôleurs de sélection ; BGLib.Polyglot.dll fournit la langue du jeu.

## Compiler et installer localement

Depuis le dossier du projet, en adaptant le chemin vers l’instance :

```powershell
.\scripts\build.ps1 -BeatSaberDir 'C:\Games\BeatSaber\1.40.8' -Install
```

Le script vérifie les fichiers requis, exécute les tests, compile le plugin et génère **dist/OBSMixer-0.1.0.zip**. Avec `-Install`, il copie les deux DLL dans l’instance. Retirez `-Install` pour produire uniquement le package. Le fichier de configuration existant n’est pas remplacé.

Si Windows demande une confirmation de sécurité, contrôlez le script avant de l’exécuter. `-SkipTests` existe pour un diagnostic local ; une release doit être préparée avec les tests activés.

## Tests

Sans instance du jeu :

```powershell
dotnet test .\tests\OBSMixer.Tests\OBSMixer.Tests.csproj -c Release
```

Avec l’analyseur de manifeste de l’instance BSIPA :

```powershell
dotnet test .\tests\OBSMixer.Tests\OBSMixer.Tests.csproj -c Release '-p:BeatSaberDir=C:\Games\BeatSaber\1.40.8'
```

Le script de build active automatiquement ce dernier test. Les tests utilisent un faux serveur OBS et ne nécessitent pas de lancer OBS. Ils ne remplacent pas les essais au casque.

## Package de release

Le ZIP généré contient :

```text
Plugins/OBSMixer.dll
Libs/OBSMixer.Core.dll
README-OBSMixer.md
README-OBSMixer.fr.md
```

Les guides sont renommés au packaging et leurs liens sont adaptés au contenu distribué. Les liens techniques pointent vers la même langue dans le dépôt. Ne publiez pas un ZIP de sources comme archive installable. Ne recopiez pas l’ensemble du dossier bin : les DLL Unity, du jeu et des dépendances doivent rester celles de l’installation de l’utilisateur. Le package ne doit contenir aucun fichier UserData/OBSMixer.json ni mot de passe.

Avant publication, confirmer une compilation réussie, puis vérifier en jeu : connexion et reconnexion OBS, volumes/mute/détails, scènes, visibilité uniquement accueil/sélection/pause, trois positions pour le mixeur et les scènes, cadenas, langues automatique et manuelles. Vérifier aussi la position commune de l’aperçu et son bouton œil. Le journal [VALIDATION.fr.md](VALIDATION.fr.md) consigne les vérifications et les limites connues.

Joindre **dist/OBSMixer-0.1.0.zip** à la release GitHub et indiquer Beat Saber **1.40.8**, ChatPlexSDK_BS **6.4.2** et les autres dépendances dans ses notes. L’annonce destinée au BSMG doit renvoyer vers cette release. Cela ne constitue pas une validation ni une approbation par le BSMG.

## Organisation du projet

- `src/OBSMixer.Core` : connexion obs-websocket v5, état des sources et scènes, sélection audio, pagination et traductions.
- `src/OBSMixer.Plugin` : intégration BSIPA, fenêtres ChatPlex, réglages BSML, positions et suivi menu/pause.
- `tests/OBSMixer.Tests` : tests réseau, états, présentation, langues et manifeste.
- [COMPATIBILITY.fr.md](COMPATIBILITY.fr.md) : versions et API retenues.

Le réglage de langue accepte `auto`, `fr` et `en`. La configuration version 5 conserve les anciennes positions Menu/Playing et initialise les positions SongSelection par des copies indépendantes. Les réglages sont enregistrés dans UserData/OBSMixer.json.


## Aperçu Program

ObsPreview partage la connexion OBS du mod. Chaque actualisation lit GetCurrentProgramScene puis GetSourceScreenshot (JPEG, bornes 640 × 360, qualité 60). PreviewIntervalMs vaut 100 par défaut et est borné à 100–10000. Le slider progresse par 100 ms. Une seule actualisation peut être en cours. La réponse est invalidée par le masquage, un changement de Program, la déconnexion ou la destruction. Les images sont décodées sur le thread Unity ; sprites et textures remplacés sont détruits.

Le rendu utilise UnityEngine.ImageConversionModule.dll, une référence du jeu non redistribuée. PreviewPanelEnabled est faux par défaut, PreviewImageVisible est faux et mémorise l’état de l’œil. PreviewPose est unique et partagée par les scènes génériques Menu et Playing. La visibilité utilise les mêmes règles accueil/sélection/pause que le reste du mod, indépendamment de PanelVisible.


## Valeurs initiales fournies par JBSAN

Les sept poses initiales (mixeur et scènes pour accueil/sélection/pause, plus aperçu partagé) reprennent les coordonnées fournies par JBSAN. Les trois cadenas sont fermés par défaut. Fenêtre du mixeur : 180 × 130, échelle 0,75, texte 3,0, huit sources par page et aucune limite totale. Aperçu désactivé et image masquée, intervalle 100 ms. Le raccourci F9 restaure la pose initiale du mixeur correspondant au contexte courant. Les valeurs de repli des réglages invalides et des poses nulles utilisent ces mêmes paramètres. La configuration reste en version 5 et aucun fichier utilisateur existant n’est remplacé.

## Documentation

[Guide utilisateur](README.fr.md) · [Développement](DEVELOPMENT.fr.md) · [Compatibilité](COMPATIBILITY.fr.md) · [Validation](VALIDATION.fr.md)
