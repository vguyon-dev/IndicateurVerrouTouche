using System.Windows;
using System.Windows.Controls;

namespace IndicateurVerrouTouche.UI.Controls;

/// <summary>Réglages du retour sonore d'une touche (DataContext = SonConfig).</summary>
public partial class SoundSettingsControl : UserControl
{
    public SoundSettingsControl() => InitializeComponent();

    /// <summary>Ouvre un sélecteur de fichier et écrit le chemin choisi dans la propriété liée (Tag).</summary>
    private void ChoisirSon(object sender, RoutedEventArgs e)
    {
        if (DataContext is not Core.SonConfig cfg || ((FrameworkElement)sender).Tag is not string nomProp) return;
        var prop = typeof(Core.SonConfig).GetProperty(nomProp);
        if (prop is null) return;
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Sons (*.wav;*.mp3)|*.wav;*.mp3|Tous les fichiers|*.*" };
        if (dlg.ShowDialog() != true) return;
        prop.SetValue(cfg, dlg.FileName);   // chemin absolu du fichier choisi
        DataContext = null; DataContext = cfg;
    }
}
