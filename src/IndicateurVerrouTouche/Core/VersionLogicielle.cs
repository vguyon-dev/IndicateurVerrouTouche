namespace IndicateurVerrouTouche.Core;

/// <summary>
/// Version au format Année.Mois.Jour + suffixe lettre (ex. <c>2026.06.11a</c>). La lettre
/// s'incrémente au fil des versions d'une même journée (a→b→…→z→aa→ab→…) et repart à « a »
/// chaque nouveau jour. La comparaison se fait d'abord par date, puis par le suffixe lettre.
/// </summary>
public static class VersionLogicielle
{
    /// <summary>Vrai si la chaîne est une version exploitable (date valide + format Année.Mois.Jour…).</summary>
    public static bool EstValide(string? version) => Decouper(version).date != DateOnly.MinValue;

    /// <summary>Compare deux versions : &lt;0 si a est antérieure, 0 si égales, &gt;0 si a est postérieure.</summary>
    public static int Comparer(string? a, string? b)
    {
        var (da, la) = Decouper(a);
        var (db, lb) = Decouper(b);
        int c = da.CompareTo(db);
        return c != 0 ? c : ComparerLettres(la, lb);
    }

    /// <summary>Vrai si <paramref name="candidate"/> est strictement plus récente que <paramref name="reference"/>.</summary>
    public static bool EstPlusRecente(string? candidate, string? reference) => Comparer(candidate, reference) > 0;

    /// <summary>Décompose "2026.06.11a" en (date, lettres). Format invalide → DateOnly.MinValue (toujours la plus ancienne).</summary>
    private static (DateOnly date, string lettres) Decouper(string? version)
    {
        var v = (version ?? "").Split('+')[0].Trim();   // ignore un éventuel +metadata d'assembly
        var parts = v.Split('.');
        if (parts.Length < 3) return (DateOnly.MinValue, "");
        if (!int.TryParse(parts[0], out var annee) || !int.TryParse(parts[1], out var mois)) return (DateOnly.MinValue, "");

        var troisieme = parts[2];
        int i = 0;
        while (i < troisieme.Length && char.IsDigit(troisieme[i])) i++;
        if (i == 0 || !int.TryParse(troisieme[..i], out var jour)) return (DateOnly.MinValue, "");
        var lettres = troisieme[i..].ToLowerInvariant();

        try { return (new DateOnly(annee, mois, jour), lettres); }
        catch { return (DateOnly.MinValue, lettres); }   // jour/mois hors plage
    }

    /// <summary>a &lt; b &lt; … &lt; z &lt; aa &lt; ab … : la longueur prime, puis l'ordre alphabétique.</summary>
    private static int ComparerLettres(string a, string b)
    {
        if (a.Length != b.Length) return a.Length.CompareTo(b.Length);
        return string.CompareOrdinal(a, b);
    }
}
