using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using IndicateurVerrouTouche.Core;

namespace IndicateurVerrouTouche.Services;

/// <summary>Affiche une incrustation fugitive (fondu entrée → maintien → fondu sortie) par bascule.</summary>
public sealed class OsdService
{
    private readonly ConfigService _config;
    public OsdService(ConfigService config) => _config = config;

    /// <summary>Affiche l'incrustation correspondant à l'état d'une touche, si son OSD est actif.</summary>
    public void Afficher(int vk, bool estActivee)
    {
        var touche = _config.Current.Touches.FirstOrDefault(t => t.Vk == vk);
        if (touche is null || !touche.Osd.Actif) return;
        Afficher(touche.Osd, estActivee);
    }

    /// <summary>
    /// Affiche directement une incrustation à partir d'une config OSD donnée, sans lire Current
    /// ni rien persister. Sert à l'aperçu des réglages : montre la copie de travail en cours d'édition.
    /// </summary>
    public void Afficher(OsdConfig c, bool estActivee)
    {
        // Une incrustation par écran cible (« tous » → un OSD sur chaque écran).
        foreach (var ecran in PositionneurEcran.EcransCibles(c.EcranCible))
        {
            var fenetre = new OsdWindow();
            ConfigurerContenu(fenetre, c, estActivee);
            fenetre.Show();                   // Show d'abord pour mesurer la taille réelle…
            fenetre.UpdateLayout();
            PositionneurEcran.Placer(fenetre, ecran, c.Ancrage, c.DecalageX, c.DecalageY);
            Animer(fenetre, c);               // …puis on anime l'opacité
        }
    }

    private static void ConfigurerContenu(OsdWindow w, OsdConfig c, bool on)
    {
        var bordure = new Border
        {
            Background = ColorHelper.VersBrush(on ? c.CouleurFondOn : c.CouleurFondOff, Colors.Black),
            CornerRadius = new CornerRadius(c.CoinsArrondis),
            Padding = new Thickness(24, 14, 24, 14),
            Child = new TextBlock
            {
                Text = on ? c.TexteOn : c.TexteOff,
                FontFamily = new FontFamily(c.Police),
                FontSize = c.TailleTexte,
                FontWeight = FontWeights.SemiBold,
                Foreground = ColorHelper.VersBrush(on ? c.CouleurTexteOn : c.CouleurTexteOff, Colors.White)
            }
        };
        w.Content = bordure;
        w.Opacity = 0;                        // démarre invisible, l'animation prend le relais
        w.OpaciteCible = c.Opacite;
    }

    private static void Animer(OsdWindow w, OsdConfig c)
    {
        var entree = new DoubleAnimation(0, w.OpaciteCible, TimeSpan.FromMilliseconds(c.FonduEntreeMs));
        // Pas de BeginTime ici : le DispatcherTimer ci-dessous fournit déjà le délai de maintien.
        // Le cumuler avec un BeginTime appliquerait le maintien deux fois (OSD ~2× trop long + clignotement).
        var sortie = new DoubleAnimation(w.OpaciteCible, 0, TimeSpan.FromMilliseconds(c.FonduSortieMs));
        sortie.Completed += (_, _) => w.Close();   // fermeture propre en fin de fondu
        w.BeginAnimation(UIElement.OpacityProperty, entree);
        // Fondu de sortie déclenché après le fondu d'entrée + le temps de maintien.
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(c.FonduEntreeMs + c.DureeMs) };
        t.Tick += (_, _) => { t.Stop(); w.BeginAnimation(UIElement.OpacityProperty, sortie); };
        t.Start();
    }

    /// <summary>Fenêtre OSD concrète (click-through héritée).</summary>
    private sealed class OsdWindow : ClickThroughWindow { public double OpaciteCible { get; set; } = 0.9; }
}
