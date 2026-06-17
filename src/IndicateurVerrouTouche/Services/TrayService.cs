using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using IndicateurVerrouTouche.Core;

namespace IndicateurVerrouTouche.Services;

/// <summary>
/// Icônes de la zone de notification : UNE icône distincte par touche surveillée (lettre + couleur
/// ON/OFF). Chaque icône est redessinée à chaque changement d'état, avec libération des handles GDI.
/// </summary>
public sealed class TrayService : IDisposable
{
    private readonly ConfigService _config;
    private readonly KeyStateMonitor _monitor;
    private readonly Dictionary<int, NotifyIcon> _icones = new();
    private readonly Dictionary<int, IntPtr> _hicons = new();
    private ContextMenuStrip? _menu;
    private Action? _onBalloonClick;

    public event EventHandler? OuvrirReglagesDemande;
    public event EventHandler? QuitterDemande;
    public event EventHandler? DesinstallerDemande;

    public TrayService(ConfigService config, KeyStateMonitor monitor)
    {
        _config = config;
        _monitor = monitor;
        ConstruireMenu();
        Redessiner();
    }

    /// <summary>Reconstruit le menu contextuel partagé par toutes les icônes (démarrage + hot-reload).</summary>
    public void ConstruireMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Réglages…", null, (_, _) => OuvrirReglagesDemande?.Invoke(this, EventArgs.Empty));
        var demarrage = new ToolStripMenuItem("Démarrer avec Windows")
        { Checked = _config.Current.General.DemarrerAvecWindows, CheckOnClick = true };
        demarrage.CheckedChanged += (_, _) =>
        {
            _config.Current.General.DemarrerAvecWindows = demarrage.Checked;
            DemarrageWindows.Definir(demarrage.Checked);
            _config.Save(_config.Current);
        };
        menu.Items.Add(demarrage);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Désinstaller…", null, (_, _) => DesinstallerDemande?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Quitter", null, (_, _) => QuitterDemande?.Invoke(this, EventArgs.Empty));

        _menu?.Dispose();   // libère l'ancien menu (le NotifyIcon ne le possède pas)
        _menu = menu;
        foreach (var ni in _icones.Values) ni.ContextMenuStrip = _menu;
    }

    /// <summary>Synchronise les icônes (une par touche systray active) avec l'état courant + l'infobulle.</summary>
    public void Redessiner()
    {
        var actives = _config.Current.Touches.Where(t => t.Activee && t.Tray.Actif).ToList();
        var vksActifs = actives.Select(t => t.Vk).ToHashSet();

        // Retire les icônes des touches qui ne sont plus affichées (désactivées via la config).
        foreach (var vk in _icones.Keys.Where(vk => !vksActifs.Contains(vk)).ToList())
            SupprimerIcone(vk);

        foreach (var t in actives)
        {
            if (!_icones.TryGetValue(t.Vk, out var ni))
            {
                ni = new NotifyIcon { Visible = true, ContextMenuStrip = _menu };
                ni.DoubleClick += (_, _) => OuvrirReglagesDemande?.Invoke(this, EventArgs.Empty);
                ni.BalloonTipClicked += (_, _) => _onBalloonClick?.Invoke();
                _icones[t.Vk] = ni;
            }
            bool on = _monitor.EtatDe(t.Vk);
            DessinerIcone(t, on);
            ni.Text = $"{t.Libelle} : {(on ? "ON" : "OFF")}";
        }
    }

    /// <summary>Dessine l'icône d'une touche : lettre grasse blanche centrée sur un fond plein coloré.</summary>
    private void DessinerIcone(ToucheConfig t, bool on)
    {
        var couleur = ToDrawing(on ? t.Tray.CouleurOn : t.Tray.CouleurOff);
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);
            using (var fond = new SolidBrush(couleur)) g.FillRectangle(fond, 0, 0, 32, 32);
            // Lettre grande et grasse → lisible même réduite dans la zone de notification.
            using var f = new Font("Segoe UI", 22f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(t.Tray.Lettre, f, Brushes.White, new RectangleF(0, 0, 32, 32), sf);
        }
        IntPtr nouveau = bmp.GetHicon();
        _icones[t.Vk].Icon = Icon.FromHandle(nouveau);
        if (_hicons.TryGetValue(t.Vk, out var ancien) && ancien != IntPtr.Zero) DestroyIcon(ancien);
        _hicons[t.Vk] = nouveau;
    }

    private void SupprimerIcone(int vk)
    {
        if (_icones.Remove(vk, out var ni)) { ni.Visible = false; ni.Dispose(); }
        if (_hicons.Remove(vk, out var h) && h != IntPtr.Zero) DestroyIcon(h);
    }

    /// <summary>Affiche une bulle d'information sur la première icône ; un clic dessus déclenche onClick.</summary>
    public void AfficherNotification(string titre, string message, Action? onClick)
    {
        _onBalloonClick = onClick;
        _icones.Values.FirstOrDefault()?.ShowBalloonTip(10000, titre, message, ToolTipIcon.Info);
    }

    private static Color ToDrawing(string hex)
    {
        var c = ColorHelper.VersColor(hex, System.Windows.Media.Colors.Gray);
        return Color.FromArgb(c.A == 0 ? (byte)255 : c.A, c.R, c.G, c.B);
    }

    public void Dispose()
    {
        foreach (var vk in _icones.Keys.ToList()) SupprimerIcone(vk);
        _menu?.Dispose();
    }

    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);
}
