# Publie un exe léger (framework-dependent, single-file).
# PRÉREQUIS sur le poste cible : .NET 8 Desktop Runtime (windowsdesktop) installé.
# Robuste au répertoire courant : tout est résolu depuis l'emplacement du script.
$ErrorActionPreference = 'Stop'
$racine = Split-Path $PSScriptRoot -Parent

dotnet publish "$racine/src/IndicateurVerrouTouche" -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true -o "$racine/dist"

Write-Host "Exécutable généré dans $racine\dist\IndicateurVerrouTouche.exe"
