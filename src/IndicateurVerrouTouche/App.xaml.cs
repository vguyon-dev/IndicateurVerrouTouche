using System.Threading;
using System.Windows;
using System.Windows.Threading;
using IndicateurVerrouTouche.Core;
using IndicateurVerrouTouche.Services;

// Désambiguïsation nécessaire : UseWindowsForms expose aussi System.Windows.Forms.Application.
using Application = System.Windows.Application;

namespace IndicateurVerrouTouche;

/// <summary>
/// Point d'entrée applicatif : instance unique, démarrage caché dans le systray,
/// câblage des modules (config, moniteur clavier, services d'indication) et arrêt propre.
/// </summary>
public partial class App : Application
{
    private const string NomMutex = "IndicateurVerrouTouche_SingleInstance";
    private const string NomEvenement = "IndicateurVerrouTouche_ShowSettings";
    private Mutex? _mutex;
    private bool _proprietaireMutex;   // seul le propriétaire peut appeler ReleaseMutex sans lever
    private EventWaitHandle? _evtAffichage;

    private ConfigService _config = null!;
    private KeyStateMonitor _monitor = null!;
    private Win32KeyboardSource _source = null!;
    private DispatcherTimer _timer = null!;
    private TrayService _tray = null!;
    private OsdService _osd = null!;
    private BadgeService _badge = null!;
    private SoundService _son = null!;
    private DispatcherTimer? _timerMaj;     // re-vérification périodique des mises à jour
    private bool _majEnCours;               // évite les installations multiples concurrentes
    private string? _versionProposee;       // évite de re-notifier en boucle la même version (non forcée)

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;  // l'appli vit dans le systray, pas de fenêtre

        // Mode désinstallation (depuis « Applications et fonctionnalités » ou le menu de l'icône).
        if (e.Args.Any(a => string.Equals(a, "--desinstaller", StringComparison.OrdinalIgnoreCase)))
        {
            Desinstallation.Executer();
            Shutdown();
            return;
        }

        // Auto-installation : si lancé depuis le partage réseau, se copier en local et relancer
        // depuis là (l'auto-MAJ remplacera la copie locale, jamais le fichier partagé commun).
        if (InstallationLocale.InstallerEtRelancerSiReseau()) { Shutdown(); return; }

        // Instance unique : si déjà lancée, on demande l'ouverture des réglages puis on quitte.
        _mutex = new Mutex(true, NomMutex, out bool premier);
        _proprietaireMutex = premier;   // true uniquement pour la 1ʳᵉ instance (celle qui détient le mutex)
        if (!premier)
        {
            // La 1ʳᵉ instance peut être en cours d'arrêt : ne pas planter si le handle a disparu.
            try { EventWaitHandle.OpenExisting(NomEvenement).Set(); } catch { /* ignoré */ }
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

        // Icône systray dynamique : suit l'état des touches en temps réel.
        _tray = new TrayService(_config, _monitor);
        _tray.OuvrirReglagesDemande += (_, _) => OuvrirReglages();
        _tray.QuitterDemande += (_, _) => Shutdown();
        _tray.DesinstallerDemande += (_, _) => { if (Desinstallation.Executer()) Shutdown(); };
        _monitor.KeyToggled += (_, _) => _tray.Redessiner();

        // OSD fugitif : incrustation à chaque bascule.
        _osd = new OsdService(_config);
        _monitor.KeyToggled += (_, e) => _osd.Afficher(e.Vk, e.EstActivee);

        // Badge permanent : fenêtre persistante par touche, synchronisée avec l'état.
        _badge = new BadgeService(_config);
        _monitor.KeyToggled += (_, e) => _badge.Mettre(e.Vk, e.EstActivee);
        foreach (var vk in _monitor.VksSurveilles) _badge.Mettre(vk, _monitor.EtatDe(vk));  // état initial au lancement

        // Retour sonore : son distinct ON/OFF par touche.
        _son = new SoundService(_config);
        _monitor.KeyToggled += (_, e) => _son.Jouer(e.Vk, e.EstActivee);

        // Rechargement à chaud : réappliquer la config (édition manuelle ou via les réglages).
        _config.ConfigChanged += (_, _) => Dispatcher.Invoke(AppliquerConfig);

        // Vérification de mise à jour : au démarrage, puis périodiquement (pour appliquer une MAJ
        // forcée même sur une instance déjà lancée, sans attendre de redémarrage).
        if (_config.Current.General.VerifierMajAuDemarrage)
        {
            VerifierMaj();
            int min = _config.Current.General.IntervalleVerifMajMinutes;
            if (min > 0)
            {
                _timerMaj = new DispatcherTimer { Interval = TimeSpan.FromMinutes(min) };
                _timerMaj.Tick += (_, _) => VerifierMaj();
                _timerMaj.Start();
            }
        }

        // Épingler les icônes dans la zone de notification (Windows 11), en différé le temps que
        // Windows enregistre les icônes (best-effort ; pleinement effectif dès le lancement suivant).
        if (_config.Current.General.EpinglerSystray)
            new Thread(() => { Thread.Sleep(2500); EpinglageSystray.Promouvoir(); }) { IsBackground = true }.Start();

        // Inscrire dans « Applications et fonctionnalités » si on tourne depuis l'installation locale.
        if (string.Equals(Environment.ProcessPath, InstallationLocale.CheminLocal, StringComparison.OrdinalIgnoreCase))
            Desinstallation.EnregistrerDansProgrammes();
    }

    /// <summary>
    /// Dossier où chercher les mises à jour : le chemin configuré s'il est renseigné, sinon le
    /// dossier d'origine détecté automatiquement (partage réseau de distribution ou dossier
    /// d'extraction de l'exe), pour que l'auto-MAJ fonctionne quel que soit l'emplacement de lancement.
    /// </summary>
    private string? CheminMajEffectif()
    {
        var c = _config.Current.General.CheminMaJ;
        return string.IsNullOrWhiteSpace(c) ? InstallationLocale.DossierMajParDefaut() : c;
    }

    /// <summary>
    /// Vérifie en arrière-plan la présence d'une mise à jour. Si le manifeste la marque « forcer »,
    /// elle est installée immédiatement et sans confirmation ; sinon elle est proposée via une bulle.
    /// </summary>
    private void VerifierMaj()
    {
        var chemin = CheminMajEffectif();
        if (string.IsNullOrWhiteSpace(chemin) || _majEnCours) return;
        new Thread(() =>
        {
            var info = MiseAJour.Verifier(chemin);   // lecture réseau : hors thread UI, ne bloque pas
            if (info is null || !MiseAJour.EstPlusRecente(info)) return;
            Dispatcher.Invoke(() =>
            {
                if (info.Forcer) InstallerMajForcee(info);
                else if (_versionProposee != info.Version)   // une seule bulle par version proposée
                {
                    _versionProposee = info.Version;
                    _tray.AfficherNotification("Mise à jour disponible",
                        $"Version {info.Version} disponible — cliquez pour installer.", () => LancerMaj(info));
                }
            });
        }) { IsBackground = true }.Start();
    }

    /// <summary>Installe une mise à jour FORCÉE sans confirmation : l'app se ferme, se remplace et se relance.</summary>
    private void InstallerMajForcee(InfoMaJ info)
    {
        if (_majEnCours) return;
        var chemin = CheminMajEffectif();
        if (string.IsNullOrWhiteSpace(chemin)) return;
        _majEnCours = true;
        _tray?.AfficherNotification("Mise à jour", $"Installation automatique de la version {info.Version}…", null);
        try { MiseAJour.Installer(info, chemin, () => Shutdown()); }
        catch { _majEnCours = false; }   // échec : nouvelle tentative au prochain cycle de vérification
    }

    /// <summary>Confirme puis installe la mise à jour (l'app se ferme, l'exe est remplacé puis relancé).</summary>
    private void LancerMaj(InfoMaJ info)
    {
        var chemin = CheminMajEffectif();
        if (string.IsNullOrWhiteSpace(chemin)) return;
        var notes = string.IsNullOrWhiteSpace(info.Notes) ? "" : $"\n\n{info.Notes}";
        if (MessageBox.Show($"Installer la version {info.Version} ?{notes}", "Mise à jour",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try { MiseAJour.Installer(info, chemin, () => Shutdown()); }
        catch (Exception ex)
        {
            MessageBox.Show($"Échec de la mise à jour : {ex.Message}", "Mise à jour", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Réapplique la configuration après un changement à chaud : reconstruit le menu/icône
    /// systray, resynchronise les badges et l'inscription au démarrage Windows. Un changement
    /// des touches surveillées nécessite en revanche un redémarrage (réinstallation du hook).
    /// </summary>
    private void AppliquerConfig()
    {
        _tray.ConstruireMenu();
        _tray.Redessiner();
        foreach (var vk in _monitor.VksSurveilles) _badge.Mettre(vk, _monitor.EtatDe(vk));
        DemarrageWindows.Definir(_config.Current.General.DemarrerAvecWindows);
    }

    /// <summary>Codes de touches virtuelles des touches activées (sans doublon).</summary>
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

    private UI.SettingsWindow? _reglages;

    /// <summary>Ouvre (ou réactive) la fenêtre de réglages. Une seule instance à la fois.</summary>
    private void OuvrirReglages()
    {
        if (_reglages is { IsVisible: true })
        {
            // Une fenêtre minimisée garde IsVisible==true ; Activate() seul ne la restaure pas.
            if (_reglages.WindowState == WindowState.Minimized) _reglages.WindowState = WindowState.Normal;
            _reglages.Activate();
            return;
        }
        _reglages = new UI.SettingsWindow(_config);
        _reglages.Closed += (_, _) => _reglages = null;
        _reglages.Show();
        _reglages.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _timer?.Stop();
        _timerMaj?.Stop();
        _badge?.ToutFermer();    // ferme les fenêtres de badge
        _tray?.Dispose();
        _monitor?.Dispose();
        _source?.Dispose();      // libère le hook clavier
        _config?.Dispose();      // libère le FileSystemWatcher
        // ReleaseMutex lèverait ApplicationException si cette instance ne détient pas le mutex
        // (cas de la 2ᵉ instance, qui n'en est jamais propriétaire).
        if (_proprietaireMutex) _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
