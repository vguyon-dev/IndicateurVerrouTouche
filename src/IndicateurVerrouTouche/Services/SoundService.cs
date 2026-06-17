using System.IO;
using System.Media;
using System.Windows.Media;
using IndicateurVerrouTouche.Core;

namespace IndicateurVerrouTouche.Services;

/// <summary>
/// Joue un son distinct ON/OFF par touche. Chemin de fichier (WAV/MP3) lu via MediaPlayer
/// (volume réglable), "systeme" → bip système, vide → silence. Asset manquant ignoré.
/// </summary>
public sealed class SoundService
{
    private readonly ConfigService _config;
    private readonly MediaPlayer _player = new();  // réutilisé ; créé sur le thread UI
    private readonly string _dossierBase = AppContext.BaseDirectory;

    public SoundService(ConfigService config) => _config = config;

    /// <summary>Joue le son correspondant à l'état d'une touche, si son retour sonore est actif.</summary>
    public void Jouer(int vk, bool estActivee)
    {
        var touche = _config.Current.Touches.FirstOrDefault(t => t.Vk == vk);
        if (touche is null || !touche.Son.Actif) return;
        var c = touche.Son;
        var source = estActivee ? c.SonOn : c.SonOff;
        if (string.IsNullOrWhiteSpace(source)) return;

        if (source.Equals("systeme", StringComparison.OrdinalIgnoreCase)) { SystemSounds.Asterisk.Play(); return; }

        var chemin = Path.IsPathRooted(source) ? source : Path.Combine(_dossierBase, source);
        if (!File.Exists(chemin)) { System.Diagnostics.Debug.WriteLine($"Son introuvable : {chemin}"); return; }

        _player.Volume = Math.Clamp(c.Volume, 0, 1);
        _player.Open(new Uri(chemin, UriKind.Absolute));
        _player.Play();
    }
}
