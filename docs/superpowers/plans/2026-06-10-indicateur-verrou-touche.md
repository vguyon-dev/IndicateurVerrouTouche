# Plan d'implémentation — IndicateurVerrouTouche

> **Pour les agents d'implémentation :** SOUS-COMPÉTENCE REQUISE — utiliser `superpowers:subagent-driven-development` (recommandé) ou `superpowers:executing-plans` pour implémenter ce plan tâche par tâche. Les étapes utilisent la syntaxe case à cocher (`- [ ]`).

**Objectif :** Un utilitaire Windows de bureau (systray) qui surveille Verr. Maj et Verr. Num et signale leurs changements d'état via 4 modes (OSD fugitif, badge permanent, icône systray dynamique, son), chacun entièrement paramétrable par touche.

**Architecture :** App WPF .NET 8. Un `KeyStateMonitor` détecte les bascules (hook bas niveau + réconciliation) et émet un événement `KeyToggled`. Des services abonnés (`Osd`, `Badge`, `Tray`, `Sound`) réagissent selon le `config.json` géré par `ConfigService` (rechargement à chaud). Une `SettingsWindow` édite la config. Modules isolés communiquant par un seul événement.

**Pile technique :** C# / .NET 8 (`net8.0-windows`), WPF + WinForms (`NotifyIcon`), `System.Text.Json`, `MediaPlayer`, P/Invoke Win32, tests xUnit.

## Conventions de code & qualité (à respecter dans CHAQUE tâche)
- **Commentaires en français**, axés sur l'**intention** (le « pourquoi »), pas la paraphrase du code. Doc XML `///` sur les types et membres publics.
- **Fichiers courts et focalisés** : une responsabilité par fichier. Si un fichier dépasse ~250 lignes, c'est un signal de découpage.
- **Optimisation équilibrée (YAGNI)** : pas de micro-optimisation prématurée, mais pas de gaspillage évident — libérer les handles natifs (icônes GDI, hook clavier), réutiliser les fenêtres OSD, éviter tout polling superflu.
- **Nommage** : PascalCase pour types/membres publics, `_camelCase` pour champs privés. Noms en français acceptés pour le métier (ex. `EstActivee`).
- **Pas de Git pour l'instant** : les étapes de fin de tâche sont des **points de contrôle** (build + exécution/observation), pas des commits.
- **Revue de code** : à la fin de chaque phase, exécuter le point de revue indiqué. Revue finale obligatoire via `superpowers:requesting-code-review`.

---

## Structure des fichiers

```
IndicateurVerrouTouche/
  IndicateurVerrouTouche.sln
  src/IndicateurVerrouTouche/
    IndicateurVerrouTouche.csproj
    app.manifest
    App.xaml / App.xaml.cs                 ← bootstrap, instance unique, câblage
    Core/
      AppConfig.cs                          ← modèle de config (DTO + défauts)
      ColorHelper.cs                        ← #RRGGBB ↔ Color/Brush
      ConfigService.cs                      ← charge/sauve/surveille config.json
      IKeyboardSource.cs                    ← abstraction source clavier
      KeyStateMonitor.cs                    ← détection des bascules (testable)
      Win32KeyboardSource.cs                ← hook LL + GetKeyState
      KeyToggledEventArgs.cs
    Services/
      TrayService.cs                        ← icône systray dynamique + menu
      ClickThroughWindow.cs                 ← fenêtre WPF transparente non cliquable
      PositionneurEcran.cs                  ← calcul position (ancrage/écran/DPI)
      OsdService.cs                         ← incrustations fugitives
      BadgeService.cs                       ← badges permanents
      SoundService.cs                       ← lecture des sons
      DemarrageWindows.cs                   ← clé de registre Run
    UI/
      SettingsWindow.xaml(.cs)
      Controls/OsdSettingsControl.xaml(.cs)
      Controls/BadgeSettingsControl.xaml(.cs)
      Controls/TraySettingsControl.xaml(.cs)
      Controls/SoundSettingsControl.xaml(.cs)
      HexColorToBrushConverter.cs
    Assets/  (on.wav, off.wav, icône .ico)
  tests/IndicateurVerrouTouche.Tests/
    IndicateurVerrouTouche.Tests.csproj
    ConfigServiceTests.cs
    KeyStateMonitorTests.cs
    PositionneurEcranTests.cs
    FakeKeyboardSource.cs
  build/publish.ps1
  config.example.json
  README.md
```

---

## PHASE 0 — Squelette & outillage

### Task 1 : Solution, projets et build à blanc

**Files:**
- Create: `IndicateurVerrouTouche.sln`, `src/IndicateurVerrouTouche/IndicateurVerrouTouche.csproj`, `src/IndicateurVerrouTouche/app.manifest`, `tests/IndicateurVerrouTouche.Tests/IndicateurVerrouTouche.Tests.csproj`

- [ ] **Step 1 : Créer la solution et les projets**

Run (depuis `C:\Mes Projets\Autres\IndicateurVerrouTouche`) :
```bash
dotnet new sln -n IndicateurVerrouTouche
dotnet new wpf -o src/IndicateurVerrouTouche -n IndicateurVerrouTouche
dotnet new xunit -o tests/IndicateurVerrouTouche.Tests -n IndicateurVerrouTouche.Tests
dotnet sln add src/IndicateurVerrouTouche tests/IndicateurVerrouTouche.Tests
dotnet add tests/IndicateurVerrouTouche.Tests reference src/IndicateurVerrouTouche
```

- [ ] **Step 2 : Configurer le `.csproj` principal**

Remplacer `src/IndicateurVerrouTouche/IndicateurVerrouTouche.csproj` par :
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <AssemblyName>IndicateurVerrouTouche</AssemblyName>
    <RootNamespace>IndicateurVerrouTouche</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <None Update="Assets\**\*"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></None>
  </ItemGroup>
</Project>
```

- [ ] **Step 3 : Manifest DPI Per-Monitor v2**

Créer `src/IndicateurVerrouTouche/app.manifest` :
```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="IndicateurVerrouTouche.app"/>
  <!-- DPI par moniteur v2 : positionnement OSD correct en multi-écran à DPI mixtes -->
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/PM</dpiAware>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2, PerMonitor</dpiAwareness>
    </windowsSettings>
  </application>
</assembly>
```

- [ ] **Step 4 : Point de contrôle — build à blanc**

Run : `dotnet build`
Attendu : `Build succeeded`, 0 erreur.

---

## PHASE 1 — Configuration (TDD)

### Task 2 : Modèle de config et défauts

**Files:**
- Create: `src/IndicateurVerrouTouche/Core/AppConfig.cs`, `src/IndicateurVerrouTouche/Core/ColorHelper.cs`

- [ ] **Step 1 : Écrire le modèle `AppConfig`**

Créer `Core/AppConfig.cs` (DTO de sérialisation ; les valeurs par défaut des propriétés servent de valeurs par défaut quand un champ est absent du JSON) :
```csharp
namespace IndicateurVerrouTouche.Core;

/// <summary>Racine de la configuration sérialisée dans config.json.</summary>
public sealed class AppConfig
{
    public int Version { get; set; } = 1;
    public GeneralConfig General { get; set; } = new();
    public List<ToucheConfig> Touches { get; set; } = new();

    /// <summary>Config livrée par défaut au premier lancement : Verr. Maj + Verr. Num.</summary>
    public static AppConfig ParDefaut() => new()
    {
        Touches =
        {
            new ToucheConfig
            {
                Id = "capsLock", Vk = 0x14, Libelle = "Verr. Maj", Activee = true,
                Osd = new OsdConfig { Actif = true, TexteOn = "MAJUSCULES ACTIVÉES", TexteOff = "Majuscules désactivées", CouleurFondOn = "#C0392B" },
                Tray = new TrayConfig { Actif = true, Lettre = "A", CouleurOn = "#C0392B" }
            },
            new ToucheConfig
            {
                Id = "numLock", Vk = 0x90, Libelle = "Verr. Num", Activee = true,
                Osd = new OsdConfig { Actif = true, TexteOn = "PAVÉ NUM. ACTIVÉ", TexteOff = "Pavé num. désactivé", CouleurFondOn = "#27AE60" },
                Tray = new TrayConfig { Actif = true, Lettre = "N", CouleurOn = "#27AE60" }
            }
        }
    };
}

public sealed class GeneralConfig
{
    public string Langue { get; set; } = "fr";
    public bool DemarrerAvecWindows { get; set; } = false;
    public string ModeDetection { get; set; } = "hook";  // "hook" | "polling"
    public int IntervallePollingMs { get; set; } = 30;
    public int IntervalleReconciliationMs { get; set; } = 500;
}

public sealed class ToucheConfig
{
    public string Id { get; set; } = "";
    public int Vk { get; set; }
    public string Libelle { get; set; } = "";
    public bool Activee { get; set; } = true;
    public OsdConfig Osd { get; set; } = new();
    public BadgeConfig Badge { get; set; } = new();
    public TrayConfig Tray { get; set; } = new();
    public SonConfig Son { get; set; } = new();
}

/// <summary>Style commun aux fenêtres affichées (OSD et badge).</summary>
public abstract class StyleAffichageConfig
{
    public bool Actif { get; set; }
    public string Police { get; set; } = "Segoe UI";
    public double TailleTexte { get; set; } = 22;
    public string CouleurTexteOn { get; set; } = "#FFFFFF";
    public string CouleurTexteOff { get; set; } = "#DDDDDD";
    public string CouleurFondOn { get; set; } = "#C0392B";
    public string CouleurFondOff { get; set; } = "#34495E";
    public double Opacite { get; set; } = 0.9;
    public double CoinsArrondis { get; set; } = 12;
    public string Ancrage { get; set; } = "basCentre";   // voir PositionneurEcran.Ancrages
    public double DecalageX { get; set; } = 0;
    public double DecalageY { get; set; } = 80;
    public string EcranCible { get; set; } = "actif";     // "principal" | "actif" | "curseur" | index
}

public sealed class OsdConfig : StyleAffichageConfig
{
    public string TexteOn { get; set; } = "ACTIVÉ";
    public string TexteOff { get; set; } = "désactivé";
    public int DureeMs { get; set; } = 1500;
    public int FonduEntreeMs { get; set; } = 150;
    public int FonduSortieMs { get; set; } = 300;
}

public sealed class BadgeConfig : StyleAffichageConfig
{
    public string AfficherSi { get; set; } = "on";  // "on" | "toujours"
    public string TexteOn { get; set; } = "⇪";
    public string TexteOff { get; set; } = "";
}

public sealed class TrayConfig
{
    public bool Actif { get; set; } = true;
    public string Lettre { get; set; } = "?";
    public string CouleurOn { get; set; } = "#C0392B";
    public string CouleurOff { get; set; } = "#7F8C8D";
}

public sealed class SonConfig
{
    public bool Actif { get; set; } = false;
    public string SonOn { get; set; } = "";    // chemin | "systeme" | ""
    public string SonOff { get; set; } = "";
    public double Volume { get; set; } = 0.8;
}
```

- [ ] **Step 2 : Helper couleur**

Créer `Core/ColorHelper.cs` :
```csharp
using System.Windows.Media;

namespace IndicateurVerrouTouche.Core;

/// <summary>Conversion robuste entre chaînes hex (#RGB/#RRGGBB/#AARRGGBB) et couleurs WPF.</summary>
public static class ColorHelper
{
    public static Color VersColor(string? hex, Color repli = default)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex ?? "")!; }
        catch { return repli; }  // hex invalide → couleur de repli, jamais d'exception remontée
    }

    public static SolidColorBrush VersBrush(string? hex, Color repli = default)
    {
        var b = new SolidColorBrush(VersColor(hex, repli));
        b.Freeze();  // figé = partageable entre threads et plus performant
        return b;
    }
}
```

- [ ] **Step 3 : Point de contrôle**

Run : `dotnet build`
Attendu : `Build succeeded`.

---

### Task 3 : `ConfigService.Load` (TDD)

**Files:**
- Create: `src/IndicateurVerrouTouche/Core/ConfigService.cs`
- Test: `tests/IndicateurVerrouTouche.Tests/ConfigServiceTests.cs`

- [ ] **Step 1 : Test qui échoue — création des défauts si fichier absent**

Créer `tests/IndicateurVerrouTouche.Tests/ConfigServiceTests.cs` :
```csharp
using System.IO;
using IndicateurVerrouTouche.Core;
using Xunit;

public class ConfigServiceTests
{
    private static string DossierTemp()
    {
        var d = Path.Combine(Path.GetTempPath(), "ivt_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }

    [Fact]
    public void Load_CreeLesDefauts_SiFichierAbsent()
    {
        var svc = new ConfigService(Path.Combine(DossierTemp(), "config.json"));
        svc.Load();
        Assert.Equal(2, svc.Current.Touches.Count);
        Assert.Contains(svc.Current.Touches, t => t.Id == "capsLock");
        Assert.True(File.Exists(svc.CheminFichier)); // le fichier de défaut est écrit
    }

    [Fact]
    public void Load_AppliqueLesDefauts_SurJsonPartiel()
    {
        var dir = DossierTemp();
        var chemin = Path.Combine(dir, "config.json");
        File.WriteAllText(chemin, "{ \"touches\": [ { \"id\": \"capsLock\", \"vk\": 20 } ] }");
        var svc = new ConfigService(chemin);
        svc.Load();
        var caps = Assert.Single(svc.Current.Touches);
        Assert.Equal("Segoe UI", caps.Osd.Police);   // champ absent → valeur par défaut
        Assert.True(caps.Activee);
    }
}
```

- [ ] **Step 2 : Lancer le test pour vérifier l'échec**

Run : `dotnet test --filter ConfigServiceTests`
Attendu : ÉCHEC de compilation (`ConfigService` n'existe pas encore).

- [ ] **Step 3 : Implémentation minimale de `ConfigService` (Load + Save)**

Créer `Core/ConfigService.cs` :
```csharp
using System.IO;
using System.Text.Json;

namespace IndicateurVerrouTouche.Core;

/// <summary>
/// Source de vérité de la configuration. Charge/sauve config.json et notifie
/// les changements (rechargement à chaud ajouté en Task 5).
/// </summary>
public sealed class ConfigService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping // garde les accents lisibles
    };

    public string CheminFichier { get; }
    public AppConfig Current { get; private set; } = new();
    public event EventHandler? ConfigChanged;

    public ConfigService(string cheminFichier) => CheminFichier = cheminFichier;

    /// <summary>Chemin par défaut : %AppData%\IndicateurVerrouTouche\config.json.</summary>
    public static string CheminParDefaut() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "IndicateurVerrouTouche", "config.json");

    public void Load()
    {
        if (!File.Exists(CheminFichier))
        {
            Current = AppConfig.ParDefaut();
            Save(Current);   // matérialise un fichier éditable dès le premier lancement
            return;
        }
        // Une config illisible ne doit jamais planter l'appli : on garde la dernière valide.
        try
        {
            var json = File.ReadAllText(CheminFichier);
            Current = JsonSerializer.Deserialize<AppConfig>(json, Options) ?? AppConfig.ParDefaut();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Config illisible, valeur précédente conservée : {ex.Message}");
        }
    }

    public void Save(AppConfig config)
    {
        Current = config;
        Directory.CreateDirectory(Path.GetDirectoryName(CheminFichier)!);
        File.WriteAllText(CheminFichier, JsonSerializer.Serialize(config, Options));
    }

    internal void DeclencherChangement() => ConfigChanged?.Invoke(this, EventArgs.Empty);
}
```

- [ ] **Step 4 : Vérifier que les tests passent**

Run : `dotnet test --filter ConfigServiceTests`
Attendu : PASS (2 tests).

- [ ] **Step 5 : Point de contrôle** — `dotnet build` OK.

---

### Task 4 : `ConfigService.Save` aller-retour (TDD)

**Files:**
- Test: `tests/IndicateurVerrouTouche.Tests/ConfigServiceTests.cs` (ajout)

- [ ] **Step 1 : Test aller-retour**

Ajouter dans `ConfigServiceTests` :
```csharp
[Fact]
public void Save_Puis_Load_PreserveLesValeurs()
{
    var chemin = Path.Combine(DossierTemp(), "config.json");
    var svc = new ConfigService(chemin);
    var cfg = AppConfig.ParDefaut();
    cfg.Touches[0].Osd.TexteOn = "TEST_MAJ";
    cfg.General.DemarrerAvecWindows = true;
    svc.Save(cfg);

    var relu = new ConfigService(chemin);
    relu.Load();
    Assert.Equal("TEST_MAJ", relu.Current.Touches[0].Osd.TexteOn);
    Assert.True(relu.Current.General.DemarrerAvecWindows);
}
```

- [ ] **Step 2 : Lancer** — `dotnet test --filter ConfigServiceTests` → Attendu : PASS (3 tests) (aucun code à ajouter, valide le round-trip).

- [ ] **Step 3 : Point de contrôle** — build OK.

---

### Task 5 : Rechargement à chaud (FileSystemWatcher)

**Files:**
- Modify: `src/IndicateurVerrouTouche/Core/ConfigService.cs`

- [ ] **Step 1 : Ajouter la surveillance fichier**

Modifier `ConfigService.cs` — ajouter les champs et méthodes (le drapeau `_ecritureInterne` évite de réagir à nos propres sauvegardes) :
```csharp
    private FileSystemWatcher? _watcher;
    private bool _ecritureInterne;
    private DateTime _dernierEvt = DateTime.MinValue;

    /// <summary>Active la détection des modifications externes du fichier (édition manuelle).</summary>
    public void DemarrerSurveillance()
    {
        var dossier = Path.GetDirectoryName(CheminFichier)!;
        Directory.CreateDirectory(dossier);
        _watcher = new FileSystemWatcher(dossier, Path.GetFileName(CheminFichier))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };
        _watcher.Changed += (_, _) =>
        {
            if (_ecritureInterne) return;
            // Anti-rebond : les éditeurs écrivent souvent en plusieurs salves.
            var maintenant = DateTime.UtcNow;
            if ((maintenant - _dernierEvt).TotalMilliseconds < 300) return;
            _dernierEvt = maintenant;
            try { System.Threading.Thread.Sleep(50); Load(); DeclencherChangement(); }
            catch { /* fichier transitoirement verrouillé : on ignore, prochain évènement rejouera */ }
        };
    }
```
Et entourer l'écriture dans `Save` du drapeau :
```csharp
    public void Save(AppConfig config)
    {
        Current = config;
        Directory.CreateDirectory(Path.GetDirectoryName(CheminFichier)!);
        _ecritureInterne = true;
        try { File.WriteAllText(CheminFichier, JsonSerializer.Serialize(config, Options)); }
        finally { _ecritureInterne = false; }
    }
```

- [ ] **Step 2 : Point de contrôle** — `dotnet build` OK. (Test manuel du hot-reload effectué en Phase 8 quand l'appli tourne.)

### ✅ Point de revue de code — Phase 1
Relire `AppConfig`, `ConfigService`, `ColorHelper` : noms clairs, commentaires d'intention présents, aucune fuite, gestion d'erreur tolérante. Corriger avant de continuer.

---

## PHASE 2 — Moniteur clavier (TDD)

### Task 6 : `KeyStateMonitor` + abstraction source (TDD)

**Files:**
- Create: `src/IndicateurVerrouTouche/Core/KeyToggledEventArgs.cs`, `Core/IKeyboardSource.cs`, `Core/KeyStateMonitor.cs`
- Test: `tests/IndicateurVerrouTouche.Tests/FakeKeyboardSource.cs`, `tests/IndicateurVerrouTouche.Tests/KeyStateMonitorTests.cs`

- [ ] **Step 1 : Contrats**

Créer `Core/KeyToggledEventArgs.cs` :
```csharp
namespace IndicateurVerrouTouche.Core;

/// <summary>Émis quand l'état verrouillé d'une touche surveillée change.</summary>
public sealed class KeyToggledEventArgs(int vk, bool estActivee) : EventArgs
{
    public int Vk { get; } = vk;
    public bool EstActivee { get; } = estActivee;
}
```
Créer `Core/IKeyboardSource.cs` :
```csharp
namespace IndicateurVerrouTouche.Core;

/// <summary>
/// Abstraction de la source clavier : permet de tester KeyStateMonitor sans Win32.
/// L'implémentation réelle (Win32KeyboardSource) pose un hook ; la fictive (tests) simule.
/// </summary>
public interface IKeyboardSource : IDisposable
{
    /// <summary>Déclenché avec le vk d'une touche surveillée venant d'être pressée (mode hook).</summary>
    event Action<int>? ToucheAppuyee;
    /// <summary>État verrouillé courant de la touche (bit bascule de l'OS).</summary>
    bool EstVerrouillee(int vk);
    void Start();
    void Stop();
}
```

- [ ] **Step 2 : Moniteur (logique pure, testable)**

Créer `Core/KeyStateMonitor.cs` :
```csharp
namespace IndicateurVerrouTouche.Core;

/// <summary>
/// Maintient l'état connu de chaque touche surveillée et émet KeyToggled à chaque
/// changement. Sans timer ni Win32 : la périodicité (réconciliation/polling) est
/// pilotée depuis l'extérieur via Refresh/RefreshAll, ce qui rend la classe testable.
/// </summary>
public sealed class KeyStateMonitor
{
    private readonly IKeyboardSource _source;
    private readonly Dictionary<int, bool> _etats = new();

    public event EventHandler<KeyToggledEventArgs>? KeyToggled;

    public KeyStateMonitor(IKeyboardSource source, IEnumerable<int> vksSurveilles)
    {
        _source = source;
        foreach (var vk in vksSurveilles) _etats[vk] = source.EstVerrouillee(vk);
        _source.ToucheAppuyee += Refresh;   // rafraîchissement instantané en mode hook
    }

    /// <summary>Relit l'état réel d'une touche et émet l'évènement si changement.</summary>
    public void Refresh(int vk)
    {
        if (!_etats.ContainsKey(vk)) return;
        bool maintenant = _source.EstVerrouillee(vk);
        if (maintenant == _etats[vk]) return;
        _etats[vk] = maintenant;
        KeyToggled?.Invoke(this, new KeyToggledEventArgs(vk, maintenant));
    }

    /// <summary>Réconciliation : relit toutes les touches (rattrape les changements programmatiques).</summary>
    public void RefreshAll()
    {
        foreach (var vk in _etats.Keys.ToList()) Refresh(vk);
    }

    public bool EtatDe(int vk) => _etats.TryGetValue(vk, out var e) && e;
    public IReadOnlyCollection<int> VksSurveilles => _etats.Keys;

    public void Start() => _source.Start();
    public void Stop() => _source.Stop();
}
```

- [ ] **Step 3 : Source fictive pour les tests**

Créer `tests/IndicateurVerrouTouche.Tests/FakeKeyboardSource.cs` :
```csharp
using IndicateurVerrouTouche.Core;

/// <summary>Source clavier simulée : on pilote les états et on déclenche les appuis à la main.</summary>
public sealed class FakeKeyboardSource : IKeyboardSource
{
    private readonly Dictionary<int, bool> _etats = new();
    public event Action<int>? ToucheAppuyee;

    public bool EstVerrouillee(int vk) => _etats.TryGetValue(vk, out var e) && e;
    public void DefinirEtat(int vk, bool valeur) => _etats[vk] = valeur;
    /// <summary>Simule un appui : bascule l'état puis notifie (comme le hook réel).</summary>
    public void SimulerAppui(int vk) { _etats[vk] = !EstVerrouillee(vk); ToucheAppuyee?.Invoke(vk); }
    public void Start() { } public void Stop() { } public void Dispose() { }
}
```

- [ ] **Step 4 : Tests du moniteur**

Créer `tests/IndicateurVerrouTouche.Tests/KeyStateMonitorTests.cs` :
```csharp
using IndicateurVerrouTouche.Core;
using Xunit;

public class KeyStateMonitorTests
{
    private const int Vk = 0x14;

    [Fact]
    public void Appui_EmetKeyToggled_AvecLeNouvelEtat()
    {
        var src = new FakeKeyboardSource();
        var mon = new KeyStateMonitor(src, new[] { Vk });
        KeyToggledEventArgs? recu = null;
        mon.KeyToggled += (_, e) => recu = e;

        src.SimulerAppui(Vk);

        Assert.NotNull(recu);
        Assert.Equal(Vk, recu!.Vk);
        Assert.True(recu.EstActivee);
    }

    [Fact]
    public void Refresh_SansChangement_NEmetRien()
    {
        var src = new FakeKeyboardSource();
        var mon = new KeyStateMonitor(src, new[] { Vk });
        int n = 0; mon.KeyToggled += (_, _) => n++;

        mon.Refresh(Vk);   // état inchangé
        Assert.Equal(0, n);
    }

    [Fact]
    public void RefreshAll_RattrapeUnChangementExterne()
    {
        var src = new FakeKeyboardSource();
        var mon = new KeyStateMonitor(src, new[] { Vk });
        bool? dernier = null; mon.KeyToggled += (_, e) => dernier = e.EstActivee;

        src.DefinirEtat(Vk, true);  // changement « programmatique » sans appui
        mon.RefreshAll();

        Assert.True(dernier);
    }
}
```

- [ ] **Step 5 : Lancer** — `dotnet test --filter KeyStateMonitorTests` → Attendu : PASS (3 tests).

- [ ] **Step 6 : Point de contrôle** — build OK.

---

### Task 7 : `Win32KeyboardSource` (hook bas niveau)

**Files:**
- Create: `src/IndicateurVerrouTouche/Core/Win32KeyboardSource.cs`

- [ ] **Step 1 : Implémentation du hook + lecture d'état**

Créer `Core/Win32KeyboardSource.cs` :
```csharp
using System.Runtime.InteropServices;

namespace IndicateurVerrouTouche.Core;

/// <summary>
/// Source clavier réelle. En mode "hook", pose un WH_KEYBOARD_LL et notifie sur le
/// relâchement (WM_KEYUP) des touches surveillées — le keyup garantit que l'OS a déjà
/// appliqué la bascule, donc EstVerrouillee renvoie le bon nouvel état. En mode "polling",
/// aucun hook : c'est le timer externe (App) qui appelle RefreshAll.
/// </summary>
public sealed class Win32KeyboardSource : IKeyboardSource
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYUP = 0x0105;

    private readonly bool _utiliserHook;
    private readonly HashSet<int> _surveilles;
    private readonly LowLevelKeyboardProc _proc;  // gardé en champ : sinon le GC le collecte → crash du hook
    private IntPtr _hook = IntPtr.Zero;

    public event Action<int>? ToucheAppuyee;

    public Win32KeyboardSource(bool utiliserHook, IEnumerable<int> vksSurveilles)
    {
        _utiliserHook = utiliserHook;
        _surveilles = new HashSet<int>(vksSurveilles);
        _proc = HookProc;
    }

    public bool EstVerrouillee(int vk) => (GetKeyState(vk) & 1) == 1;  // bit de poids faible = bascule

    public void Start()
    {
        if (!_utiliserHook || _hook != IntPtr.Zero) return;
        // hMod peut être IntPtr.Zero pour un hook LL global dans le process courant.
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, IntPtr.Zero, 0);
    }

    public void Stop()
    {
        if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
    }

    public void Dispose() => Stop();

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == WM_KEYUP || wParam == WM_SYSKEYUP))
        {
            int vk = Marshal.ReadInt32(lParam);  // KBDLLHOOKSTRUCT.vkCode est le 1er champ
            if (_surveilles.Contains(vk)) ToucheAppuyee?.Invoke(vk);
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);  // ne jamais bloquer la chaîne de hooks
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);
}
```

- [ ] **Step 2 : Point de contrôle (manuel)** — `dotnet build` OK. (Test fonctionnel réel du hook fait en Task 9 quand l'icône systray reflète l'état.)

### ✅ Point de revue de code — Phase 2
Vérifier : délégué `_proc` bien conservé en champ (sinon crash GC), `CallNextHookEx` toujours appelé, hook libéré dans `Stop/Dispose`, logique de bascule (`& 1`) correcte.

---

## PHASE 3 — Coquille applicative & systray (premier exécutable lançable)

### Task 8 : Bootstrap App + instance unique

**Files:**
- Modify: `src/IndicateurVerrouTouche/App.xaml`, `App.xaml.cs`
- Delete: `src/IndicateurVerrouTouche/MainWindow.xaml(.cs)` (pas de fenêtre principale)

- [ ] **Step 1 : Retirer la fenêtre principale**

Supprimer `MainWindow.xaml` et `MainWindow.xaml.cs`. Modifier `App.xaml` pour retirer `StartupUri` :
```xml
<Application x:Class="IndicateurVerrouTouche.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources/>
</Application>
```

- [ ] **Step 2 : Bootstrap + Mutex d'instance unique**

Remplacer `App.xaml.cs` (le câblage des services sera complété aux tâches suivantes) :
```csharp
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using IndicateurVerrouTouche.Core;

namespace IndicateurVerrouTouche;

public partial class App : Application
{
    private const string NomMutex = "IndicateurVerrouTouche_SingleInstance";
    private const string NomEvenement = "IndicateurVerrouTouche_ShowSettings";
    private Mutex? _mutex;
    private EventWaitHandle? _evtAffichage;

    private ConfigService _config = null!;
    private KeyStateMonitor _monitor = null!;
    private Win32KeyboardSource _source = null!;
    private DispatcherTimer _timer = null!;
    // Services ajoutés aux tâches suivantes : Tray, Osd, Badge, Sound.

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;  // l'appli vit dans le systray, pas de fenêtre

        // Instance unique : si déjà lancée, on demande l'ouverture des réglages puis on quitte.
        _mutex = new Mutex(true, NomMutex, out bool premier);
        if (!premier)
        {
            EventWaitHandle.OpenExisting(NomEvenement).Set();
            Shutdown();
            return;
        }
        _evtAffichage = new EventWaitHandle(false, EventResetMode.AutoReset, NomEvenement);
        SurveillerDemandeAffichage();

        // Configuration
        _config = new ConfigService(ConfigService.CheminParDefaut());
        _config.Load();
        _config.DemarrerSurveillance();

        // Moniteur clavier
        var vks = VksActifs();
        bool hook = _config.Current.General.ModeDetection != "polling";
        _source = new Win32KeyboardSource(hook, vks);
        _monitor = new KeyStateMonitor(_source, vks);
        _monitor.Start();

        // Timer : réconciliation (mode hook) ou polling complet.
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(hook
                ? _config.Current.General.IntervalleReconciliationMs
                : _config.Current.General.IntervallePollingMs)
        };
        _timer.Tick += (_, _) => _monitor.RefreshAll();
        _timer.Start();

        // TrayService câblé en Task 9.
    }

    private IEnumerable<int> VksActifs() =>
        _config.Current.Touches.Where(t => t.Activee).Select(t => t.Vk).Distinct().ToList();

    /// <summary>Ouvre les réglages quand une 2ᵉ instance le demande (thread d'attente → dispatcher UI).</summary>
    private void SurveillerDemandeAffichage()
    {
        var th = new Thread(() =>
        {
            while (true) { _evtAffichage!.WaitOne(); Dispatcher.Invoke(OuvrirReglages); }
        }) { IsBackground = true };
        th.Start();
    }

    private void OuvrirReglages() { /* implémenté en Task 18 */ }

    protected override void OnExit(ExitEventArgs e)
    {
        _timer?.Stop();
        _source?.Dispose();      // libère le hook
        _mutex?.ReleaseMutex();
        base.OnExit(e);
    }
}
```

- [ ] **Step 3 : Point de contrôle** — `dotnet run --project src/IndicateurVerrouTouche`. Attendu : l'appli démarre **sans fenêtre** et ne se ferme pas (processus visible dans le Gestionnaire des tâches). La fermer via le Gestionnaire des tâches pour l'instant.

---

### Task 9 : `TrayService` — icône systray dynamique

**Files:**
- Create: `src/IndicateurVerrouTouche/Services/TrayService.cs`
- Modify: `src/IndicateurVerrouTouche/App.xaml.cs`

- [ ] **Step 1 : Service systray**

Créer `Services/TrayService.cs` :
```csharp
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using IndicateurVerrouTouche.Core;

namespace IndicateurVerrouTouche.Services;

/// <summary>
/// Icône de la zone de notification. L'icône est redessinée à chaque changement d'état
/// (lettre + couleur ON/OFF par touche), avec libération systématique du handle GDI.
/// </summary>
public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _notify = new();
    private readonly ConfigService _config;
    private readonly KeyStateMonitor _monitor;
    private IntPtr _hiconActuel = IntPtr.Zero;

    public event EventHandler? OuvrirReglagesDemande;
    public event EventHandler? QuitterDemande;

    public TrayService(ConfigService config, KeyStateMonitor monitor)
    {
        _config = config;
        _monitor = monitor;
        _notify.Visible = true;
        _notify.DoubleClick += (_, _) => OuvrirReglagesDemande?.Invoke(this, EventArgs.Empty);
        ConstruireMenu();
        Redessiner();
    }

    /// <summary>Reconstruit le menu contextuel (appelé au démarrage et après changement de config).</summary>
    public void ConstruireMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Réglages…", null, (_, _) => OuvrirReglagesDemande?.Invoke(this, EventArgs.Empty));
        var demarrage = new ToolStripMenuItem("Démarrer avec Windows")
        { Checked = _config.Current.General.DemarrerAvecWindows, CheckOnClick = true };
        demarrage.CheckedChanged += (_, _) =>
        {
            _config.Current.General.DemarrerAvecWindows = demarrage.Checked;
            DemarrageWindows.Definir(demarrage.Checked);
            _config.Save(_config.Current);
        };
        menu.Items.Add(demarrage);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quitter", null, (_, _) => QuitterDemande?.Invoke(this, EventArgs.Empty));
        _notify.ContextMenuStrip = menu;
    }

    /// <summary>Redessine l'icône combinée (toutes les touches systray actives) + l'infobulle.</summary>
    public void Redessiner()
    {
        var touches = _config.Current.Touches.Where(t => t.Activee && t.Tray.Actif).ToList();
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            // Découpe horizontale : une zone par touche (1 ou 2 en pratique).
            int n = Math.Max(touches.Count, 1);
            for (int i = 0; i < touches.Count; i++)
            {
                var t = touches[i];
                bool on = _monitor.EtatDe(t.Vk);
                var couleur = ToDrawing(on ? t.Tray.CouleurOn : t.Tray.CouleurOff);
                var zone = new RectangleF(i * 32f / n, 0, 32f / n, 32);
                using var b = new SolidBrush(couleur);
                g.FillRectangle(b, zone);
                using var f = new Font("Segoe UI", 32f / n * 0.5f, FontStyle.Bold, GraphicsUnit.Pixel);
                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(t.Tray.Lettre, f, Brushes.White, zone, sf);
            }
        }
        // Remplacement de l'icône avec libération de l'ancien handle (sinon fuite GDI).
        IntPtr nouveau = bmp.GetHicon();
        _notify.Icon = Icon.FromHandle(nouveau);
        if (_hiconActuel != IntPtr.Zero) DestroyIcon(_hiconActuel);
        _hiconActuel = nouveau;

        _notify.Text = touches.Count == 0
            ? "Indicateur Verrou Touche"
            : string.Join("  ", touches.Select(t => $"{t.Libelle} : {(_monitor.EtatDe(t.Vk) ? "ON" : "OFF")}"));
    }

    private static Color ToDrawing(string hex)
    {
        var c = ColorHelper.VersColor(hex, System.Windows.Media.Colors.Gray);
        return Color.FromArgb(c.A == 0 ? (byte)255 : c.A, c.R, c.G, c.B);
    }

    public void Dispose()
    {
        _notify.Visible = false;
        if (_hiconActuel != IntPtr.Zero) DestroyIcon(_hiconActuel);
        _notify.Dispose();
    }

    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);
}
```

- [ ] **Step 2 : Câbler le tray dans App**

Dans `App.xaml.cs` : ajouter le champ `private TrayService _tray = null!;`, puis à la fin de `OnStartup` (après le timer) :
```csharp
        _tray = new TrayService(_config, _monitor);
        _tray.OuvrirReglagesDemande += (_, _) => OuvrirReglages();
        _tray.QuitterDemande += (_, _) => Shutdown();
        _monitor.KeyToggled += (_, _) => _tray.Redessiner();  // l'icône suit l'état en temps réel
```
Et dans `OnExit`, avant `base.OnExit` : `_tray?.Dispose();`

- [ ] **Step 3 : Point de contrôle (test fonctionnel réel)**

Run : `dotnet run --project src/IndicateurVerrouTouche`
Attendu : une **icône apparaît dans la zone de notification** affichant « A | N ». Appuyer sur **Verr. Maj** → la moitié « A » change de couleur **instantanément** ; idem **Verr. Num** → « N ». L'infobulle indique l'état. Le hook clavier est ainsi validé. Clic droit → menu (Réglages… inactif pour l'instant, Quitter fonctionne).

---

### Task 10 : Démarrage avec Windows

**Files:**
- Create: `src/IndicateurVerrouTouche/Services/DemarrageWindows.cs`

- [ ] **Step 1 : Helper registre**

Créer `Services/DemarrageWindows.cs` :
```csharp
using Microsoft.Win32;

namespace IndicateurVerrouTouche.Services;

/// <summary>Gère la clé Run de l'utilisateur courant (démarrage automatique, sans droits admin).</summary>
public static class DemarrageWindows
{
    private const string Cle = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Nom = "IndicateurVerrouTouche";

    public static void Definir(bool actif)
    {
        using var k = Registry.CurrentUser.OpenSubKey(Cle, writable: true)!;
        if (actif)
        {
            var exe = Environment.ProcessPath!;  // chemin de l'exe en cours
            k.SetValue(Nom, $"\"{exe}\"");
        }
        else if (k.GetValue(Nom) != null)
        {
            k.DeleteValue(Nom);
        }
    }

    public static bool EstActif()
    {
        using var k = Registry.CurrentUser.OpenSubKey(Cle);
        return k?.GetValue(Nom) != null;
    }
}
```

- [ ] **Step 2 : Point de contrôle** — lancer l'appli, cocher « Démarrer avec Windows » dans le menu, vérifier dans `regedit` que la valeur existe sous `HKCU\...\Run`, décocher → valeur supprimée.

### ✅ Point de revue de code — Phase 3
Vérifier : libération handle GDI (`DestroyIcon`) sans fuite, `NotifyIcon` correctement disposé, thread d'attente en arrière-plan, aucun chemin qui laisse le hook installé après sortie.

---

## PHASE 4 — OSD fugitif

### Task 11 : Fenêtre transparente non cliquable

**Files:**
- Create: `src/IndicateurVerrouTouche/Services/ClickThroughWindow.cs`

- [ ] **Step 1 : Fenêtre de base**

Créer `Services/ClickThroughWindow.cs` :
```csharp
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace IndicateurVerrouTouche.Services;

/// <summary>
/// Fenêtre WPF servant de support aux OSD/badges : sans bordure, transparente, toujours
/// au-dessus, et surtout « click-through » (invisible à la souris) et sans vol de focus.
/// </summary>
public abstract class ClickThroughWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_TOOLWINDOW = 0x80;   // hors barre des tâches / Alt+Tab
    private const int WS_EX_NOACTIVATE = 0x8000000;

    protected ClickThroughWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        ShowActivated = false;
        SizeToContent = SizeToContent.WidthAndHeight;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
    }

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
```

- [ ] **Step 2 : Point de contrôle** — build OK.

---

### Task 12 : Positionnement écran (ancrage / écran / DPI)

**Files:**
- Create: `src/IndicateurVerrouTouche/Services/PositionneurEcran.cs`
- Test: `tests/IndicateurVerrouTouche.Tests/PositionneurEcranTests.cs`

- [ ] **Step 1 : Test du calcul d'ancrage (logique pure, en pixels)**

Créer `tests/IndicateurVerrouTouche.Tests/PositionneurEcranTests.cs` :
```csharp
using System.Drawing;
using IndicateurVerrouTouche.Services;
using Xunit;

public class PositionneurEcranTests
{
    // Zone de travail 1000x800 à l'origine (0,0), fenêtre 200x100, décalage 10/20.
    private static readonly Rectangle Zone = new(0, 0, 1000, 800);

    [Fact]
    public void BasCentre_CentreEnXEtCalleEnBas()
    {
        var p = PositionneurEcran.CalculerCoin("basCentre", Zone, 200, 100, 0, 20);
        Assert.Equal(400, p.X);          // (1000-200)/2
        Assert.Equal(680, p.Y);          // 800-100-20
    }

    [Fact]
    public void HautGauche_RespecteLesDecalages()
    {
        var p = PositionneurEcran.CalculerCoin("hautGauche", Zone, 200, 100, 10, 20);
        Assert.Equal(10, p.X);
        Assert.Equal(20, p.Y);
    }
}
```

- [ ] **Step 2 : Lancer** — `dotnet test --filter PositionneurEcranTests` → ÉCHEC (classe absente).

- [ ] **Step 3 : Implémentation**

Créer `Services/PositionneurEcran.cs` :
```csharp
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;

namespace IndicateurVerrouTouche.Services;

/// <summary>
/// Place une fenêtre WPF sur l'écran cible selon un ancrage. Le placement final est fait
/// en pixels physiques via SetWindowPos pour être fiable en multi-écran à DPI mixtes
/// (on contourne ainsi les conversions DIP délicates de WPF).
/// </summary>
public static class PositionneurEcran
{
    public static readonly string[] Ancrages =
    {
        "hautGauche","hautCentre","hautDroite","milieuGauche","centre","milieuDroite","basGauche","basCentre","basDroite"
    };

    /// <summary>Calcule le coin haut-gauche (px) de la fenêtre dans la zone donnée. Logique pure et testable.</summary>
    public static Point CalculerCoin(string ancrage, Rectangle zone, int largeur, int hauteur, int decalageX, int decalageY)
    {
        int x = ancrage switch
        {
            "hautGauche" or "milieuGauche" or "basGauche" => zone.Left + decalageX,
            "hautDroite" or "milieuDroite" or "basDroite" => zone.Right - largeur - decalageX,
            _ => zone.Left + (zone.Width - largeur) / 2
        };
        int y = ancrage switch
        {
            "hautGauche" or "hautCentre" or "hautDroite" => zone.Top + decalageY,
            "basGauche" or "basCentre" or "basDroite" => zone.Bottom - hauteur - decalageY,
            _ => zone.Top + (zone.Height - hauteur) / 2
        };
        return new Point(x, y);
    }

    /// <summary>Positionne réellement la fenêtre (déjà mesurée) selon la config de style.</summary>
    public static void Placer(Window fenetre, string ecranCible, string ancrage, double decalageX, double decalageY)
    {
        var ecran = ChoisirEcran(ecranCible);
        var hwnd = new WindowInteropHelper(fenetre).Handle;
        double dpi = VisualTreeHelper.GetDpi(fenetre).DpiScaleX; // facteur d'échelle de la fenêtre
        int largeurPx = (int)Math.Round(fenetre.ActualWidth * dpi);
        int hauteurPx = (int)Math.Round(fenetre.ActualHeight * dpi);
        int dx = (int)Math.Round(decalageX * dpi);
        int dy = (int)Math.Round(decalageY * dpi);

        var coin = CalculerCoin(ancrage, ecran.WorkingArea, largeurPx, hauteurPx, dx, dy);
        SetWindowPos(hwnd, IntPtr.Zero, coin.X, coin.Y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    private static Screen ChoisirEcran(string cible) => cible switch
    {
        "principal" => Screen.PrimaryScreen!,
        "curseur" => Screen.FromPoint(Control.MousePosition),
        _ when int.TryParse(cible, out var i) && i >= 0 && i < Screen.AllScreens.Length => Screen.AllScreens[i],
        // "actif" (et repli) : écran contenant le curseur, faute d'API simple « fenêtre active ».
        _ => Screen.FromPoint(Control.MousePosition)
    };

    private const uint SWP_NOSIZE = 0x1, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10;
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
}
```

- [ ] **Step 4 : Vérifier** — `dotnet test --filter PositionneurEcranTests` → PASS (2 tests).

- [ ] **Step 5 : Point de contrôle** — build OK.

---

### Task 13 : `OsdService` — incrustations fugitives

**Files:**
- Create: `src/IndicateurVerrouTouche/Services/OsdService.cs`
- Modify: `src/IndicateurVerrouTouche/App.xaml.cs`

- [ ] **Step 1 : Service OSD**

Créer `Services/OsdService.cs` :
```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using IndicateurVerrouTouche.Core;

namespace IndicateurVerrouTouche.Services;

/// <summary>Affiche une incrustation fugitive (fondu entrée → maintien → fondu sortie) par bascule.</summary>
public sealed class OsdService
{
    private readonly ConfigService _config;
    public OsdService(ConfigService config) => _config = config;

    public void Afficher(int vk, bool estActivee)
    {
        var touche = _config.Current.Touches.FirstOrDefault(t => t.Vk == vk);
        if (touche is null || !touche.Osd.Actif) return;
        var c = touche.Osd;

        var fenetre = new OsdWindow();
        ConfigurerContenu(fenetre, c, estActivee);
        fenetre.Show();                       // Show d'abord pour mesurer la taille réelle…
        fenetre.UpdateLayout();
        PositionneurEcran.Placer(fenetre, c.EcranCible, c.Ancrage, c.DecalageX, c.DecalageY);
        Animer(fenetre, c);                   // …puis on anime l'opacité
    }

    private static void ConfigurerContenu(OsdWindow w, OsdConfig c, bool on)
    {
        var bordure = new Border
        {
            Background = ColorHelper.VersBrush(on ? c.CouleurFondOn : c.CouleurFondOff, Colors.Black),
            CornerRadius = new CornerRadius(c.CoinsArrondis),
            Padding = new Thickness(24, 14, 24, 14),
            Child = new TextBlock
            {
                Text = on ? c.TexteOn : c.TexteOff,
                FontFamily = new FontFamily(c.Police),
                FontSize = c.TailleTexte,
                FontWeight = FontWeights.SemiBold,
                Foreground = ColorHelper.VersBrush(on ? c.CouleurTexteOn : c.CouleurTexteOff, Colors.White)
            }
        };
        w.Content = bordure;
        w.Opacity = 0;                        // démarre invisible, l'animation prend le relais
        w.OpaciteCible = c.Opacite;
    }

    private static void Animer(OsdWindow w, OsdConfig c)
    {
        var entree = new DoubleAnimation(0, w.OpaciteCible, TimeSpan.FromMilliseconds(c.FonduEntreeMs));
        var sortie = new DoubleAnimation(w.OpaciteCible, 0, TimeSpan.FromMilliseconds(c.FonduSortieMs))
        { BeginTime = TimeSpan.FromMilliseconds(c.FonduEntreeMs + c.DureeMs) };
        sortie.Completed += (_, _) => w.Close();   // fermeture propre en fin de fondu
        w.BeginAnimation(UIElement.OpacityProperty, entree);
        // 2ᵉ animation différée pour le fondu de sortie après le temps de maintien.
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(c.FonduEntreeMs + c.DureeMs) };
        t.Tick += (_, _) => { t.Stop(); w.BeginAnimation(UIElement.OpacityProperty, sortie); };
        t.Start();
    }

    /// <summary>Fenêtre OSD concrète (click-through héritée).</summary>
    private sealed class OsdWindow : ClickThroughWindow { public double OpaciteCible { get; set; } = 0.9; }
}
```

- [ ] **Step 2 : Câbler dans App**

Dans `App.xaml.cs` : champ `private OsdService _osd = null!;`, puis dans `OnStartup` après le tray :
```csharp
        _osd = new OsdService(_config);
        _monitor.KeyToggled += (_, e) => _osd.Afficher(e.Vk, e.EstActivee);
```

- [ ] **Step 3 : Point de contrôle (fonctionnel)** — `dotnet run`. Appuyer sur Verr. Maj → une incrustation « MAJUSCULES ACTIVÉES » apparaît en bas-centre, en fondu, puis disparaît. Vérifier qu'elle ne capte pas la souris (cliquer « à travers »).

### ✅ Point de revue de code — Phase 4
Vérifier : fenêtres OSD bien fermées (pas d'accumulation), timer d'animation stoppé, styles Win32 appliqués au bon moment (`OnSourceInitialized`), placement correct sur écran secondaire.

---

## PHASE 5 — Badge permanent

### Task 14 : `BadgeService`

**Files:**
- Create: `src/IndicateurVerrouTouche/Services/BadgeService.cs`
- Modify: `src/IndicateurVerrouTouche/App.xaml.cs`

- [ ] **Step 1 : Service badge**

Créer `Services/BadgeService.cs` :
```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IndicateurVerrouTouche.Core;

namespace IndicateurVerrouTouche.Services;

/// <summary>
/// Badges permanents : une fenêtre persistante par touche, affichée selon AfficherSi
/// ("on" = seulement quand verrouillé, "toujours"). Mise à jour à chaque bascule.
/// </summary>
public sealed class BadgeService
{
    private readonly ConfigService _config;
    private readonly Dictionary<int, BadgeWindow> _fenetres = new();

    public BadgeService(ConfigService config) => _config = config;

    /// <summary>Synchronise les badges avec l'état courant (appelée au démarrage et à chaque bascule).</summary>
    public void Mettre(int vk, bool estActivee)
    {
        var touche = _config.Current.Touches.FirstOrDefault(t => t.Vk == vk);
        if (touche is null || !touche.Badge.Actif) { Fermer(vk); return; }
        var c = touche.Badge;

        bool doitAfficher = c.AfficherSi == "toujours" || estActivee;
        if (!doitAfficher) { Fermer(vk); return; }

        if (!_fenetres.TryGetValue(vk, out var w)) { w = new BadgeWindow(); _fenetres[vk] = w; w.Show(); }
        w.Content = new Border
        {
            Background = ColorHelper.VersBrush(estActivee ? c.CouleurFondOn : c.CouleurFondOff, Colors.Black),
            CornerRadius = new CornerRadius(c.CoinsArrondis),
            Padding = new Thickness(12, 6, 12, 6),
            Child = new TextBlock
            {
                Text = estActivee ? c.TexteOn : c.TexteOff,
                FontFamily = new FontFamily(c.Police),
                FontSize = c.TailleTexte,
                Foreground = ColorHelper.VersBrush(estActivee ? c.CouleurTexteOn : c.CouleurTexteOff, Colors.White)
            }
        };
        w.Opacity = c.Opacite;
        w.UpdateLayout();
        PositionneurEcran.Placer(w, c.EcranCible, c.Ancrage, c.DecalageX, c.DecalageY);
    }

    public void Fermer(int vk)
    {
        if (_fenetres.Remove(vk, out var w)) w.Close();
    }

    public void ToutFermer()
    {
        foreach (var w in _fenetres.Values) w.Close();
        _fenetres.Clear();
    }

    private sealed class BadgeWindow : ClickThroughWindow { }
}
```

- [ ] **Step 2 : Câbler dans App**

Champ `private BadgeService _badge = null!;`, dans `OnStartup` après l'OSD :
```csharp
        _badge = new BadgeService(_config);
        _monitor.KeyToggled += (_, e) => _badge.Mettre(e.Vk, e.EstActivee);
        // État initial des badges au lancement.
        foreach (var vk in _monitor.VksSurveilles) _badge.Mettre(vk, _monitor.EtatDe(vk));
```
Dans `OnExit` : `_badge?.ToutFermer();`

- [ ] **Step 3 : Point de contrôle** — activer un badge dans le `config.json` (`"badge": { "actif": true }` pour capsLock), relancer, vérifier que le badge apparaît quand Verr. Maj est ON et disparaît quand OFF.

---

## PHASE 6 — Retour sonore

### Task 15 : `SoundService`

**Files:**
- Create: `src/IndicateurVerrouTouche/Services/SoundService.cs`
- Modify: `src/IndicateurVerrouTouche/App.xaml.cs`

- [ ] **Step 1 : Service son**

Créer `Services/SoundService.cs` :
```csharp
using System.IO;
using System.Media;
using System.Windows.Media;
using IndicateurVerrouTouche.Core;

namespace IndicateurVerrouTouche.Services;

/// <summary>
/// Joue un son distinct ON/OFF par touche. Chemin de fichier (WAV/MP3) lu via MediaPlayer
/// (volume réglable), "systeme" → bip système, vide → silence. Asset manquant ignoré.
/// </summary>
public sealed class SoundService
{
    private readonly ConfigService _config;
    private readonly MediaPlayer _player = new();  // réutilisé ; créé sur le thread UI
    private readonly string _dossierBase = AppContext.BaseDirectory;

    public SoundService(ConfigService config) => _config = config;

    public void Jouer(int vk, bool estActivee)
    {
        var touche = _config.Current.Touches.FirstOrDefault(t => t.Vk == vk);
        if (touche is null || !touche.Son.Actif) return;
        var c = touche.Son;
        var source = estActivee ? c.SonOn : c.SonOff;
        if (string.IsNullOrWhiteSpace(source)) return;

        if (source.Equals("systeme", StringComparison.OrdinalIgnoreCase)) { SystemSounds.Asterisk.Play(); return; }

        var chemin = Path.IsPathRooted(source) ? source : Path.Combine(_dossierBase, source);
        if (!File.Exists(chemin)) { System.Diagnostics.Debug.WriteLine($"Son introuvable : {chemin}"); return; }

        _player.Volume = Math.Clamp(c.Volume, 0, 1);
        _player.Open(new Uri(chemin, UriKind.Absolute));
        _player.Play();
    }
}
```

- [ ] **Step 2 : Câbler dans App**

Champ `private SoundService _son = null!;`, dans `OnStartup` :
```csharp
        _son = new SoundService(_config);
        _monitor.KeyToggled += (_, e) => _son.Jouer(e.Vk, e.EstActivee);
```

- [ ] **Step 3 : Point de contrôle** — mettre `"son": { "actif": true, "sonOn": "systeme", "sonOff": "systeme" }` pour une touche, relancer, vérifier le bip à chaque bascule.

---

## PHASE 7 — Fenêtre de réglages

### Task 16 : Convertisseur couleur + bouton de sélection

**Files:**
- Create: `src/IndicateurVerrouTouche/UI/HexColorToBrushConverter.cs`

- [ ] **Step 1 : Convertisseur hex → brush (aperçu)**

Créer `UI/HexColorToBrushConverter.cs` :
```csharp
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using IndicateurVerrouTouche.Core;

namespace IndicateurVerrouTouche.UI;

/// <summary>Affiche un aperçu de couleur à partir d'une chaîne hex liée à un TextBox.</summary>
public sealed class HexColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) => ColorHelper.VersBrush(value as string, Colors.Transparent);
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}
```

- [ ] **Step 2 : Point de contrôle** — build OK.

---

### Task 17 : Contrôles de réglages réutilisables

> DRY : un seul contrôle par mode, réutilisé pour chaque touche via son `DataContext`. Les contrôles éditent directement l'objet de config (copie de travail) en liaison bidirectionnelle ; pas besoin d'INotifyPropertyChanged car l'UI initialise puis écrit vers le modèle.

**Files:**
- Create: `UI/Controls/OsdSettingsControl.xaml(.cs)`, `BadgeSettingsControl.xaml(.cs)`, `TraySettingsControl.xaml(.cs)`, `SoundSettingsControl.xaml(.cs)`

- [ ] **Step 1 : Contrôle OSD**

Créer `UI/Controls/OsdSettingsControl.xaml` :
```xml
<UserControl x:Class="IndicateurVerrouTouche.UI.Controls.OsdSettingsControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:ui="clr-namespace:IndicateurVerrouTouche.UI">
    <UserControl.Resources><ui:HexColorToBrushConverter x:Key="Hex"/></UserControl.Resources>
    <StackPanel Margin="8">
        <CheckBox Content="Activer l'OSD fugitif" IsChecked="{Binding Actif}" Margin="0,0,0,8"/>
        <Grid>
            <Grid.ColumnDefinitions><ColumnDefinition Width="150"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
            <Grid.RowDefinitions>
                <RowDefinition/><RowDefinition/><RowDefinition/><RowDefinition/>
                <RowDefinition/><RowDefinition/><RowDefinition/><RowDefinition/>
            </Grid.RowDefinitions>
            <TextBlock Text="Texte (ON)" Grid.Row="0"/><TextBox Text="{Binding TexteOn}" Grid.Row="0" Grid.Column="1"/>
            <TextBlock Text="Texte (OFF)" Grid.Row="1"/><TextBox Text="{Binding TexteOff}" Grid.Row="1" Grid.Column="1"/>
            <TextBlock Text="Taille texte" Grid.Row="2"/><TextBox Text="{Binding TailleTexte}" Grid.Row="2" Grid.Column="1"/>
            <TextBlock Text="Couleur fond (ON)" Grid.Row="3"/>
            <DockPanel Grid.Row="3" Grid.Column="1">
                <Border Width="24" DockPanel.Dock="Right" Background="{Binding CouleurFondOn, Converter={StaticResource Hex}}"/>
                <Button Content="…" DockPanel.Dock="Right" Width="28" Tag="CouleurFondOn" Click="ChoisirCouleur"/>
                <TextBox Text="{Binding CouleurFondOn}"/>
            </DockPanel>
            <TextBlock Text="Opacité (0-1)" Grid.Row="4"/><TextBox Text="{Binding Opacite}" Grid.Row="4" Grid.Column="1"/>
            <TextBlock Text="Ancrage" Grid.Row="5"/>
            <ComboBox Grid.Row="5" Grid.Column="1" SelectedValue="{Binding Ancrage}" x:Name="cboAncrage"/>
            <TextBlock Text="Écran cible" Grid.Row="6"/><TextBox Text="{Binding EcranCible}" Grid.Row="6" Grid.Column="1"/>
            <TextBlock Text="Durée (ms)" Grid.Row="7"/><TextBox Text="{Binding DureeMs}" Grid.Row="7" Grid.Column="1"/>
        </Grid>
    </StackPanel>
</UserControl>
```
Créer `UI/Controls/OsdSettingsControl.xaml.cs` :
```csharp
using System.Windows;
using System.Windows.Controls;
using IndicateurVerrouTouche.Services;

namespace IndicateurVerrouTouche.UI.Controls;

public partial class OsdSettingsControl : UserControl
{
    public OsdSettingsControl()
    {
        InitializeComponent();
        cboAncrage.ItemsSource = PositionneurEcran.Ancrages;  // liste centralisée → pas de doublon
    }

    /// <summary>Ouvre le sélecteur de couleur Windows et réécrit la valeur hex liée.</summary>
    private void ChoisirCouleur(object sender, RoutedEventArgs e)
    {
        if (DataContext is not Core.OsdConfig cfg) return;
        var prop = typeof(Core.OsdConfig).GetProperty((string)((FrameworkElement)sender).Tag)!;
        using var dlg = new System.Windows.Forms.ColorDialog { FullOpen = true };
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        var c = dlg.Color;
        prop.SetValue(cfg, $"#{c.R:X2}{c.G:X2}{c.B:X2}");
        DataContext = null; DataContext = cfg;   // force le rafraîchissement de l'aperçu
    }
}
```

- [ ] **Step 2 : Contrôle Tray**

Créer `UI/Controls/TraySettingsControl.xaml` :
```xml
<UserControl x:Class="IndicateurVerrouTouche.UI.Controls.TraySettingsControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:ui="clr-namespace:IndicateurVerrouTouche.UI">
    <UserControl.Resources><ui:HexColorToBrushConverter x:Key="Hex"/></UserControl.Resources>
    <StackPanel Margin="8">
        <CheckBox Content="Afficher dans la zone de notification" IsChecked="{Binding Actif}" Margin="0,0,0,8"/>
        <Grid>
            <Grid.ColumnDefinitions><ColumnDefinition Width="150"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
            <Grid.RowDefinitions><RowDefinition/><RowDefinition/><RowDefinition/></Grid.RowDefinitions>
            <TextBlock Text="Lettre" Grid.Row="0"/><TextBox Text="{Binding Lettre}" Grid.Row="0" Grid.Column="1" MaxLength="2"/>
            <TextBlock Text="Couleur (ON)" Grid.Row="1"/>
            <DockPanel Grid.Row="1" Grid.Column="1">
                <Border Width="24" DockPanel.Dock="Right" Background="{Binding CouleurOn, Converter={StaticResource Hex}}"/>
                <TextBox Text="{Binding CouleurOn}"/>
            </DockPanel>
            <TextBlock Text="Couleur (OFF)" Grid.Row="2"/>
            <DockPanel Grid.Row="2" Grid.Column="1">
                <Border Width="24" DockPanel.Dock="Right" Background="{Binding CouleurOff, Converter={StaticResource Hex}}"/>
                <TextBox Text="{Binding CouleurOff}"/>
            </DockPanel>
        </Grid>
    </StackPanel>
</UserControl>
```
Créer `UI/Controls/TraySettingsControl.xaml.cs` :
```csharp
using System.Windows.Controls;
namespace IndicateurVerrouTouche.UI.Controls;
public partial class TraySettingsControl : UserControl { public TraySettingsControl() => InitializeComponent(); }
```

- [ ] **Step 3 : Contrôle Son**

Créer `UI/Controls/SoundSettingsControl.xaml` :
```xml
<UserControl x:Class="IndicateurVerrouTouche.UI.Controls.SoundSettingsControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <StackPanel Margin="8">
        <CheckBox Content="Activer le retour sonore" IsChecked="{Binding Actif}" Margin="0,0,0,8"/>
        <Grid>
            <Grid.ColumnDefinitions><ColumnDefinition Width="150"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
            <Grid.RowDefinitions><RowDefinition/><RowDefinition/><RowDefinition/></Grid.RowDefinitions>
            <TextBlock Text="Son ON (chemin/systeme)" Grid.Row="0"/><TextBox Text="{Binding SonOn}" Grid.Row="0" Grid.Column="1"/>
            <TextBlock Text="Son OFF (chemin/systeme)" Grid.Row="1"/><TextBox Text="{Binding SonOff}" Grid.Row="1" Grid.Column="1"/>
            <TextBlock Text="Volume (0-1)" Grid.Row="2"/><TextBox Text="{Binding Volume}" Grid.Row="2" Grid.Column="1"/>
        </Grid>
    </StackPanel>
</UserControl>
```
Créer `UI/Controls/SoundSettingsControl.xaml.cs` :
```csharp
using System.Windows.Controls;
namespace IndicateurVerrouTouche.UI.Controls;
public partial class SoundSettingsControl : UserControl { public SoundSettingsControl() => InitializeComponent(); }
```

- [ ] **Step 4 : Contrôle Badge**

Créer `UI/Controls/BadgeSettingsControl.xaml` :
```xml
<UserControl x:Class="IndicateurVerrouTouche.UI.Controls.BadgeSettingsControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:ui="clr-namespace:IndicateurVerrouTouche.UI">
    <UserControl.Resources><ui:HexColorToBrushConverter x:Key="Hex"/></UserControl.Resources>
    <StackPanel Margin="8">
        <CheckBox Content="Activer le badge permanent" IsChecked="{Binding Actif}" Margin="0,0,0,8"/>
        <Grid>
            <Grid.ColumnDefinitions><ColumnDefinition Width="150"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
            <Grid.RowDefinitions><RowDefinition/><RowDefinition/><RowDefinition/><RowDefinition/></Grid.RowDefinitions>
            <TextBlock Text="Texte (ON)" Grid.Row="0"/><TextBox Text="{Binding TexteOn}" Grid.Row="0" Grid.Column="1"/>
            <TextBlock Text="Afficher si" Grid.Row="1"/>
            <ComboBox Grid.Row="1" Grid.Column="1" SelectedValue="{Binding AfficherSi}">
                <ComboBoxItem Content="on"/><ComboBoxItem Content="toujours"/>
            </ComboBox>
            <TextBlock Text="Couleur fond (ON)" Grid.Row="2"/>
            <DockPanel Grid.Row="2" Grid.Column="1">
                <Border Width="24" DockPanel.Dock="Right" Background="{Binding CouleurFondOn, Converter={StaticResource Hex}}"/>
                <TextBox Text="{Binding CouleurFondOn}"/>
            </DockPanel>
            <TextBlock Text="Ancrage" Grid.Row="3"/>
            <ComboBox Grid.Row="3" Grid.Column="1" SelectedValue="{Binding Ancrage}" x:Name="cboAncrage"/>
        </Grid>
    </StackPanel>
</UserControl>
```
Créer `UI/Controls/BadgeSettingsControl.xaml.cs` :
```csharp
using System.Windows.Controls;
using IndicateurVerrouTouche.Services;
namespace IndicateurVerrouTouche.UI.Controls;
public partial class BadgeSettingsControl : UserControl
{
    public BadgeSettingsControl() { InitializeComponent(); cboAncrage.ItemsSource = PositionneurEcran.Ancrages; }
}
```

- [ ] **Step 5 : Point de contrôle** — `dotnet build` OK.

---

### Task 18 : `SettingsWindow` (assemblage)

**Files:**
- Create: `UI/SettingsWindow.xaml(.cs)`
- Modify: `src/IndicateurVerrouTouche/App.xaml.cs` (méthode `OuvrirReglages`)

- [ ] **Step 1 : Fenêtre de réglages**

Créer `UI/SettingsWindow.xaml` :
```xml
<Window x:Class="IndicateurVerrouTouche.UI.SettingsWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Indicateur Verrou Touche — Réglages" Width="620" Height="560" WindowStartupLocation="CenterScreen">
    <DockPanel Margin="10">
        <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,10,0,0">
            <Button Content="Importer…" Width="90" Margin="4,0" Click="Importer"/>
            <Button Content="Exporter…" Width="90" Margin="4,0" Click="Exporter"/>
            <Button Content="Annuler" Width="90" Margin="4,0" Click="Annuler"/>
            <Button Content="Enregistrer" Width="100" Margin="4,0" Click="Enregistrer" IsDefault="True"/>
        </StackPanel>
        <TabControl x:Name="onglets"/>
    </DockPanel>
</Window>
```

- [ ] **Step 2 : Code-behind (copie de travail + onglets dynamiques + aperçu)**

Créer `UI/SettingsWindow.xaml.cs` :
```csharp
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using IndicateurVerrouTouche.Core;
using IndicateurVerrouTouche.Services;
using IndicateurVerrouTouche.UI.Controls;

namespace IndicateurVerrouTouche.UI;

/// <summary>
/// Édite une COPIE de travail de la config ; n'applique qu'à l'enregistrement (via ConfigService.Save),
/// ce qui déclenche le rechargement à chaud et la ré-application par tous les services.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly ConfigService _config;
    private AppConfig _travail;
    private readonly OsdService _apercuOsd;

    public SettingsWindow(ConfigService config)
    {
        InitializeComponent();
        _config = config;
        _travail = Cloner(config.Current);   // édition isolée : Annuler = on jette la copie
        _apercuOsd = new OsdService(config); // l'aperçu lit la config courante ; suffisant pour juger le style
        ConstruireOnglets();
    }

    private void ConstruireOnglets()
    {
        onglets.Items.Clear();
        onglets.Items.Add(new TabItem { Header = "Général", Content = OngletGeneral() });
        foreach (var t in _travail.Touches)
            onglets.Items.Add(new TabItem { Header = t.Libelle, Content = OngletTouche(t) });
    }

    private UIElement OngletGeneral()
    {
        var sp = new StackPanel { Margin = new Thickness(12) };
        var chk = new CheckBox { Content = "Démarrer avec Windows", IsChecked = _travail.General.DemarrerAvecWindows };
        chk.Checked += (_, _) => _travail.General.DemarrerAvecWindows = true;
        chk.Unchecked += (_, _) => _travail.General.DemarrerAvecWindows = false;
        sp.Children.Add(chk);
        var mode = new ComboBox { ItemsSource = new[] { "hook", "polling" }, SelectedItem = _travail.General.ModeDetection, Width = 160, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0) };
        mode.SelectionChanged += (_, _) => _travail.General.ModeDetection = (string)mode.SelectedItem;
        sp.Children.Add(new TextBlock { Text = "Mode de détection :", Margin = new Thickness(0, 8, 0, 0) });
        sp.Children.Add(mode);
        return sp;
    }

    private UIElement OngletTouche(ToucheConfig t)
    {
        var sous = new TabControl();
        sous.Items.Add(new TabItem { Header = "OSD", Content = new OsdSettingsControl { DataContext = t.Osd } });
        sous.Items.Add(new TabItem { Header = "Badge", Content = new BadgeSettingsControl { DataContext = t.Badge } });
        sous.Items.Add(new TabItem { Header = "Systray", Content = new TraySettingsControl { DataContext = t.Tray } });
        sous.Items.Add(new TabItem { Header = "Son", Content = new SoundSettingsControl { DataContext = t.Son } });
        var dock = new DockPanel();
        var apercu = new Button { Content = "Aperçu OSD", Width = 110, Margin = new Thickness(8), HorizontalAlignment = HorizontalAlignment.Left };
        apercu.Click += (_, _) => { _config.Save(_travail); _apercuOsd.Afficher(t.Vk, true); };  // applique puis montre
        DockPanel.SetDock(apercu, Dock.Bottom);
        dock.Children.Add(apercu);
        dock.Children.Add(sous);
        return dock;
    }

    private void Enregistrer(object s, RoutedEventArgs e) { _config.Save(_travail); _config.DeclencherChangement(); DialogResult = true; Close(); }
    private void Annuler(object s, RoutedEventArgs e) { DialogResult = false; Close(); }

    private void Exporter(object s, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "Profil JSON|*.json", FileName = "profil.json" };
        if (dlg.ShowDialog() == true) File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(_travail, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void Importer(object s, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Profil JSON|*.json" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            _travail = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(dlg.FileName),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) ?? _travail;
            ConstruireOnglets();
        }
        catch (Exception ex) { MessageBox.Show($"Import impossible : {ex.Message}"); }
    }

    private static AppConfig Cloner(AppConfig c) =>
        JsonSerializer.Deserialize<AppConfig>(JsonSerializer.Serialize(c))!;  // clone profond par sérialisation
}
```

- [ ] **Step 3 : Ouvrir les réglages depuis App (instance unique gérée)**

Dans `App.xaml.cs`, remplacer `OuvrirReglages` (réutilise la fenêtre si déjà ouverte) :
```csharp
    private UI.SettingsWindow? _reglages;
    private void OuvrirReglages()
    {
        if (_reglages is { IsVisible: true }) { _reglages.Activate(); return; }
        _reglages = new UI.SettingsWindow(_config);
        _reglages.Closed += (_, _) => _reglages = null;
        _reglages.Show();
        _reglages.Activate();
    }
```

- [ ] **Step 4 : Point de contrôle (fonctionnel)** — `dotnet run`. Double-clic sur l'icône systray → la fenêtre de réglages s'ouvre. Modifier le texte OSD de Verr. Maj, cliquer « Aperçu OSD » → l'incrustation s'affiche avec la nouvelle valeur. « Enregistrer » → fichier mis à jour. Relancer une 2ᵉ instance → elle ouvre les réglages de la 1ʳᵉ et se ferme.

### ✅ Point de revue de code — Phase 7
Vérifier : édition sur copie de travail (Annuler ne modifie rien), liaisons bidirectionnelles correctes, sélecteur de couleur réécrit bien la valeur, import robuste au JSON invalide.

---

## PHASE 8 — Finitions & packaging

### Task 19 : Ré-application complète au rechargement à chaud

**Files:**
- Modify: `src/IndicateurVerrouTouche/App.xaml.cs`

- [ ] **Step 1 : Réagir à `ConfigChanged`**

Dans `App.xaml.cs`, à la fin de `OnStartup`, s'abonner :
```csharp
        _config.ConfigChanged += (_, _) => Dispatcher.Invoke(AppliquerConfig);
```
Ajouter la méthode (reconstruit le menu/tray et resynchronise les badges ; le changement de touches surveillées invite à redémarrer pour réinstaller le hook) :
```csharp
    private void AppliquerConfig()
    {
        _tray.ConstruireMenu();
        _tray.Redessiner();
        foreach (var vk in _monitor.VksSurveilles) _badge.Mettre(vk, _monitor.EtatDe(vk));
        DemarrageWindows.Definir(_config.Current.General.DemarrerAvecWindows);
    }
```

- [ ] **Step 2 : Point de contrôle** — appli lancée, éditer `config.json` à la main (changer une couleur tray), sauver → l'icône systray se met à jour **sans redémarrer**.

---

### Task 20 : Packaging, exemple et README

**Files:**
- Create: `build/publish.ps1`, `config.example.json`, `README.md`

- [ ] **Step 1 : Script de publication**

Créer `build/publish.ps1` :
```powershell
# Publie un .exe autonome (aucun runtime .NET requis sur la machine cible).
dotnet publish ../src/IndicateurVerrouTouche -c Release -r win-x64 --self-contained `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ../dist
Write-Host "Exécutable généré dans /dist/IndicateurVerrouTouche.exe"
```

- [ ] **Step 2 : Exemple de config commenté**

Créer `config.example.json` = la config par défaut complète (Verr. Maj + Verr. Num avec tous les champs renseignés), pour servir de référence d'édition manuelle.

- [ ] **Step 3 : README avec checklist de test manuel**

Créer `README.md` documentant : but, build (`dotnet run`), publication (`build/publish.ps1`), emplacement du `config.json`, et la **checklist de test manuel** :
  - [ ] Icône systray « A | N » visible, suit Verr. Maj / Verr. Num en temps réel.
  - [ ] OSD fugitif s'affiche/disparaît, click-through (la souris passe au travers).
  - [ ] Badge permanent apparaît/disparaît selon `afficherSi`.
  - [ ] Son ON/OFF joué quand activé.
  - [ ] Réglages : modification + Aperçu + Enregistrer appliquent les changements.
  - [ ] Rechargement à chaud du `config.json` édité à la main.
  - [ ] Démarrer avec Windows : valeur registre créée/supprimée.
  - [ ] Instance unique : 2ᵉ lancement ouvre les réglages existants.
  - [ ] Multi-écran : OSD positionné sur le bon écran (`ecranCible`).

- [ ] **Step 4 : Point de contrôle** — exécuter `build/publish.ps1`, lancer `dist/IndicateurVerrouTouche.exe`, dérouler toute la checklist.

---

### ✅ Revue de code FINALE (obligatoire)
- Lancer la suite de tests : `dotnet test` → tout vert.
- Invoquer `superpowers:requesting-code-review` sur l'ensemble du code produit.
- Appliquer `superpowers:verification-before-completion` : ne déclarer « terminé » qu'après avoir déroulé la checklist de test manuel du README et constaté les comportements réels.

---

## Auto-revue du plan (effectuée)
- **Couverture spec** : config par touche ✓, 4 modes ✓, détection hook+repli+réconciliation ✓, hot reload ✓, réglages+aperçu+import/export ✓, instance unique ✓, démarrage Windows ✓, multi-écran/DPI ✓, packaging ✓, tests ✓.
- **Placeholders** : aucun — chaque étape contient le code réel. Les assets son (`on.wav`/`off.wav`) sont optionnels (le service ignore proprement un fichier absent ; option `"systeme"` disponible sans asset).
- **Cohérence des types** : `KeyToggledEventArgs(Vk, EstActivee)`, `EstVerrouillee`, `ToucheAppuyee`, `PositionneurEcran.Placer/CalculerCoin`, `ConfigService.Save/DeclencherChangement` — noms identiques d'un bout à l'autre.
