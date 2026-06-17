using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using IndicateurVerrouTouche.Core;

namespace IndicateurVerrouTouche.Services;

/// <summary>
/// Désinstallation propre : ferme l'application, retire le démarrage Windows, l'entrée
/// « Applications et fonctionnalités » et les icônes épinglées, supprime la configuration
/// (%AppData%) et l'installation locale (%LocalAppData%). Le dossier local contenant l'exe en
/// cours est supprimé en différé par un script qui attend la fermeture du processus.
/// </summary>
public static class Desinstallation
{
    private const string CleUninstall = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\IndicateurVerrouTouche";

    /// <summary>Inscrit l'application dans « Applications et fonctionnalités » (bouton Désinstaller natif).</summary>
    public static void EnregistrerDansProgrammes()
    {
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(CleUninstall);
            var exe = Environment.ProcessPath!;
            k.SetValue("DisplayName", "Indicateur Verrou Touche");
            k.SetValue("DisplayVersion", MiseAJour.VersionActuelle);
            k.SetValue("Publisher", "GUYON Valentin");
            k.SetValue("DisplayIcon", exe);
            k.SetValue("UninstallString", $"\"{exe}\" --desinstaller");
            k.SetValue("InstallLocation", Path.GetDirectoryName(exe)!);
            k.SetValue("NoModify", 1, RegistryValueKind.DWord);
            k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }
        catch { /* best-effort */ }
    }

    /// <summary>Exécute la désinstallation après confirmation. Retourne true si elle a été menée.</summary>
    public static bool Executer()
    {
        if (System.Windows.MessageBox.Show(
                "Désinstaller Indicateur Verrou Touche ?\n\nLes réglages et l'installation locale seront supprimés.",
                "Désinstallation", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning)
            != System.Windows.MessageBoxResult.Yes)
            return false;

        TuerAutresInstances();
        try { DemarrageWindows.Definir(false); } catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree(CleUninstall, throwOnMissingSubKey: false); } catch { }
        NettoyerNotifyIconSettings();
        try { Directory.Delete(DossierConfig, recursive: true); } catch { /* config déjà absente / verrouillée */ }
        SupprimerInstallLocaleDiffere();

        System.Windows.MessageBox.Show("Désinstallation terminée.", "Indicateur Verrou Touche",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        return true;
    }

    private static string DossierConfig => Path.GetDirectoryName(ConfigService.CheminParDefaut())!;

    private static void TuerAutresInstances()
    {
        var moi = Environment.ProcessId;
        foreach (var p in Process.GetProcessesByName("IndicateurVerrouTouche"))
        {
            if (p.Id == moi) continue;
            try { p.Kill(); p.WaitForExit(3000); } catch { /* déjà fermé / accès refusé */ }
        }
    }

    private static void NettoyerNotifyIconSettings()
    {
        try
        {
            var exe = Environment.ProcessPath;
            using var racine = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings", writable: true);
            if (racine is null || exe is null) return;
            foreach (var nom in racine.GetSubKeyNames())
            {
                bool match;
                using (var k = racine.OpenSubKey(nom))
                    match = k?.GetValue("ExecutablePath") is string c && string.Equals(c, exe, StringComparison.OrdinalIgnoreCase);
                if (match) { try { racine.DeleteSubKeyTree(nom, throwOnMissingSubKey: false); } catch { } }
            }
        }
        catch { /* best-effort */ }
    }

    // Le dossier local contient l'exe en cours d'exécution : on délègue sa suppression à un script
    // qui attend la fermeture du processus avant de supprimer.
    private static void SupprimerInstallLocaleDiffere()
    {
        try
        {
            var dossier = Path.GetDirectoryName(InstallationLocale.CheminLocal)!;
            int pid = Environment.ProcessId;
            var script = $"$ErrorActionPreference='SilentlyContinue'; Wait-Process -Id {pid} -Timeout 30; " +
                         $"Start-Sleep -Milliseconds 300; Remove-Item -Recurse -Force '{dossier}'";
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{script}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch { /* best-effort */ }
    }
}
