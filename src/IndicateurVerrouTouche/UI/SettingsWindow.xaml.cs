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

        sp.Children.Add(new TextBlock { Text = "Mode de détection :", Margin = new Thickness(0, 8, 0, 0) });
        var modes = new[]
        {
            new ModeDetection("Hook (recommandé)", "hook"),
            new ModeDetection("Sondage périodique", "polling"),
        };
        var mode = new ComboBox
        {
            ItemsSource = modes,
            DisplayMemberPath = nameof(ModeDetection.Libelle),
            SelectedValuePath = nameof(ModeDetection.Valeur),
            SelectedValue = _travail.General.ModeDetection,
            Width = 200, HorizontalAlignment = HorizontalAlignment.Left
        };
        mode.SelectionChanged += (_, _) => { if (mode.SelectedValue is string v) _travail.General.ModeDetection = v; };
        sp.Children.Add(mode);

        // Mises à jour (dossier réseau paramétrable).
        sp.Children.Add(new TextBlock { Text = "Dossier de mises à jour (UNC) :", Margin = new Thickness(0, 16, 0, 0) });
        var ligneMaj = new StackPanel { Orientation = Orientation.Horizontal };
        var cheminMaj = new TextBox { Text = _travail.General.CheminMaJ, Width = 360, VerticalAlignment = VerticalAlignment.Center };
        cheminMaj.TextChanged += (_, _) => _travail.General.CheminMaJ = cheminMaj.Text;
        var btnParcourir = new Button { Content = "Parcourir…", Width = 90, Margin = new Thickness(6, 0, 0, 0) };
        btnParcourir.Click += (_, _) =>
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Dossier de mises à jour" };
            try { if (System.IO.Directory.Exists(cheminMaj.Text)) dlg.InitialDirectory = cheminMaj.Text; } catch { /* chemin invalide : ignoré */ }
            if (dlg.ShowDialog() == true) cheminMaj.Text = dlg.FolderName;
        };
        ligneMaj.Children.Add(cheminMaj);
        ligneMaj.Children.Add(btnParcourir);
        sp.Children.Add(ligneMaj);
        var defautMaj = InstallationLocale.DossierMajParDefaut();
        sp.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(defautMaj)
                ? "Laissez vide pour détecter automatiquement le dossier de distribution."
                : $"Laissez vide pour détecter automatiquement : {defautMaj}",
            Foreground = System.Windows.Media.Brushes.Gray,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 0)
        });
        var chkMaj = new CheckBox { Content = "Vérifier les mises à jour au démarrage", IsChecked = _travail.General.VerifierMajAuDemarrage, Margin = new Thickness(0, 6, 0, 0) };
        chkMaj.Checked += (_, _) => _travail.General.VerifierMajAuDemarrage = true;
        chkMaj.Unchecked += (_, _) => _travail.General.VerifierMajAuDemarrage = false;
        sp.Children.Add(chkMaj);
        var btnMaj = new Button { Content = "Vérifier maintenant", Width = 180, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 0) };
        btnMaj.Click += (_, _) => VerifierMajManuel(cheminMaj.Text);
        sp.Children.Add(btnMaj);

        // À propos : version (format Année.Mois.Jour + lettre) + créateur.
        sp.Children.Add(new TextBlock
        {
            Text = $"Indicateur Verrou Touche\nversion {MiseAJour.VersionActuelle}\n© GUYON Valentin",
            Margin = new Thickness(0, 24, 0, 0),
            Foreground = System.Windows.Media.Brushes.Gray
        });
        return sp;
    }

    /// <summary>Vérifie manuellement le partage et propose l'installation si une version plus récente existe.</summary>
    private void VerifierMajManuel(string chemin)
    {
        if (string.IsNullOrWhiteSpace(chemin)) chemin = InstallationLocale.DossierMajParDefaut() ?? "";
        var info = MiseAJour.Verifier(chemin);
        if (info is null) { MessageBox.Show("Aucune mise à jour trouvée (dossier vide, injoignable ou non configuré)."); return; }
        if (!MiseAJour.EstPlusRecente(info)) { MessageBox.Show($"Vous avez déjà la dernière version ({MiseAJour.VersionActuelle})."); return; }
        var notes = string.IsNullOrWhiteSpace(info.Notes) ? "" : $"\n\n{info.Notes}";
        if (MessageBox.Show($"Version {info.Version} disponible.{notes}\n\nInstaller maintenant ?", "Mise à jour",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try { MiseAJour.Installer(info, chemin, () => Application.Current.Shutdown()); }
        catch (Exception ex) { MessageBox.Show($"Échec : {ex.Message}"); }
    }

    /// <summary>Libellé français d'un mode de détection ↔ valeur stockée en config.</summary>
    private sealed record ModeDetection(string Libelle, string Valeur);

    private UIElement OngletTouche(ToucheConfig t)
    {
        var sous = new TabControl();
        sous.Items.Add(new TabItem { Header = "OSD", Content = new OsdSettingsControl { DataContext = t.Osd } });
        sous.Items.Add(new TabItem { Header = "Badge", Content = new BadgeSettingsControl { DataContext = t.Badge } });
        sous.Items.Add(new TabItem { Header = "Systray", Content = new TraySettingsControl { DataContext = t.Tray } });
        sous.Items.Add(new TabItem { Header = "Son", Content = new SoundSettingsControl { DataContext = t.Son } });
        var dock = new DockPanel();
        var apercu = new Button { Content = "Aperçu OSD", Width = 110, Margin = new Thickness(8), HorizontalAlignment = HorizontalAlignment.Left };
        apercu.Click += (_, _) => _apercuOsd.Afficher(t.Osd, true);  // aperçu de la copie de travail, sans rien persister
        DockPanel.SetDock(apercu, Dock.Bottom);
        dock.Children.Add(apercu);
        dock.Children.Add(sous);
        return dock;
    }

    // Fenêtre ouverte en modeless (Show) : ne PAS toucher DialogResult — son setter lèverait
    // InvalidOperationException hors d'un ShowDialog(). On se contente de fermer.
    private void Enregistrer(object s, RoutedEventArgs e) { _config.Save(_travail); _config.DeclencherChangement(); Close(); }
    private void Annuler(object s, RoutedEventArgs e) => Close();

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
            var importe = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(dlg.FileName),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            // Un JSON valide mais incohérent (ex. "touches": null) ne doit pas écraser la copie de travail.
            if (importe?.Touches is null) { MessageBox.Show("Import impossible : profil invalide (aucune touche)."); return; }
            _travail = importe;
            ConstruireOnglets();
        }
        catch (Exception ex) { MessageBox.Show($"Import impossible : {ex.Message}"); }
    }

    private static AppConfig Cloner(AppConfig c) =>
        JsonSerializer.Deserialize<AppConfig>(JsonSerializer.Serialize(c))!;  // clone profond par sérialisation
}
