using Microsoft.Win32;

namespace IndicateurVerrouTouche.Services;

/// <summary>
/// Épingle (promeut) les icônes de l'application dans la zone de notification visible plutôt que
/// dans le menu de débordement. Repose sur la clé Windows 11 « Control Panel\NotifyIconSettings »
/// (valeur IsPromoted=1). Sans effet sur Windows 10 (clé absente → no-op). Best-effort.
/// </summary>
public static class EpinglageSystray
{
    /// <summary>Marque comme épinglées toutes les icônes de notification de l'exécutable courant.</summary>
    public static void Promouvoir()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return;
            using var racine = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings", writable: true);
            if (racine is null) return;   // Windows < 11 : la clé n'existe pas
            foreach (var nom in racine.GetSubKeyNames())
            {
                using var k = racine.OpenSubKey(nom, writable: true);
                if (k?.GetValue("ExecutablePath") is string chemin &&
                    string.Equals(chemin, exe, StringComparison.OrdinalIgnoreCase))
                {
                    k.SetValue("IsPromoted", 1, RegistryValueKind.DWord);
                }
            }
        }
        catch { /* best-effort : l'épinglage dépend de la version de Windows */ }
    }
}
