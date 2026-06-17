using System.Drawing;
using System.Windows.Forms;
using System.Windows.Interop;

namespace IndicateurVerrouTouche.Services;

/// <summary>
/// Place une fenêtre WPF sur l'écran cible selon un ancrage. Le placement final est fait
/// en pixels physiques via SetWindowPos pour être fiable en multi-écran à DPI mixtes
/// (on contourne ainsi les conversions DIP délicates de WPF).
/// </summary>
public static class PositionneurEcran
{
    public static readonly string[] Ancrages =
    {
        "hautGauche","hautCentre","hautDroite","milieuGauche","centre","milieuDroite","basGauche","basCentre","basDroite"
    };

    /// <summary>Cibles d'écran proposées dans les réglages : valeurs symboliques + index des écrans détectés.</summary>
    public static IEnumerable<string> CiblesEcran() =>
        new[] { "actif", "principal", "curseur", "tous" }
            .Concat(Enumerable.Range(0, Screen.AllScreens.Length).Select(i => i.ToString()));

    /// <summary>Écrans visés par une cible : tous les écrans si « tous », sinon l'unique écran résolu.</summary>
    public static IReadOnlyList<Screen> EcransCibles(string ecranCible) =>
        string.Equals(ecranCible, "tous", StringComparison.OrdinalIgnoreCase)
            ? Screen.AllScreens
            : new[] { ChoisirEcran(ecranCible) };

    /// <summary>Calcule le coin haut-gauche (px) de la fenêtre dans la zone donnée. Logique pure et testable.</summary>
    public static Point CalculerCoin(string ancrage, Rectangle zone, int largeur, int hauteur, int decalageX, int decalageY)
    {
        int x = ancrage switch
        {
            "hautGauche" or "milieuGauche" or "basGauche" => zone.Left + decalageX,
            "hautDroite" or "milieuDroite" or "basDroite" => zone.Right - largeur - decalageX,
            _ => zone.Left + (zone.Width - largeur) / 2
        };
        int y = ancrage switch
        {
            "hautGauche" or "hautCentre" or "hautDroite" => zone.Top + decalageY,
            "basGauche" or "basCentre" or "basDroite" => zone.Bottom - hauteur - decalageY,
            _ => zone.Top + (zone.Height - hauteur) / 2
        };
        return new Point(x, y);
    }

    /// <summary>Positionne la fenêtre (déjà mesurée) sur l'écran résolu depuis la cible.</summary>
    public static void Placer(System.Windows.Window fenetre, string ecranCible, string ancrage, double decalageX, double decalageY)
        => Placer(fenetre, ChoisirEcran(ecranCible), ancrage, decalageX, decalageY);

    /// <summary>Positionne la fenêtre (déjà mesurée) sur un écran précis (utilisé pour la cible « tous »).</summary>
    public static void Placer(System.Windows.Window fenetre, Screen ecran, string ancrage, double decalageX, double decalageY)
    {
        var hwnd = new WindowInteropHelper(fenetre).Handle;
        // DPI de l'écran CIBLE (pas celui où la fenêtre vient d'être créée) : indispensable en
        // multi-écran à DPI mixtes, sinon les dimensions en pixels seraient calculées au mauvais facteur.
        double dpi = DpiEcran(ecran);
        int largeurPx = (int)Math.Round(fenetre.ActualWidth * dpi);
        int hauteurPx = (int)Math.Round(fenetre.ActualHeight * dpi);
        int dx = (int)Math.Round(decalageX * dpi);
        int dy = (int)Math.Round(decalageY * dpi);

        var coin = CalculerCoin(ancrage, ecran.WorkingArea, largeurPx, hauteurPx, dx, dy);
        SetWindowPos(hwnd, IntPtr.Zero, coin.X, coin.Y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    private static Screen ChoisirEcran(string cible) => cible switch
    {
        "principal" => Screen.PrimaryScreen!,
        "curseur" => Screen.FromPoint(Control.MousePosition),
        _ when int.TryParse(cible, out var i) && i >= 0 && i < Screen.AllScreens.Length => Screen.AllScreens[i],
        // "actif" (et repli) : écran contenant le curseur, faute d'API simple « fenêtre active ».
        _ => Screen.FromPoint(Control.MousePosition)
    };

    /// <summary>Facteur d'échelle (DPI/96) de l'écran cible. Repli 1.0 si l'API DPI échoue.</summary>
    private static double DpiEcran(Screen ecran)
    {
        var centre = new Point(ecran.Bounds.Left + ecran.Bounds.Width / 2, ecran.Bounds.Top + ecran.Bounds.Height / 2);
        var hmon = MonitorFromPoint(centre, MONITOR_DEFAULTTONEAREST);
        return GetDpiForMonitor(hmon, MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 ? dpiX / 96.0 : 1.0;
    }

    private const uint SWP_NOSIZE = 0x1, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int MDT_EFFECTIVE_DPI = 0;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(Point pt, uint dwFlags);
    [System.Runtime.InteropServices.DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
}
