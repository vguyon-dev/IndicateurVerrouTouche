using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using IndicateurVerrouTouche.Core;

namespace IndicateurVerrouTouche.Services;

/// <summary>Informations de mise à jour lues depuis le manifeste « maj.json » du partage.</summary>
public sealed record InfoMaJ(string Version, string Fichier, string Notes, bool Forcer);

/// <summary>
/// Mise à jour depuis un partage réseau (UNC). Le dossier partagé contient un manifeste
/// « maj.json » (<c>{ "version": "2026.06.12a", "fichier": "IndicateurVerrouTouche.exe", "notes": "…" }</c>)
/// et le nouvel exécutable. L'installation rapatrie l'exe en local puis délègue le remplacement
/// à un script qui attend la fermeture de l'application, écrase l'ancien exe et le relance.
/// Les versions suivent le format Année.Mois.Jour + lettre (voir <see cref="VersionLogicielle"/>).
/// </summary>
public static class MiseAJour
{
    private const string Manifeste = "maj.json";
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Version de l'exécutable en cours (InformationalVersion, ex. « 2026.06.11a »).</summary>
    public static string VersionActuelle =>
        (Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
         ?? "0.0.0").Split('+')[0];

    /// <summary>Lit le manifeste sur le partage. Retourne null si non configuré, injoignable ou illisible.</summary>
    public static InfoMaJ? Verifier(string? baseUnc)
    {
        if (string.IsNullOrWhiteSpace(baseUnc)) return null;
        try
        {
            var chemin = Path.Combine(baseUnc, Manifeste);
            if (!File.Exists(chemin)) return null;
            var dto = JsonSerializer.Deserialize<ManifesteDto>(File.ReadAllText(chemin), Options);
            if (dto is null || !VersionLogicielle.EstValide(dto.Version)) return null;
            var fichier = string.IsNullOrWhiteSpace(dto.Fichier) ? "IndicateurVerrouTouche.exe" : dto.Fichier;
            return new InfoMaJ(dto.Version, fichier, dto.Notes ?? "", dto.Forcer);
        }
        catch { return null; }   // partage indisponible / JSON invalide → on considère qu'il n'y a pas de MAJ
    }

    /// <summary>Vrai si le manifeste annonce une version strictement plus récente que l'exe courant.</summary>
    public static bool EstPlusRecente(InfoMaJ info) => VersionLogicielle.EstPlusRecente(info.Version, VersionActuelle);

    /// <summary>
    /// Copie le nouvel exe depuis le partage, puis lance un script PowerShell qui attend la
    /// fermeture du processus courant, remplace l'exe et le relance. Appelle <paramref name="quitter"/>
    /// pour libérer l'exécutable. Peut lever (partage injoignable…) : à entourer d'un try/catch.
    /// </summary>
    public static void Installer(InfoMaJ info, string baseUnc, Action quitter)
    {
        var source = Path.Combine(baseUnc, info.Fichier);
        var exeActuel = Environment.ProcessPath!;
        var nouveau = exeActuel + ".new";
        File.Copy(source, nouveau, overwrite: true);   // rapatrie le nouvel exe en local (peut lever)

        int pid = Environment.ProcessId;
        // Le script attend la fin du process, remplace l'exe (avec quelques tentatives), puis relance.
        var script =
            $"$ErrorActionPreference='SilentlyContinue'; Wait-Process -Id {pid} -Timeout 60; " +
            $"for($i=0;$i -lt 20;$i++){{ Move-Item -Force '{nouveau}' '{exeActuel}'; if($?){{break}}; Start-Sleep -Milliseconds 500 }}; " +
            $"Start-Process '{exeActuel}'";
        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{script}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });
        quitter();
    }

    private sealed class ManifesteDto
    {
        public string Version { get; set; } = "";
        public string Fichier { get; set; } = "";
        public string? Notes { get; set; }
        public bool Forcer { get; set; }   // true = mise à jour imposée, sans confirmation ni attente
    }
}
