# Runs EditMode tests in the running Editor. Usage: tools\test.ps1 [filter]
param([string]$Filter = "")
$u = "$PSScriptRoot\u.ps1"
& $u command console clear | Out-Null
if ($Filter) { $lines = & $u command test run --timeout 900 --format json -- --mode editor --filter $Filter --timeout 800 }
else { $lines = & $u command test run --timeout 900 --format json -- --mode editor --timeout 800 }
$pass = 0; $fail = 0
foreach ($line in $lines) {
    try { $t = $line | ConvertFrom-Json } catch { $line; continue }
    if ($t.status -eq "Passed") { $pass++ }
    elseif ($t.fullName) {
        $fail++
        "FAIL " + $t.fullName
        if ($t.message) { "     " + ($t.message.Trim() -replace "`r?`n", "`n     ") }
    }
    else { $line }
}
"passed=$pass failed=$fail"
# Lines the tests logged
$log = (& $u command console get --format json -- --tail 80 --level log) -join "`n"
try {
    $lj = $log | ConvertFrom-Json
    $entries = if ($lj.data.entries) { $lj.data.entries } else { $lj.entries }
    $entries | Where-Object { $_.level -eq "log" } | ForEach-Object { "LOG  " + $_.message }
} catch {}
if ($fail -gt 0) { exit 1 }
