using Microsoft.Win32;

namespace IndicateurVerrouTouche.Services;

/// <summary>Gère la clé Run de l'utilisateur courant (démarrage automatique, sans droits admin).</summary>
public static class DemarrageWindows
{
    private const string Cle = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Nom = "IndicateurVerrouTouche";

    /// <summary>Inscrit ou retire l'exécutable courant du démarrage automatique de l'utilisateur.</summary>
    public static void Definir(bool actif)
    {
        // CreateSubKey ouvre en écriture ET crée la clé Run si elle est absente (évite une NRE).
        using var k = Registry.CurrentUser.CreateSubKey(Cle)!;
        if (actif)
        {
            var exe = Environment.ProcessPath!;  // chemin de l'exe en cours
            k.SetValue(Nom, $"\"{exe}\"");
        }
        else if (k.GetValue(Nom) != null)
        {
            k.DeleteValue(Nom);
        }
    }

    /// <summary>Indique si l'application est inscrite au démarrage automatique.</summary>
    public static bool EstActif()
    {
        using var k = Registry.CurrentUser.OpenSubKey(Cle);
        return k?.GetValue(Nom) != null;
    }
}
