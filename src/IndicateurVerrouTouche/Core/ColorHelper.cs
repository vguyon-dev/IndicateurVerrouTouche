using System.Windows.Media;
using WpfColor = System.Windows.Media.Color;

namespace IndicateurVerrouTouche.Core;

/// <summary>Conversion robuste entre chaînes hex (#RGB/#RRGGBB/#AARRGGBB) et couleurs WPF.</summary>
public static class ColorHelper
{
    /// <summary>Convertit une chaîne hex en couleur WPF ; retourne la couleur de repli si la chaîne est invalide.</summary>
    public static WpfColor VersColor(string? hex, WpfColor repli = default)
    {
        try { return (WpfColor)System.Windows.Media.ColorConverter.ConvertFromString(hex ?? "")!; }
        catch { return repli; }  // hex invalide → couleur de repli, jamais d'exception remontée
    }

    /// <summary>Convertit une chaîne hex en SolidColorBrush figé et partageable entre threads.</summary>
    public static SolidColorBrush VersBrush(string? hex, WpfColor repli = default)
    {
        var b = new SolidColorBrush(VersColor(hex, repli));
        b.Freeze();  // figé = partageable entre threads et plus performant
        return b;
    }
}
