using System.Windows;
using System.Windows.Controls;
using IndicateurVerrouTouche.Services;

namespace IndicateurVerrouTouche.UI.Controls;

/// <summary>Réglages du badge permanent d'une touche (DataContext = BadgeConfig).</summary>
public partial class BadgeSettingsControl : UserControl
{
    public BadgeSettingsControl()
    {
        InitializeComponent();
        cboAncrage.ItemsSource = PositionneurEcran.Ancrages;
    }

    private void ChoisirCouleur(object sender, RoutedEventArgs e)
    {
        if (!SelecteurCouleur.Choisir((FrameworkElement)sender, DataContext)) return;
        var ctx = DataContext; DataContext = null; DataContext = ctx;
    }
}
