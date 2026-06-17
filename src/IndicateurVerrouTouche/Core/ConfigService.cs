using System.IO;
using System.Text.Json;
using System.Threading;

namespace IndicateurVerrouTouche.Core;

/// <summary>
/// Source de vérité de la configuration. Charge/sauve config.json et notifie
/// les changements (rechargement à chaud ajouté en Task 5).
/// </summary>
public sealed class ConfigService : IDisposable
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping // garde les accents lisibles
    };

    public string CheminFichier { get; }
    // Défauts fonctionnels d'emblée : si le fichier est corrompu au tout premier chargement
    // (aucune « dernière config valide » à conserver), l'appli garde des indicateurs utilisables.
    public AppConfig Current { get; private set; } = AppConfig.ParDefaut();
    public event EventHandler? ConfigChanged;

    public ConfigService(string cheminFichier) => CheminFichier = cheminFichier;

    /// <summary>Chemin par défaut : %AppData%\IndicateurVerrouTouche\config.json.</summary>
    public static string CheminParDefaut() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "IndicateurVerrouTouche", "config.json");

    /// <summary>Charge la configuration depuis le fichier ; crée et enregistre les valeurs par défaut si absent.</summary>
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

    /// <summary>Sérialise et enregistre la configuration ; met à jour Current uniquement si l'écriture réussit.</summary>
    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CheminFichier)!);
        // Les notifications du watcher dues à CETTE écriture arrivent de façon ASYNCHRONE quelques ms
        // plus tard : on ouvre une courte fenêtre d'ignorance pour ne pas se recharger soi-même.
        Interlocked.Exchange(ref _ignorerJusquaTicks, DateTime.UtcNow.AddMilliseconds(750).Ticks);
        File.WriteAllText(CheminFichier, JsonSerializer.Serialize(config, Options));
        Current = config;   // seulement si l'écriture a réussi (sinon l'exception remonte, Current inchangé)
    }

    internal void DeclencherChangement() => ConfigChanged?.Invoke(this, EventArgs.Empty);

    private FileSystemWatcher? _watcher;
    private long _ignorerJusquaTicks;   // ignore les notifications du watcher jusqu'à ce tick (nos propres écritures)
    private long _dernierEvtTicks = DateTime.MinValue.Ticks;

    /// <summary>Active la détection des modifications externes du fichier (édition manuelle).</summary>
    public void DemarrerSurveillance()
    {
        _watcher?.Dispose();
        var dossier = Path.GetDirectoryName(CheminFichier)!;
        Directory.CreateDirectory(dossier);
        _watcher = new FileSystemWatcher(dossier, Path.GetFileName(CheminFichier))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };
        _watcher.Changed += (_, _) =>
        {
            // Ignore les notifications provoquées par nos propres sauvegardes (fenêtre temporelle).
            if (DateTime.UtcNow.Ticks < Interlocked.Read(ref _ignorerJusquaTicks)) return;
            // Anti-rebond : les éditeurs écrivent souvent en plusieurs salves.
            var maintenant = DateTime.UtcNow.Ticks;
            if (maintenant - Interlocked.Read(ref _dernierEvtTicks) < 300 * TimeSpan.TicksPerMillisecond) return;
            Interlocked.Exchange(ref _dernierEvtTicks, maintenant);
            try { Thread.Sleep(50); Load(); DeclencherChangement(); }
            catch (IOException) { /* fichier transitoirement verrouillé : on ignore, prochain évènement rejouera */ }
        };
    }

    /// <summary>Libère le FileSystemWatcher associé à la surveillance du fichier.</summary>
    public void Dispose()
    {
        _watcher?.Dispose();
        _watcher = null;
    }
}
