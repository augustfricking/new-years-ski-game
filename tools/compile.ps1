# Recompiles the Unity project in the running Editor and prints compile errors, if any.
$u = "$PSScriptRoot\u.ps1"
& $u command script eval 'UnityEditor.AssetDatabase.Refresh(); return 1;' | Out-Null
Start-Sleep -Seconds 2
& $u status --until-ready --timeout 300 | Out-Null
$deadline = (Get-Date).AddSeconds(240)
do {
    $st = (& $u command script recompile_status) -join " "
    if ($st -match "compiling|triggered") { Start-Sleep -Seconds 2; & $u status --until-ready --timeout 300 | Out-Null } else { break }
} while ((Get-Date) -lt $deadline)
$cs = (& $u command console status) -join "`n"
if ($cs -match "compilationFailed.{0,4}true") {
    "COMPILE FAILED"
    $raw = (& $u command console get --format json -- --tail 80 --level error) -join "`n"
    $j = $raw | ConvertFrom-Json
    $entries = if ($j.data.entries) { $j.data.entries } else { $j.entries }
    $entries | Where-Object { $_.message -match "error CS" } | ForEach-Object { $_.message } | Sort-Object -Unique
    exit 1
}
"COMPILE OK"
