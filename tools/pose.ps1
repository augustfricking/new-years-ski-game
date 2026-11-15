# Freezes a run in a situation and saves a screenshot. Usage: tools\pose.ps1 -Slope Red -What brake -Shot brake1 [-Who Erik]
# Situations: brake, carve, air (Blue), kids (Green), park (Blue), pipe (Blue), offpiste (OffPiste).
param([string]$Slope = "Red", [string]$Who = "Erik", [string]$What = "brake", [string]$Shot = "pose")
$u = "$PSScriptRoot\u.ps1"
& "$PSScriptRoot\play.ps1" -Slope $Slope -Who $Who -Seconds 0.5 -Shot tmp -Keep -NoBot | Out-Null
Set-Content -Path "$PSScriptRoot\..\SkiGame\Screenshots\pose.txt" -Value $What -NoNewline
& $u command script eval --file "$PSScriptRoot\diag\pose.cs"
Start-Sleep -Milliseconds 900
& $u command capture game_view -- --source camera --output-path "Screenshots/$Shot.png" | Select-String savedPath
