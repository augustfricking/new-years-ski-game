# Plays the lift cutscene in the headless Editor and saves a screenshot at chosen beats.
# Usage: tools\cutshots.ps1 -Beats 9,11,15 -Prefix cut
param([int[]]$Beats = @(2, 9, 11, 15), [string]$Prefix = "cut", [string]$Who = "Erik")
$u = "$PSScriptRoot\u.ps1"
& "$PSScriptRoot\play.ps1" -Slope None -Seconds 2 -Shot tmp -Keep | Out-Null
& $u command script eval "Ski.Game.GameApp.Instance.Lift.OnSelect(Ski.Core.CharacterId.$Who); return 1;" | Out-Null
foreach ($b in $Beats) {
    for ($i = 0; $i -lt 200; $i++) {
        $r = (& $u command script eval 'var l = Ski.Game.GameApp.Instance.Lift; return l == null ? 99 : l.BeatIndex;') -join " "
        if ($r -match "(\d+)\s*$" -and [int]$Matches[1] -ge $b) { break }
        Start-Sleep -Milliseconds 400
    }
    Start-Sleep -Milliseconds 900
    & $u command capture game_view -- --source camera --output-path "Screenshots/$Prefix$b.png" | Select-String savedPath
}
& $u command editor.playmode stop | Out-Null
