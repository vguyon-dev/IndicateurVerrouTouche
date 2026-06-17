using System.Windows;
using System.Windows.Controls;

namespace IndicateurVerrouTouche.UI.Controls;

/// <summary>Réglages de l'icône systray d'une touche (DataContext = TrayConfig).</summary>
public partial class TraySettingsControl : UserControl
{
    public TraySettingsControl() => InitializeComponent();

    private void ChoisirCouleur(object sender, RoutedEventArgs e)
    {
        if (!SelecteurCouleur.Choisir((FrameworkElement)sender, DataContext)) return;
        var ctx = DataContext; DataContext = null; DataContext = ctx;
    }
}
