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
