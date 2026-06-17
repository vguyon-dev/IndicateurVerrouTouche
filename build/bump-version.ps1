# Incrémente la version applicative (format Année.Mois.Jour + lettre) dans le .csproj.
# Règle : nouveau jour  -> date du jour + 'a'
#         même jour     -> lettre suivante (a->b->…->z->aa->ab->…)
# Exemple : 2026.06.11b  (le 12)  ->  2026.06.12a
#           2026.06.11b  (le 11)  ->  2026.06.11c
$ErrorActionPreference = 'Stop'
$csproj = Join-Path $PSScriptRoot '..\src\IndicateurVerrouTouche\IndicateurVerrouTouche.csproj'
$contenu = Get-Content $csproj -Raw

if ($contenu -notmatch '<InformationalVersion>([^<]*)</InformationalVersion>') {
    throw "InformationalVersion introuvable dans $csproj"
}
$actuelle = $Matches[1]

function NextLetters([string]$l) {
    if ([string]::IsNullOrEmpty($l)) { return 'a' }
    $c = $l.ToCharArray()
    for ($i = $c.Length - 1; $i -ge 0; $i--) {
        if ($c[$i] -ne 'z') { $c[$i] = [char]([int][char]$c[$i] + 1); return -join $c }
        $c[$i] = 'a'   # retenue : on passe la lettre courante à 'a' et on propage
    }
    return 'a' + (-join $c)   # débordement (zz -> aaa)
}

$today = Get-Date
$dateStr = '{0:yyyy.MM.dd}' -f $today

$m = [regex]::Match($actuelle, '^(\d{4}\.\d{2}\.\d{2})([a-z]*)$')
$dateActuelle = if ($m.Success) { $m.Groups[1].Value } else { '' }
$lettres = if ($m.Success) { $m.Groups[2].Value } else { '' }

$nouvellesLettres = if ($dateActuelle -eq $dateStr) { NextLetters $lettres } else { 'a' }
$nouvelle = "$dateStr$nouvellesLettres"
$versionNum = '{0}.{1}.{2}' -f $today.Year, $today.Month, $today.Day   # version numérique pour l'assembly

$contenu = $contenu -replace '<InformationalVersion>[^<]*</InformationalVersion>', "<InformationalVersion>$nouvelle</InformationalVersion>"
$contenu = $contenu -replace '<Version>[^<]*</Version>', "<Version>$versionNum</Version>"
Set-Content $csproj $contenu -NoNewline -Encoding UTF8

Write-Host "Version : $actuelle  ->  $nouvelle"
