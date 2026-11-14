# Enters Play mode in the headless Editor, starts a run with the bot driving, waits and takes a screenshot.
# Usage: tools\play.ps1 -Slope Green -Who Erik -Seconds 6 -Shot green1 [-Eval "<C# to run before the shot>"] [-Keep]
param([string]$Slope = "Green", [string]$Who = "Erik", [double]$Seconds = 5, [string]$Shot = "shot", [string]$Eval = "", [switch]$Keep, [switch]$NoBot)
$u = "$PSScriptRoot\u.ps1"
$st = (& $u command editor status) -join " "
if ($st -notmatch "playMode:\s*playing|isPlaying:\s*true") {
    & $u command script eval 'return Ski.EditorTools.ProjectSetup.OpenMain();' | Out-Null
    & $u command editor set_auto_tick | Out-Null
    & $u command editor.playmode start | Out-Null
    Start-Sleep -Seconds 2
    & $u status --until-ready --timeout 120 | Out-Null
}
$bot = if ($NoBot) { "" } else { "app.Run.Autopilot = new Ski.Core.Bot(app.Run.Session);" }
& $u command script eval "var app = Ski.Game.GameApp.Instance; app.StartRun(Ski.Core.SlopeId.$Slope, Ski.Core.CharacterId.$Who); $bot return app.Run.Session.Course.Name;"
Start-Sleep -Milliseconds ([int]($Seconds * 1000))
if ($Eval) { & $u command script eval $Eval }
& $u command capture game_view -- --source camera --output-path "Screenshots/$Shot.png" | Select-String savedPath
$errs = (& $u command console get --format json -- --tail 30 --level error) -join "`n"
try {
    $j = $errs | ConvertFrom-Json
    $entries = if ($j.data.entries) { $j.data.entries } else { $j.entries }
    $entries | ForEach-Object { "ERR " + $_.message.Substring(0, [Math]::Min(400, $_.message.Length)) } | Sort-Object -Unique | Select-Object -First 12
} catch {}
if (-not $Keep) { & $u command editor.playmode stop | Out-Null }
