# Indicateur Verrou Touche

[![Licence](https://img.shields.io/badge/licence-Propri%C3%A9taire-red)](LICENSE)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![Plateforme](https://img.shields.io/badge/plateforme-Windows-0078D6?logo=windows&logoColor=white)
![Dépendances](https://img.shields.io/badge/d%C3%A9pendances-aucune-brightgreen)

Utilitaire Windows (zone de notification) qui surveille **Verr. Maj** et **Verr. Num** et
signale leurs changements d'état via 4 modes, chacun **entièrement paramétrable par touche** :

1. **OSD fugitif** — incrustation à l'écran au changement d'état (fondu entrée/sortie, position, couleurs…).
2. **Badge permanent** — indicateur persistant affiché selon l'état (`on`) ou en permanence (`toujours`).
3. **Icône systray dynamique** — reflète l'état en temps réel (lettre + couleur ON/OFF par touche).
4. **Retour sonore** — sons distincts ON/OFF (fichier WAV/MP3, son système, ou silence).

L'outil tourne caché dans le systray, en **instance unique**, avec **démarrage Windows** optionnel.

## Pile technique

- C# / .NET 8 (`net8.0-windows`), **WPF** + WinForms (`NotifyIcon`).
- Détection : hook bas niveau `WH_KEYBOARD_LL` + poll de réconciliation (rattrape les changements programmatiques) ; repli **polling** configurable.
- Configuration `config.json` typée (`System.Text.Json`), **rechargement à chaud** (`FileSystemWatcher`), import/export de profils.
- Aucune dépendance NuGet.

## Lancer en développement

```powershell
dotnet run --project src/IndicateurVerrouTouche
```

L'application démarre sans fenêtre : une icône « A | N » apparaît dans la zone de notification.
**Double-clic** sur l'icône (ou **clic droit → Réglages…**) ouvre la fenêtre de configuration.

## Publier l'exécutable

```powershell
./build/publish.ps1
```

Produit `dist/IndicateurVerrouTouche.exe` — **un seul fichier léger (~230 Ko)**, *framework-dependent*
win-x64. **Prérequis sur le poste cible : le .NET 8 Desktop Runtime** (windowsdesktop) doit être
installé (à déployer une fois par poste, p. ex. via GPO/Intune).

## Installation, auto-mise-à-jour et désinstallation

- **Distribution** : l'exe est déposé sur le partage réseau (cf. ci-dessous). Quand un utilisateur
  lance l'exe **depuis le réseau**, l'app se **copie automatiquement** dans
  `%LocalAppData%\IndicateurVerrouTouche\` et redémarre depuis cette copie locale (les mises à jour
  remplacent la copie locale, jamais le fichier partagé commun).
- **Désinstallation** : l'app s'inscrit dans **« Applications et fonctionnalités »** (bouton
  Désinstaller), répond à l'argument `--desinstaller`, et propose **« Désinstaller… »** dans le menu
  de l'icône. La désinstallation retire l'install locale, la configuration, le démarrage Windows,
  l'entrée registre et les icônes épinglées.

## Versions & mises à jour

**Versionnage** — format `Année.Mois.Jour` + lettre (ex. `2026.06.11a`). La lettre s'incrémente
à chaque version d'une même journée (`a→b→…→z→aa→…`) et repart à `a` chaque nouveau jour.
Avant de publier, exécuter `./build/bump-version.ps1` : il calcule et écrit la version suivante
dans le `.csproj` (champ `InformationalVersion`).

**Mises à jour automatiques** — l'application lit un manifeste `maj.json` dans un **dossier de mises
à jour détecté automatiquement** : le dossier réseau d'où l'exe a été distribué et lancé (mémorisé
lors de l'auto-installation locale), ou à défaut le dossier d'où l'exe est exécuté. Les mises à jour
fonctionnent donc **quel que soit l'emplacement d'où l'application a été lancée** (partage réseau
d'entreprise, dossier local extrait…). Ce dossier reste **paramétrable** (réglages → Général) pour
forcer une autre source. Pour publier une mise à jour :

1. `./build/bump-version.ps1` puis `./build/publish.ps1`.
2. Copier `dist/IndicateurVerrouTouche.exe` dans le dossier réseau.
3. Y déposer / mettre à jour `maj.json` (modèle : `build/maj.example.json`) :
   ```json
   { "version": "2026.06.12a", "fichier": "IndicateurVerrouTouche.exe", "notes": "Nouveautés…", "forcer": false }
   ```

L'app vérifie **au démarrage** (silencieux) **et périodiquement** (toutes les 30 min par défaut,
`general.intervalleVerifMajMinutes`), ainsi que via le bouton « Vérifier maintenant » des réglages.
Si une version plus récente est annoncée, elle télécharge l'exe depuis le partage, se ferme,
remplace l'ancien exécutable puis se relance.

**Mise à jour forcée** — mettre `"forcer": true` dans `maj.json` : la mise à jour est alors
installée **automatiquement, sans confirmation et sans attendre de redémarrage** (les instances déjà
lancées l'appliquent au prochain cycle de vérification, ≤ 30 min). Utile pour imposer un correctif.

## Configuration

Fichier : `%AppData%\IndicateurVerrouTouche\config.json` (créé au premier lancement).

- Éditable **à la main** (Cursor, etc.) : tout champ omis prend sa valeur par défaut, et le
  fichier est **rechargé à chaud** dès la sauvegarde.
- Éditable via la **fenêtre de réglages** (un onglet par touche → sous-sections OSD / Badge / Systray / Son,
  avec sélecteur de couleur et bouton **Aperçu OSD**).
- `config.example.json` (racine du dépôt) liste **tous les champs** avec leurs valeurs par défaut.

Valeurs d'ancrage : `hautGauche, hautCentre, hautDroite, milieuGauche, centre, milieuDroite, basGauche, basCentre, basDroite`.
`ecranCible` : `principal` | `actif` | `curseur` | index numérique d'écran.

## Tests

```powershell
dotnet test
```

Couvre la configuration (`ConfigService` : défauts, JSON partiel, aller-retour) et la logique de
détection (`KeyStateMonitor` via source clavier fictive) ainsi que le calcul de position (`PositionneurEcran`).
Les couches Win32/WPF (rendu OSD, hook réel, multi-écran) sont validées par la checklist manuelle ci-dessous.

## Checklist de test manuel

- [ ] Icône systray « A | N » visible, suit Verr. Maj / Verr. Num **en temps réel**.
- [ ] OSD fugitif s'affiche puis disparaît en fondu ; **click-through** (la souris passe au travers).
- [ ] Badge permanent apparaît / disparaît selon `afficherSi` (`on` vs `toujours`).
- [ ] Son ON/OFF joué quand activé (`"sonOn": "systeme"` pour un test rapide).
- [ ] Réglages : modification + **Aperçu OSD** + **Enregistrer** appliquent les changements immédiatement.
- [ ] **Rechargement à chaud** : éditer `config.json` à la main (ex. une couleur tray) → mise à jour sans redémarrer.
- [ ] **Démarrer avec Windows** : la valeur sous `HKCU\…\Run` est créée / supprimée selon la case.
- [ ] **Instance unique** : un 2ᵉ lancement ouvre les réglages de la 1ʳᵉ instance puis se ferme.
- [ ] **Multi-écran** : OSD positionné sur le bon écran selon `ecranCible`.

## Structure

```
src/IndicateurVerrouTouche/
  App.xaml(.cs)            bootstrap, instance unique, câblage des modules
  Core/                    AppConfig, ConfigService, KeyStateMonitor, Win32KeyboardSource, ColorHelper…
  Services/                TrayService, OsdService, BadgeService, SoundService,
                           ClickThroughWindow, PositionneurEcran, DemarrageWindows
  UI/                      SettingsWindow + Controls/ (OSD, Badge, Tray, Son)
tests/IndicateurVerrouTouche.Tests/
build/publish.ps1
config.example.json
```
