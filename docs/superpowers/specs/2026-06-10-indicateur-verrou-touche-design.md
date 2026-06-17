# Spécification — IndicateurVerrouTouche

- **Date** : 2026-06-10
- **Statut** : Conception validée — en attente de relecture avant rédaction du plan d'implémentation
- **Nom technique** : `IndicateurVerrouTouche` — **Nom affiché** : « Indicateur Verrou Touche »
- **Emplacement projet** : `C:\Mes Projets\Autres\IndicateurVerrouTouche\`

---

## 1. Contexte & objectif

Recréer un équivalent du logiciel **CapsLockIndicator** (utilitaire Windows affichant l'état des touches de verrouillage), mais **beaucoup plus paramétrable**. L'outil tourne en arrière-plan dans la zone de notification (systray), surveille l'état des touches de verrouillage, et signale les changements via plusieurs modes d'affichage, **tous configurables touche par touche**.

Objectif central : **« tout paramétrable »**. Chaque comportement visuel/sonore est réglable indépendamment pour chaque touche surveillée, via une fenêtre de réglages **et** via un fichier JSON éditable à la main (l'utilisateur travaille avec Cursor).

## 2. Périmètre

**Dans le périmètre :**
- Surveillance de **Verr. Maj (Caps Lock)** et **Verr. Num (Num Lock)**.
- 4 modes d'indication, chacun activable/désactivable et **paramétrable par touche** :
  1. OSD fugitif (incrustation à l'écran au changement d'état)
  2. Badge permanent (indicateur persistant à l'écran)
  3. Icône systray dynamique (reflète l'état)
  4. Retour sonore (sons distincts ON/OFF)
- Fenêtre de réglages graphique avec aperçu en direct.
- Configuration en `config.json` (UI + édition manuelle), **rechargement à chaud**, import/export de profils.
- Démarrage avec Windows (optionnel), instance unique, exécution cachée dans le systray.

**Hors périmètre (extensible plus tard, sans refonte) :**
- Arrêt défil. (Scroll Lock) et Inser : **non livrés**, mais le moteur est agnostique à la touche, donc activables ultérieurement par simple ajout de configuration.
- Profils par application, raccourcis globaux, synchronisation cloud, multi-langue au-delà du français.

## 3. Choix techniques verrouillés

| Décision | Choix |
|---|---|
| Plateforme | Windows 11 (compatible 10), bureau |
| Langage / Framework | **C# / .NET 8 (LTS) + WPF** |
| Livrable | `.exe` autonome — `dotnet publish` *self-contained, single-file, win-x64* (aucun runtime requis) |
| Détection | Hook bas niveau `WH_KEYBOARD_LL` + **repli polling** (option de config) + poll de réconciliation basse fréquence |
| Systray | `System.Windows.Forms.NotifyIcon` (zéro NuGet) |
| Audio | `System.Windows.Media.MediaPlayer` (volume réglable, WAV/MP3, sans NuGet) |
| Config | `config.json` dans `%AppData%\IndicateurVerrouTouche\` |
| Git | Aucun pour le moment — travail local |

## 4. Architecture générale

Modules isolés, à responsabilité unique, communiquant via **un seul événement** émis par le moniteur clavier. Ajouter un mode = ajouter un abonné, sans toucher au reste.

```
                ┌────────────────────┐
   Win32 hook → │  KeyStateMonitor   │ ── événement KeyToggled(touche, estActivée) ──┐
                └────────────────────┘                                               │
                          ▲                                                          ▼
                ┌────────────────────┐        ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐
                │   ConfigService    │ ◀────── │OsdService│ │BadgeServ.│ │TrayServ. │ │SoundServ.│
                │  (charge/surveille │  config └──────────┘ └──────────┘ └──────────┘ └──────────┘
                │   config.json)     │             ▲ tous les services lisent la config de la touche
                └────────────────────┘
                          ▲
                ┌────────────────────┐
                │  SettingsWindow    │  (lit/écrit la config, déclenche l'aperçu)
                └────────────────────┘
                          ▲
                ┌────────────────────┐
                │  App (bootstrap)   │  instance unique, démarrage Windows, câblage
                └────────────────────┘
```

**Flux nominal :** une touche est pressée → `KeyStateMonitor` lit le nouvel état → émet `KeyToggled` → chaque service abonné consulte la config de cette touche et réagit (afficher OSD, mettre à jour badge, redessiner l'icône systray, jouer un son).

## 5. Détail des modules

> Les signatures sont indicatives (contrat de chaque unité).

### 5.1 `KeyStateMonitor`
- **Rôle** : détecter les bascules de Verr.Maj/Num et publier l'état réel.
- **Dépend de** : abstraction `IKeyboardSource` (impl. réelle Win32 ; impl. fictive pour les tests).
- **Interface** :
  ```csharp
  event EventHandler<KeyToggledEventArgs> KeyToggled; // (vk, estActivee)
  void Start();   // installe le hook ou démarre le polling selon la config
  void Stop();
  ```
- **Détail détection** :
  - **Mode hook** : `SetWindowsHookEx(WH_KEYBOARD_LL)`. Sur `WM_KEYUP` de `VK_CAPITAL`/`VK_NUMLOCK`, on poste sur le thread UI et on lit le nouvel état via `GetKeyState(vk) & 1` (le keyup garantit que l'OS a appliqué la bascule).
  - **Poll de réconciliation** (toujours actif, basse fréquence ~500 ms) : compare l'état affiché à `GetKeyState` réel pour **s'auto-corriger** si une autre application change l'état par programmation (cas que le hook peut manquer).
  - **Mode polling** (repli) : `GetKeyState` toutes les `intervallePollingMs` (déf. 30 ms), détection de transition.
  - Anti-rebond : ignore les événements identiques consécutifs.

### 5.2 `ConfigService`
- **Rôle** : source de vérité de la configuration.
- **Interface** :
  ```csharp
  AppConfig Current { get; }
  event EventHandler ConfigChanged;   // émis après rechargement à chaud
  void Load();                        // crée la config par défaut si absente
  void Save(AppConfig config);        // écrit le JSON (en supprimant la surveillance le temps de l'écriture)
  ```
- **Détail** :
  - Désérialisation typée via `System.Text.Json`. **Valeurs par défaut** appliquées pour tout champ manquant (config tolérante / évolutive via `version`).
  - **Rechargement à chaud** : `FileSystemWatcher` sur `config.json` avec anti-rebond (~300 ms) ; ignore les écritures déclenchées par l'appli elle-même.
  - **Config corrompue** : si parsing impossible → log, notification systray, conserve la dernière config valide en mémoire (n'écrase pas le fichier).
  - **Import/Export** : copie de fichiers JSON validés.

### 5.3 `OsdService`
- **Rôle** : afficher les incrustations fugitives.
- Une **fenêtre WPF transparente click-through** par OSD (réutilisée/poolée). Styles étendus : `WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`, `Topmost`, `ShowInTaskbar=false`, `AllowsTransparency=true`, `WindowStyle=None`.
- Gère : positionnement (ancrage + décalage + écran cible), animation fondu entrée/sortie, durée, file d'attente si bascules rapprochées.

### 5.4 `BadgeService`
- **Rôle** : badges permanents (une fenêtre persistante par touche).
- Même nature de fenêtre click-through que l'OSD, mais **persistante** : visible selon `afficherSi` (`on` = uniquement quand la touche est ON ; `toujours`). Position indépendante par touche.

### 5.5 `TrayService`
- **Rôle** : icône systray dynamique + menu.
- `NotifyIcon` WinForms. **Icône générée à la volée** (GDI+ : lettre + couleur ON/OFF paramétrables par touche → `Icon.FromHandle`). Une icône combinée pour les 2 touches, infobulle d'état (« Maj : ON • Num : OFF »).
- **Menu clic-droit** : Réglages… · Activer/Désactiver la surveillance · Démarrer avec Windows (case) · Quitter. **Double-clic** : ouvre les réglages.

### 5.6 `SoundService`
- **Rôle** : jouer les sons ON/OFF par touche.
- `MediaPlayer` (volume réglable). Source : chemin WAV/MP3, `"systeme"` (sons système), ou vide (silencieux). Sons par défaut fournis dans `Assets/`.

### 5.7 `SettingsWindow`
- **Rôle** : UI de configuration.
- Onglet **Général** : langue, démarrer avec Windows, mode de détection (+ intervalles), import/export.
- Un onglet **par touche** (Verr.Maj, Verr.Num) → sous-sections **OSD / Badge / Tray / Son**, chacune avec ses contrôles.
- Bouton **« Aperçu »** par section : déclenche l'affichage/son réel pour juger en direct.
- Écrit via `ConfigService.Save` → déclenche l'application immédiate.

### 5.8 `App` (bootstrap)
- **Instance unique** via `Mutex` nommé ; une 2ᵉ instance signale la 1ʳᵉ (handle d'événement nommé) pour ouvrir les réglages, puis se ferme.
- **Démarrage Windows** : clé `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (toggle, sans droits admin), exe lancé avec argument silencieux.
- Démarre **caché** (pas de fenêtre principale), câble les modules, gère l'arrêt propre (désinstalle le hook, libère le NotifyIcon).
- **DPI** : manifeste *Per-Monitor v2* pour un positionnement OSD correct en multi-écran à DPI mixtes.

## 6. Modèle de configuration (`config.json`)

Emplacement : `%AppData%\IndicateurVerrouTouche\config.json`. Exemple (Verr.Maj détaillée, Verr.Num analogue) :

```json
{
  "version": 1,
  "general": {
    "langue": "fr",
    "demarrerAvecWindows": false,
    "modeDetection": "hook",
    "intervallePollingMs": 30,
    "intervalleReconciliationMs": 500
  },
  "touches": [
    {
      "id": "capsLock",
      "vk": 20,
      "libelle": "Verr. Maj",
      "activee": true,
      "osd": {
        "actif": true,
        "texteOn": "MAJUSCULES ACTIVÉES",
        "texteOff": "Majuscules désactivées",
        "police": "Segoe UI",
        "tailleTexte": 22,
        "couleurTexteOn": "#FFFFFF",
        "couleurTexteOff": "#DDDDDD",
        "couleurFondOn": "#C0392B",
        "couleurFondOff": "#34495E",
        "opacite": 0.90,
        "coinsArrondis": 12,
        "ancrage": "basCentre",
        "decalageX": 0,
        "decalageY": 80,
        "ecranCible": "actif",
        "dureeMs": 1500,
        "fonduEntreeMs": 150,
        "fonduSortieMs": 300,
        "afficherIcone": true,
        "icone": "Assets/caps.png"
      },
      "badge": {
        "actif": false,
        "afficherSi": "on",
        "texteOn": "⇪",
        "police": "Segoe UI",
        "tailleTexte": 18,
        "couleurTexteOn": "#FFFFFF",
        "couleurFondOn": "#C0392B",
        "opacite": 0.85,
        "coinsArrondis": 8,
        "ancrage": "basDroite",
        "decalageX": 16,
        "decalageY": 16,
        "ecranCible": "principal"
      },
      "tray": {
        "actif": true,
        "lettre": "A",
        "couleurOn": "#C0392B",
        "couleurOff": "#7F8C8D"
      },
      "son": {
        "actif": false,
        "sonOn": "Assets/on.wav",
        "sonOff": "Assets/off.wav",
        "volume": 0.80
      }
    },
    {
      "id": "numLock",
      "vk": 144,
      "libelle": "Verr. Num",
      "activee": true,
      "osd": { "actif": true, "texteOn": "PAVÉ NUM. ACTIVÉ", "texteOff": "Pavé num. désactivé", "ancrage": "basCentre", "decalageY": 80, "couleurFondOn": "#27AE60" },
      "badge": { "actif": false },
      "tray": { "actif": true, "lettre": "N", "couleurOn": "#27AE60", "couleurOff": "#7F8C8D" },
      "son": { "actif": false }
    }
  ]
}
```

**Valeurs d'ancrage** : `hautGauche, hautCentre, hautDroite, milieuGauche, centre, milieuDroite, basGauche, basCentre, basDroite`.
**`ecranCible`** : `"principal"` | `"actif"` | `"curseur"` | index numérique d'écran.
**Tolérance** : tout champ omis prend sa valeur par défaut → l'édition manuelle reste simple (on ne met que ce qu'on veut changer).

## 7. Gestion des erreurs & cas limites
- **Config corrompue / illisible** → garde la dernière config valide, notifie, ne réécrase pas le fichier.
- **Multi-écran / DPI mixtes** → positionnement calculé par écran cible (Per-Monitor v2).
- **Bascules très rapprochées** → file d'attente OSD / réutilisation de fenêtre, pas d'empilement incontrôlé.
- **Changement d'état programmatique par une autre appli** → rattrapé par le poll de réconciliation.
- **Échec d'installation du hook** → bascule automatique en mode polling + notification.
- **Asset son/icône manquant** → ignore proprement (pas de son / icône par défaut), log.
- **Arrêt** → désinstallation du hook, libération `NotifyIcon`, fermeture des fenêtres.

## 8. Stratégie de test
- **Projet `IndicateurVerrouTouche.Tests` (xUnit).**
- `ConfigService` : chargement, application des valeurs par défaut, robustesse au JSON partiel/corrompu, round-trip sauvegarde/relecture, migration `version`.
- `KeyStateMonitor` : logique de transition/anti-rebond testée via `IKeyboardSource` fictif (on injecte des événements synthétiques, sans Win32).
- Les couches Win32/WPF (rendu OSD, hook réel) sont couvertes par une **checklist de test manuel** documentée dans le README (positions, multi-écran, fondu, son, démarrage Windows, instance unique).

## 9. Packaging / structure de dossiers

```
IndicateurVerrouTouche/
  IndicateurVerrouTouche.sln
  src/IndicateurVerrouTouche/
    IndicateurVerrouTouche.csproj      (WPF, net8.0-windows, UseWindowsForms=true)
    App.xaml(.cs)
    Core/        KeyStateMonitor, IKeyboardSource, Win32KeyboardSource, ConfigService, modèle AppConfig
    Services/    OsdService, BadgeService, TrayService, SoundService
    UI/          SettingsWindow + contrôles d'aperçu
    Assets/      sons & icônes par défaut
  tests/IndicateurVerrouTouche.Tests/
  build/         publish.ps1 (publish self-contained single-file win-x64)
  config.example.json
  docs/superpowers/specs/2026-06-10-indicateur-verrou-touche-design.md
  README.md
```

Build : `dotnet publish src/IndicateurVerrouTouche -c Release -r win-x64 --self-contained -p:PublishSingleFile=true` → un seul `.exe`.

## 10. Évolutions futures (hors périmètre actuel)
- Activation de Arrêt défil. / Inser (déjà supporté par le moteur, à exposer en config).
- Profils par application, raccourcis globaux, thèmes prédéfinis livrés.
