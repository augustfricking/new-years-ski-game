# Runs one of the views in tools\diag on the menu (lift) scene and saves a screenshot.
# Usage: tools\diag.ps1 -File face -With "2 0.8 0.25" -Shot name    (the view reads its arguments from Screenshots\pose.txt)
param([string]$File = "face", [string]$With = "", [string]$Shot = "diag", [double]$Wait = 1.5)
$u = "$PSScriptRoot\u.ps1"
Set-Content -Path "$PSScriptRoot\..\SkiGame\Screenshots\pose.txt" -Value $With
& "$PSScriptRoot\play.ps1" -Slope None -Seconds 2 -Shot _menu -Keep | Out-Null
& $u command script eval "Ski.Game.SaveData.NameAsked = true; Ski.Game.Dialogs.Close(); return 1;" | Out-Null
& $u command script eval --file (Resolve-Path "$PSScriptRoot\diag\$File.cs").Path
Start-Sleep -Milliseconds ([int]($Wait * 1000))
& $u command capture game_view -- --source camera --output-path "Screenshots/$Shot.png" | Select-String savedPath
