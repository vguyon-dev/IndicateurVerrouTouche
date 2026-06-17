using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IndicateurVerrouTouche.Core;

namespace IndicateurVerrouTouche.Services;

/// <summary>
/// Badges permanents : une (ou plusieurs, si cible « tous ») fenêtre(s) persistante(s) par touche,
/// affichée(s) selon AfficherSi ("on" = seulement quand verrouillé, "toujours"). Mise à jour à chaque bascule.
/// </summary>
public sealed class BadgeService
{
    private readonly ConfigService _config;
    private readonly Dictionary<int, List<BadgeWindow>> _fenetres = new();

    public BadgeService(ConfigService config) => _config = config;

    /// <summary>Synchronise les badges avec l'état courant (appelée au démarrage et à chaque bascule).</summary>
    public void Mettre(int vk, bool estActivee)
    {
        var touche = _config.Current.Touches.FirstOrDefault(t => t.Vk == vk);
        if (touche is null || !touche.Badge.Actif) { Fermer(vk); return; }
        var c = touche.Badge;

        bool doitAfficher = c.AfficherSi == "toujours" || estActivee;
        if (!doitAfficher) { Fermer(vk); return; }

        var ecrans = PositionneurEcran.EcransCibles(c.EcranCible);
        // Réutilise les fenêtres tant que le nombre d'écrans cibles est inchangé (évite le clignotement).
        if (!_fenetres.TryGetValue(vk, out var liste) || liste.Count != ecrans.Count)
        {
            Fermer(vk);
            liste = new List<BadgeWindow>();
            foreach (var _ in ecrans) { var w = new BadgeWindow(); w.Show(); liste.Add(w); }
            _fenetres[vk] = liste;
        }
        for (int i = 0; i < ecrans.Count; i++)
        {
            ConfigurerContenu(liste[i], c, estActivee);
            liste[i].UpdateLayout();
            PositionneurEcran.Placer(liste[i], ecrans[i], c.Ancrage, c.DecalageX, c.DecalageY);
        }
    }

    private static void ConfigurerContenu(BadgeWindow w, BadgeConfig c, bool estActivee)
    {
        w.Content = new Border
        {
            Background = ColorHelper.VersBrush(estActivee ? c.CouleurFondOn : c.CouleurFondOff, Colors.Black),
            CornerRadius = new CornerRadius(c.CoinsArrondis),
            Padding = new Thickness(12, 6, 12, 6),
            Child = new TextBlock
            {
                Text = estActivee ? c.TexteOn : c.TexteOff,
                FontFamily = new FontFamily(c.Police),
                FontSize = c.TailleTexte,
                Foreground = ColorHelper.VersBrush(estActivee ? c.CouleurTexteOn : c.CouleurTexteOff, Colors.White)
            }
        };
        w.Opacity = c.Opacite;
    }

    /// <summary>Ferme et oublie tous les badges d'une touche.</summary>
    public void Fermer(int vk)
    {
        if (_fenetres.Remove(vk, out var liste))
            foreach (var w in liste) w.Close();
    }

    /// <summary>Ferme tous les badges (arrêt de l'application).</summary>
    public void ToutFermer()
    {
        foreach (var liste in _fenetres.Values)
            foreach (var w in liste) w.Close();
        _fenetres.Clear();
    }

    private sealed class BadgeWindow : ClickThroughWindow { }
}
