using System.Windows;
using IndicateurVerrouTouche.Core;

namespace IndicateurVerrouTouche.UI;

/// <summary>
/// Sélecteur de couleur partagé par les contrôles de réglages. Ouvre la palette Windows complète
/// (roue / couleurs personnalisées) et écrit la valeur hex dans la propriété ciblée du DataContext.
/// </summary>
public static class SelecteurCouleur
{
    /// <summary>Le Tag du bouton porte le nom de la propriété string (hex) à modifier sur le contexte.</summary>
    public static bool Choisir(FrameworkElement bouton, object? contexte)
    {
        if (contexte is null || bouton.Tag is not string nomProp) return false;
        var prop = contexte.GetType().GetProperty(nomProp);
        if (prop is null || prop.PropertyType != typeof(string)) return false;

        var actuel = ColorHelper.VersColor(prop.GetValue(contexte) as string, System.Windows.Media.Colors.White);
        using var dlg = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,   // ouvre directement la palette complète + couleurs personnalisées
            Color = System.Drawing.Color.FromArgb(actuel.R, actuel.G, actuel.B)
        };
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return false;
        var c = dlg.Color;
        prop.SetValue(contexte, $"#{c.R:X2}{c.G:X2}{c.B:X2}");
        return true;
    }
}
