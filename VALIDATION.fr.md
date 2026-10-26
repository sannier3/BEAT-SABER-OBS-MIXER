# Validation du 26 octobre 2026 : cible Beat Saber 1.40.8

[English](VALIDATION.md) | **Français**

## Dernier résultat Windows communiqué

Le dernier build transmis par l’utilisateur compile OBSMixer.Core et OBSMixer.Tests et exécute **42 tests, tous réussis**. La compilation du plugin échoue ensuite car BeatSaber.ViewSystem.dll n’était pas référencée. Les huit diagnostics concernent cette assembly absente et les membres hérités de NavigationController.

La référence BeatSaber.ViewSystem est maintenant ajoutée avec Private=false. Le script vérifie aussi la présence de BeatSaber.ViewSystem.dll et BGLib.Polyglot.dll ; les compilations directes MSBuild ont des messages d’erreur dédiés si elles manquent. Les erreurs de conversion vers UnityEngine.Object et de membres hérités sont des conséquences de la référence manquante. La compilation du plugin corrigé doit encore être confirmée sur l’instance utilisateur.

Les sections suivantes conservent l’historique des révisions et des vérifications locales.

## Contrôles effectués

- Analyse syntaxique des fichiers C# par tree-sitter-c-sharp : réussie.
- Lecture XML des trois projets et lecture JSON du manifeste : réussies.
- Noms des requêtes OBS comparés au protocole officiel récupéré : présents.
- Révision ChatPlexSDK_BS 6.4.2 retrouvée depuis le sous-module de la release officielle BeatSaberPlus V6.4.2 compatible 1.40.8.
- Cible `.NET Framework 4.8` lue dans son projet et identité `6.4.2.0` relevée dans ses attributs d'assembly.
- Comparaison de 16 fichiers d'API ChatPlex utilisés : contenu identique entre la révision initialement inspectée et la révision cible. La signature publique `MTMainThreadInvoker.Enqueue(Action)` est présente dans la révision cible.
- Contrôles statiques du manifeste, du package de références net48, du chemin de sortie du build et des vérifications de version ChatPlex : cohérents avec la nouvelle cible.
- Les vérifications de version ajoutées à PowerShell et MSBuild n'ont pas pu être exécutées ici ; elles restent à valider lors du premier build Windows.
- Formule et vecteur d'authentification vérifiés indépendamment en Python.
- Structure de l'archive vérifiée : sources, tests et documentation présents, aucune DLL propriétaire ni configuration personnelle incluse.

## Contrôles non effectués

- Compilation du cœur C# : non effectuée. Le SDK .NET 8.0.427 a été téléchargé mais son runtime échoue au démarrage avec `Failed to create CoreCLR, HRESULT: 0x8007000E` dans cet environnement.
- Exécution des tests xUnit : non effectuée pour la même raison. Leur présence ne signifie pas qu'ils passent.
- Compilation du plugin : non effectuée ; les assemblies de Beat Saber/BSIPA/ChatPlex ne sont pas disponibles ici.
- Connexion à un OBS réel, interface Unity, FPFC et interaction VR : non effectuées.

## Tests automatisés fournis

Les fichiers de tests couvrent un vecteur d'authentification, le décodage des VU stéréo/mono, les réponses réseau reçues dans le désordre, le timeout, le rejet d'une requête, l'arrêt des requêtes en attente, un mauvais mot de passe, une reconnexion, le parcours des groupes, les cycles de scènes, la déduplication, les inputs globaux, l'exclusion des inputs sans audio, les événements de propriétés et les commandes sortantes.

Le premier résultat à obtenir sur le PC cible est la sortie complète de `scripts/build.ps1`, puis un essai Beat Saber + OBS. Des corrections de compilation ou d'intégration peuvent encore être nécessaires.


## Correction du cycle de vie du serveur de test

Le premier build Windows communiqué a compilé `OBSMixer.Core` et `OBSMixer.Tests`. Sur les six tests lancés, trois ont réussi et trois ont échoué dans `FakeObs.DisposeAsync` avec `ObjectDisposedException: IFeatureCollection has been disposed`. Le script a arrêté le build avant la compilation du plugin.

La correction retire l'appel `Abort()` sur la référence du dernier WebSocket : cette référence pouvait appartenir à un contexte HTTP déjà terminé. Un jeton d'arrêt annule désormais les lectures, envois et attentes des réponses. Le callback HTTP attend ses réponses avant de se terminer et retire sa référence de connexion dans `finally`. L'arrêt attend Kestrel, puis libère ses ressources ; les appels répétés partagent la même tâche d'arrêt.

Deux tests de régression ont été ajoutés : arrêt répété après la fin d'une connexion, et arrêt avec une connexion active dont une réponse reste en attente. La suite contient désormais huit tests. Son exécution a été tentée ici, mais .NET échoue encore au démarrage avec `Failed to create CoreCLR, HRESULT: 0x8007000E`. Les huit tests restent donc à lancer sur Windows ; aucun résultat réussi n'est revendiqué pour cette nouvelle version.


## Résultat Windows et correction du logger

Le retour de compilation suivant confirme huit tests réussis sur huit, sans échec, sous .NET 8.0.31. La compilation du plugin net48 a ensuite signalé deux erreurs CS0104 dans `Plugin.cs` : le type `Logger` était ambigu entre `IPA.Logging.Logger` et `UnityEngine.Logger`.

L'import a été remplacé par l'alias explicite `using Logger = IPA.Logging.Logger;`. La propriété du logger et le paramètre injecté par BSIPA désignent ainsi le type attendu. Le reste du plugin est inchangé. Cette correction a été vérifiée syntaxiquement ici ; la compilation complète du plugin reste à confirmer sur Windows.


## Diagnostic du premier journal de jeu

Le journal communiqué montre que BSIPA 4.3.6 détecte `OBSMixer.dll`, mais refuse ses métadonnées : `ArgumentException: 6.4.2` dans `Hive.Versioning.VersionRange`. Le mod n'est donc pas initialisé. ChatPlexSDK_BS 6.4.2 est bien présent et chargé ; les erreurs de connexion propres à son intégration OBS ne viennent pas d'OBSMixer.

Correction : la dépendance devient `"ChatPlexSDK_BS": "=6.4.2"`. L'opérateur d'égalité est confirmé par l'analyseur et les tests de `Atlas-Rhythm/Hive`, ainsi que par le convertisseur de plages de BSIPA qui construit directement un `VersionRange` à partir de cette chaîne. Le contrôle d'identité d'assembly 6.4.2.0 au build reste inchangé.

Le script transmet désormais le chemin du jeu aux tests. Un neuvième test référence le véritable `Libs/Hive.Versioning.dll` de l'instance et analyse le manifeste embarqué dans les tests, puis vérifie les versions acceptées et refusées. Les huit tests existants restent utilisables sans instance du jeu. Le nouveau test et le chargement du mod corrigé restent à vérifier sur Windows.

## Révision console compacte, menu gauche et pause

Cette révision ajoute une console à tranches verticales avec pagination, des VU basés sur la factory d'images ChatPlex, un fader vertical non linéaire et un panneau de détails pour les pistes, le monitoring et l'offset. Les réglages quittent la console et sont accessibles par un bouton BSML OBS Mixer dans le menu gauche.

L'apparence est configurable : largeur, hauteur, échelle, taille des textes, opacité des fonds et courbure. Les positions Menu/Playing existantes sont conservées. Le panneau n'est autorisé qu'en menu et en pause ; les événements de pause/reprise proviennent de BS_Utils déjà présent dans le journal communiqué. Les sauvegardes de relâchement sont protégées contre un changement de contexte en cours de manipulation.

Le modèle conserve désormais l'état de tous les inputs audio contrôlables pour permettre l'épinglage et les sources récemment audibles sans reconstruire le modèle à chaque événement VU. Les sources affichées restent filtrées par scène, épinglage, masquage et option d'audibilité récente. Les mesures sont après fader, sans seconde multiplication par le gain ; le nombre de canaux distingue mono et stéréo.

Contrôles effectués :

- Analyse syntaxique des 23 fichiers C# et lecture XML des trois projets.
- Vérification du JSON du manifeste, de sa plage `=6.4.2`, et des dépendances BSML/BS Utils.
- Inspection des API publiques ChatPlex pour ImageFactory, CImage, FlowCoordinator, IModal/CreateModal, XUI et le panneau flottant.
- Inspection de RegisterButton/UnregisterButton et du constructeur MenuButton dans BSML ; ces mêmes API sont utilisées par le SDK 6.4.2.
- Inspection des déclarations publiques songPaused/songUnpaused et de leur raccordement aux événements PauseController dans BS_Utils.
- Vérification de la structure et de l'intégrité du ZIP de sources.

Nouveaux tests fournis : courbe des faders et conversion inverse, précision près de 0 dB, visibilité menu/pause/map, transitions et activation, pagination, sources hors scène audibles et épinglées. La suite représente 20 cas sans instance du jeu, 21 avec le test de manifeste BSIPA activé par le script de build.

L'exécution de cette nouvelle suite a été tentée dans l'environnement de création ; le runtime .NET échoue encore au démarrage avec `Failed to create CoreCLR, HRESULT: 0x8007000E`. Les nouveaux tests, la compilation du plugin et le rendu Unity ne sont donc pas annoncés comme validés. Le précédent résultat utilisateur de huit tests réussis reste un résultat de la révision précédente.

À vérifier sur l'instance réelle : le nouveau bouton du menu gauche, le clavier masqué, la courbure des VU et faders, leur interaction au pointeur, la lisibilité selon les dimensions, le cadenas, la persistance distincte menu/pause, le masquage pendant la map et la reprise. Aucun nouveau patch Harmony n'est ajouté par OBSMixer.


## Limite de sources et séparation des libellés

Ajout de MaxDisplayedSources : 0 signifie Toutes, 1 à 64 limite le total après masquage, épinglage et ordre des sources. Le réglage est séparé de Sources par page. La configuration antérieure reste illimitée par défaut. Si le nombre total retenu est inférieur à la capacité d'une page, les tranches remplissent la largeur disponible.

Les repères L/R ou M sont déplacés au-dessus des barres ; la valeur en dB reste sous le fader. Les barres et le fader sont raccourcis pour réserver une marge avant cette valeur. Les bornes verticales calculées pour toutes les hauteurs configurables préservent au moins trois unités entre la fin des niveaux et le début de la valeur en dB. Le résultat visuel reste à vérifier dans Unity.

Trois tests de sélection ont été ajoutés : limite avec sources épinglées/masquées et ordre utilisateur, absence de plafond avec Toutes, et références à des sources supprimées qui ne consomment pas la limite. Cela porte la suite à 23 cas sans le jeu, 24 avec le test du manifeste BSIPA. L'analyse syntaxique et les contrôles statiques passent ; ces nouveaux tests n'ont pas été exécutés ici, le runtime .NET étant indisponible comme indiqué précédemment.


## Stabilisation de Détails, pagination explicite et faders affinés

Le panneau de détails était un enfant du panneau principal, positionné hors de son rectangle alors que la factory de panneau flottant installe un RectMask2D. Il est désormais créé à la racine, avec sa propre durée de vie et un suivi de la position, de la rotation et de l'échelle du mixeur en LateUpdate. Il n'hérite plus du masque de découpe du mixeur. La source est assignée avant l'activation de la vue. Une source indisponible ne ferme plus automatiquement le panneau, et Fermer reste utilisable en cas de déconnexion. Les règles menu/pause/map sont appliquées explicitement aux deux panneaux.

Le nombre de sources par page est désormais une capacité explicite de 1 à 8, indépendante de la largeur. Les tranches se resserrent et adaptent leurs libellés. Le ContentSizeFitter est désactivé sur les composants positionnés manuellement pour éviter qu'il réécrive leurs dimensions après le calcul de la mise en page.

Le rail du fader fait 0,65 unité ; la poignée passe de 12x4 à au plus 6x2,5 unités. La zone de saisie garde une largeur supérieure au rail, avec un fond presque invisible. L'ensemble VU/fader est centré dans chaque tranche et ses dimensions s'adaptent aux tranches étroites.

Le test de pagination existant a été actualisé ; un test de régression vérifie des largeurs de 100, 160, 184, 185 et 260 avec six ou huit sources, sans création d'une deuxième page. La suite compte désormais 24 cas sans instance du jeu et 25 avec le test du manifeste. Les vérifications syntaxiques, XML/JSON et les calculs de bornes de la mise en page passent. Les nouveaux tests et le rendu Unity ne peuvent toujours pas être exécutés ici.

À confirmer dans le jeu : ouverture et fermeture répétées de Détails, changement de source, ouverture pendant la pause puis reprise, suivi lors du déplacement/redimensionnement du mixeur, stabilité des pages en redimensionnant autour de 185, lisibilité et saisie du nouveau fader au laser.


## Détails au premier plan et fenêtre de scènes optionnelle

Détails est désormais centré sur la position du mixeur avec un décalage de 12 cm le long de sa normale avant, conserve sa rotation et son échelle et utilise un Canvas avec un ordre de rendu supérieur. Ce calcul ne dépend plus de la largeur du mixeur. Le résultat visuel et les raycasts sont à vérifier dans Unity.

Ajout d'ObsSceneStore, indépendant d'Unity : GetSceneList pour l'état initial, CurrentProgramSceneChanged pour la scène active, événements de liste/création/suppression/renommage et reconnexion. Une révision de l'état empêche une ancienne réponse GetSceneList de réécrire un événement plus récent. La sélection envoie SetCurrentProgramScene puis confirme avec GetCurrentProgramScene ; aucune surbrillance optimiste n'est appliquée.

La nouvelle fenêtre de scènes est opt-in, désactivée par défaut, avec son propre cadenas et deux positions menu/pause. Elle peut rester affichée lorsque seul le mixeur est masqué, mais ne s'affiche jamais pendant la map. Les opérations du sélecteur sont désactivées lorsqu'il n'est pas activé ; les boutons et états sont actualisés sans reconstruire les objets à chaque changement de scène active.

La configuration passe à la version 3. Le rayon par défaut et son repli pour valeur invalide valent 200. Les configurations antérieures qui avaient 140 migrent vers 200 ; les autres réglages et les positions existantes sont conservés.

Quatre tests réseau ont été ajoutés pour le sélecteur : activation optionnelle, synchronisation bidirectionnelle et modifications de liste, rejet sans fausse surbrillance, réponse de liste ancienne, reconnexion et désactivation. La suite comprend 28 cas sans instance du jeu et 29 avec le test de manifeste BSIPA. Le nouveau code a été analysé syntaxiquement ; les requêtes et événements utilisés sont présents dans le protocole officiel obs-websocket inspecté.

Les tests C# de cette révision et sa compilation Unity ne peuvent toujours pas être exécutés dans cet environnement, où CoreCLR échoue au démarrage. Aucun résultat de réussite en jeu n'est revendiqué pour cette nouvelle fenêtre. Vérifier sur l'instance : Détails centré après rotation/déplacement, fenêtre de scènes désactivée au premier lancement, changements de Program dans les deux sens, listes de plus de huit scènes, renommage/suppression/reconnexion, cadenas et sauvegarde distincte des positions menu/pause.


## Opacité des détails, ordre des scènes et crédits

Le panneau de détails utilise max(0,95 ; opacité générale) pour son fond, indépendamment de la transparence du mixeur. Le suivi et les règles de visibilité restent identiques.

Le code officiel obs-websocket (Obs_ArrayHelper.cpp, GetSceneList) attribue les indices à partir du bas et inverse le tableau avant envoi. Le parseur trie désormais sceneIndex par ordre décroissant pour retrouver le haut vers le bas du dock OBS. Le rendu existant remplit déjà les lignes de gauche à droite. Le même parseur traite GetSceneList et SceneListChanged ; sans index, le tri stable conserve l'ordre reçu. Un test réseau supplémentaire couvre dix scènes, une permutation et la scène active après réordonnancement. Total : 29 cas hors jeu, 30 avec le test optionnel du manifeste.

Un contrôleur de vue dédié occupe le panneau droit du flow des paramètres, avec des blocs Code source / Site de JBSAN / Projet associé Streamer Assistant / SDK de Hardcpp. Les liens s'ouvrent par Application.OpenURL ; les contrôleurs central et droit sont détruits avec le flow. Les signatures XUI et la position droite dans le tuple ont été vérifiées dans les sources SDK 6.4.2.

Validation locale : analyse syntaxique C#, XML et JSON. Compilation, exécution des tests et rendu VR non validés ici (CoreCLR indisponible). À vérifier au casque : détails lisibles avec opacité générale 0 et 1 ; ordre OBS à travers les pages et après réordonnancement ; crédits lisibles et complets à droite, liens ouvrant les bonnes pages.


## Trois contextes, crédits à gauche et choix de langue

Le flow utilise désormais le tuple (réglages, crédits, null), donc le panneau À propos est à gauche. MenuContextTracker identifie le contexte sélection des morceaux via les contrôleurs natifs LevelSelectionNavigationController, isActiveAndEnabled, isInViewControllerHierarchy et isInTransition. Ces membres sont utilisés dans les sources SDK 6.4.2 inspectées. Les instances sont mises en cache et redécouvertes toutes les deux secondes dans le menu ; l’état affiché est vérifié toutes les 150 ms. Les transitions animées sont ignorées jusqu’à stabilisation. Aucun objet UI n’est recherché pendant la map.

Menu/SongSelection/Playing et ScenesMenu/ScenesSongSelection/ScenesPlaying conservent trois positions distinctes. Playing reste réservé à la pause. Un changement de contexte au sein du menu sauvegarde la pose précédente avant d’appliquer la suivante. Les relâchements après changement de contexte sont rejetés et la pose du nouveau contexte est restaurée. La migration de configuration vers la version 4 copie les anciennes poses menu dans les nouvelles poses de sélection par des objets distincts. Elle ne supprime aucun ancien réglage.

Le mode auto lit BGLib.Polyglot.Localization.Instance.SelectedLanguage, dont l’utilisation a été vérifiée dans les sources SiraLocalizer (LocalizationManager). French résout fr, toute autre langue ou indisponibilité résout en. Les choix manuels fr/en ont priorité. Aucune dépendance à SiraLocalizer n’est ajoutée : seule la DLL BGLib.Polyglot du jeu est référencée. Le modèle de langue est relu toutes les 500 ms sur le thread Unity. Les révisions de langue actualisent les réglages, les crédits, le clavier, les détails et les deux fenêtres flottantes sans redémarrer. Les noms OBS restent inchangés.

Le catalogue compte 85 entrées FR/EN. Ajout de dix cas pour la résolution automatique/manuelle, d’un test de traduction aller-retour et d’un test des règles de visibilité dans le contexte SongSelection. Ces tests C# sont ajoutés mais ne sont pas déclarés exécutés : le SDK local échoue toujours au démarrage de CoreCLR (0x8007000E, code 137). Analyse syntaxique de tous les fichiers C#, XML/JSON et audit du catalogue réalisés séparément.

À vérifier sur l’instance 1.40.8 : déplacement et rotation des deux panneaux dans chacun des trois contextes, aller-retour accueil/sélection, lancement d’une map puis pause/reprise, relâchement tardif après transition, sauvegarde après relancement. Vérifier le mode auto avec Beat Saber français puis anglais/autre langue, les deux choix manuels, leur persistance et l’actualisation des onglets/crédits/détails/scènes/clavier. Le rendu final et ces chemins Unity restent à confirmer au casque.


## Documentation communautaire et package

README.md est désormais un guide utilisateur anglais et README.fr.md sa version française. Ils couvrent les prérequis, le ZIP de release distinct des archives Source code GitHub, l’installation dans la bonne instance BSManager, OBS WebSocket, le mixeur, les scènes Program, les trois positions, les langues, les mises à jour et le dépannage. DEVELOPMENT.md regroupe les instructions de compilation et de préparation de publication. Aucune validation BSMG ni publication effective n’est revendiquée.

Le script inclut les deux guides dans le package sous les noms README-OBSMixer.md et README-OBSMixer.fr.md. Il adapte leurs liens réciproques et redirige les liens techniques vers le dépôt. Lecture et écriture PowerShell sont explicitement en UTF-8. Les références de projet et les fichiers requis du script ont été comparés, les liens relatifs des guides source et les liens du package vérifiés, ainsi que la syntaxe C# et XML. Le script PowerShell n’a pas été exécuté sur Windows ici ; aucune DLL de release n’est produite dans cet environnement.


## Aperçu Program optionnel et intervalle configurable

Ajout d’ObsPreview avec captures GetCurrentProgramScene / GetSourceScreenshot, JPEG limité à 640 × 360 et qualité 60. Les noms et champs ont été vérifiés dans le protocole et les sources officiels obs-websocket présents dans le workspace. L’intervalle vaut 2000 ms par défaut, est réglable de 100 à 10000 ms par pas de 100 et ne lance jamais deux actualisations simultanées. Aucun appel n’est fait tant que l’aperçu est inactif ou OBS déconnecté.

La fenêtre est opt-in et indépendante du mixeur, avec un bouton œil graphique en haut à gauche, un cadenas et une position unique persistante commune à l’accueil, à la sélection et à la pause. Elle reste masquée pendant la map. Masquer l’image supprime la capture courante, annule l’attente et rejette les anciennes réponses. Un changement de scène Program ou une reconnexion invalide aussi l’image. Une image défectueuse affiche un message local à l’aperçu sans casser la connexion du mixeur. Les textures et sprites sont remplacés et détruits sur le thread Unity.

La configuration passe à la version 5, avec PreviewPanelEnabled=false, PreviewImageVisible=true, PreviewIntervalMs=2000 et PreviewPose. Les anciens réglages restent conservés. UnityEngine.ImageConversionModule.dll est une référence explicite vérifiée par le script et n’est pas redistribuée. Les nouveaux libellés sont traduits en français et en anglais ; les deux README communautaires décrivent l’option et l’exception de position unique.

Quatre tests réseau ajoutés : absence de captures masquées et intervalle configurable aux bornes 100/10000 et à la valeur 2000 ; absence de chevauchement et rejet d’une réponse tardive après masquage ; invalidation après changement Program et reconnexion ; récupération après capture invalide. La suite déclare désormais 46 cas avec le test de manifeste. Les 42 tests de la révision précédente ont été confirmés par le journal Windows de l’utilisateur ; les quatre nouveaux tests ne sont pas déclarés exécutés. L’essai local de dotnet test échoue toujours au démarrage CoreCLR (0x8007000E, sortie 137).

À valider au casque : ouverture et fermeture de l’œil, arrêt des captures masquées, position commune lors des trois changements de contexte, cadenas, absence pendant la map, reconnexion OBS, changements de Program, ratios vidéo différents, affichage en mode studio et intervalles court/long. Compilation du plugin et rendu Unity de cette option restent à confirmer sur l’installation cible.


## Nouveaux paramètres par défaut fournis par JBSAN

Les valeurs initiales de MixerConfig sont alignées sur le JSON transmis : cadenas mixeur/scènes/aperçu fermés, aperçu et scènes désactivés, œil fermé, intervalle 100 ms, fenêtre 180 × 130, échelle 0,75, texte 3,0, huit sources par page. Les sept poses sont reprises champ par champ, avec des littéraux float correspondant au stockage Unity. Les autres paramètres (connexion, langue, filtrage, volume, courbure, listes vides) restent ceux fournis. La version de configuration reste 5.

Les valeurs de repli numériques et les poses nulles ont été alignées. F9 réinitialise le mixeur sur la nouvelle pose de son contexte. Les anciennes positions de sélection sont copiées depuis le menu uniquement pour les configurations antérieures à la version 4 qui possèdent effectivement une pose menu enregistrée. Les fichiers existants ne sont pas écrasés et leurs réglages explicites gardent la priorité. L’intervalle initial du composant ObsPreview est aussi 100 ms ; le test de cadence conserve une vérification de l’intervalle personnalisé à 2000 ms.

Les README français/anglais et le guide développeur décrivent les nouveaux paramètres initiaux. Vérifications statiques des champs, des coordonnées, de la syntaxe C# et des fichiers de projet ; compilation Unity et tests C# toujours non exécutables ici en raison du démarrage CoreCLR indisponible. Les sections précédentes sont l’historique et peuvent décrire des valeurs par défaut remplacées depuis.

## Documentation bilingue

Les quatre documents ont désormais un fichier anglais par défaut et une version française `.fr.md`. Chacun propose un changement de langue et des liens de documentation qui restent dans la langue choisie. Les liens des README de release sont réécrits vers le bon README distribué ou le bon document technique dans le dépôt. Les vérifications statiques couvrent les huit fichiers, les liens entre traductions, le routage par langue et les destinations du package. Cette mise à jour documentaire ne modifie pas le comportement du plugin et n’ajoute aucun résultat de validation en jeu.

## Documentation

[Guide utilisateur](README.fr.md) · [Développement](DEVELOPMENT.fr.md) · [Compatibilité](COMPATIBILITY.fr.md) · [Validation](VALIDATION.fr.md)
