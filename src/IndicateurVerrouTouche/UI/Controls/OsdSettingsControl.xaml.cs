using System.Windows;
using System.Windows.Controls;
using IndicateurVerrouTouche.Services;

namespace IndicateurVerrouTouche.UI.Controls;

/// <summary>Réglages de l'OSD fugitif d'une touche (DataContext = OsdConfig).</summary>
public partial class OsdSettingsControl : UserControl
{
    public OsdSettingsControl()
    {
        InitializeComponent();
        cboAncrage.ItemsSource = PositionneurEcran.Ancrages;       // listes centralisées → pas de doublon
        cboEcran.ItemsSource = PositionneurEcran.CiblesEcran();
    }

    /// <summary>Ouvre la palette Windows et réécrit la valeur hex liée (Tag = nom de propriété).</summary>
    private void ChoisirCouleur(object sender, RoutedEventArgs e)
    {
        if (!SelecteurCouleur.Choisir((FrameworkElement)sender, DataContext)) return;
        var ctx = DataContext; DataContext = null; DataContext = ctx;   // force le rafraîchissement des aperçus
    }
}
