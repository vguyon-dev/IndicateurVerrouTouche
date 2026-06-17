using System.Diagnostics;
using System.IO;

namespace IndicateurVerrouTouche.Services;

/// <summary>
/// Auto-installation : le partage réseau ne sert qu'à DISTRIBUER l'exe. Comme l'auto-mise-à-jour
/// remplace l'exécutable en cours, l'app ne doit pas tourner depuis le partage (fichier commun,
/// verrouillé par les autres). Si elle est lancée depuis un emplacement réseau, elle se copie dans
/// %LocalAppData% et redémarre depuis cette copie locale (que les mises à jour pourront remplacer).
/// </summary>
public static class InstallationLocale
{
    /// <summary>Dossier d'installation locale de l'application.</summary>
    public static string DossierLocal => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IndicateurVerrouTouche");

    /// <summary>Emplacement local d'installation de l'exécutable.</summary>
    public static string CheminLocal => Path.Combine(DossierLocal, "IndicateurVerrouTouche.exe");

    /// <summary>
    /// Fichier mémorisant le dossier réseau d'origine (d'où l'exe a été distribué et lancé) ;
    /// écrit lors de l'auto-installation locale et conservé à travers les mises à jour.
    /// </summary>
    private static string CheminSourceMaj => Path.Combine(DossierLocal, "source-maj.txt");

    /// <summary>
    /// Si l'exe tourne depuis un emplacement réseau, le copie en local et relance la copie.
    /// Retourne true si une copie locale a été lancée (l'appelant doit alors se fermer).
    /// </summary>
    public static bool InstallerEtRelancerSiReseau()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe) || !EstReseau(exe)) return false;
        try
        {
            var dest = CheminLocal;
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            // Mémorise le dossier réseau d'origine : la copie locale saura y chercher les mises à
            // jour (le partage de distribution contient l'exe ET le manifeste maj.json).
            try { File.WriteAllText(CheminSourceMaj, Path.GetDirectoryName(exe)!); } catch { /* best-effort */ }
            // Copie best-effort : si la copie locale est déjà ouverte (instance installée en cours),
            // on ne l'écrase pas et on se contente de la lancer.
            try { File.Copy(exe, dest, overwrite: true); }
            catch (IOException) { }
            Process.Start(new ProcessStartInfo { FileName = dest, UseShellExecute = true });
            return true;
        }
        catch { return false; }   // échec : on laisse l'app tourner depuis le réseau (mode dégradé)
    }

    /// <summary>
    /// Dossier où chercher les mises à jour par défaut (aucun chemin configuré explicitement),
    /// pour que l'auto-MAJ fonctionne quel que soit l'endroit d'extraction / de lancement :
    /// le dossier réseau d'origine mémorisé lors de l'auto-installation, sinon le dossier de
    /// l'exécutable en cours (cas d'un dossier extrait et lancé sur place). Null si indéterminable.
    /// </summary>
    public static string? DossierMajParDefaut()
    {
        try
        {
            if (File.Exists(CheminSourceMaj))
            {
                var src = File.ReadAllText(CheminSourceMaj).Trim();
                if (!string.IsNullOrWhiteSpace(src)) return src;
            }
        }
        catch { /* sidecar illisible : on retombe sur le dossier de l'exe */ }
        var exe = Environment.ProcessPath;
        return string.IsNullOrEmpty(exe) ? null : Path.GetDirectoryName(exe);
    }

    /// <summary>Vrai si le chemin est sur un partage UNC ou un lecteur réseau mappé.</summary>
    private static bool EstReseau(string chemin)
    {
        if (chemin.StartsWith(@"\\", StringComparison.Ordinal)) return true;
        try
        {
            var racine = Path.GetPathRoot(chemin);
            if (!string.IsNullOrEmpty(racine) && racine.Length >= 2 && racine[1] == ':')
                return new DriveInfo(racine).DriveType == DriveType.Network;
        }
        catch { /* lecteur inaccessible : on considère que ce n'est pas réseau */ }
        return false;
    }
}
