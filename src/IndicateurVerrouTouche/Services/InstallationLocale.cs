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
    /// <summary>Emplacement local d'installation de l'exécutable.</summary>
    public static string CheminLocal => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IndicateurVerrouTouche", "IndicateurVerrouTouche.exe");

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
            // Copie best-effort : si la copie locale est déjà ouverte (instance installée en cours),
            // on ne l'écrase pas et on se contente de la lancer.
            try { File.Copy(exe, dest, overwrite: true); }
            catch (IOException) { }
            Process.Start(new ProcessStartInfo { FileName = dest, UseShellExecute = true });
            return true;
        }
        catch { return false; }   // échec : on laisse l'app tourner depuis le réseau (mode dégradé)
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
