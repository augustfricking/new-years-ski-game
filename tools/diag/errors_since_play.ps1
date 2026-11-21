# Prints the distinct errors and exceptions logged since Play mode last started (first line of each, with a count).
$log = Join-Path $PSScriptRoot "..\..\SkiGame\Logs\editor-headless.log"
$L = (Select-String -Path $log -Pattern "Entering Playmode" | Select-Object -Last 1).LineNumber
$lines = Get-Content $log | Select-Object -Skip $L
$hits = $lines | Select-String -Pattern "Exception:|^Error|error CS|\[Error\]|NullReference|IndexOutOfRange|ArgumentException|KeyNotFound|InvalidOperation" |
        ForEach-Object { $_.Line.Trim() } | Where-Object { $_ -notmatch "Curl error|Could not resolve host|websocket|Wire|Lobby events" }
if (-not $hits) { "errors: 0"; return }
$hits | Group-Object | Sort-Object Count -Descending | Select-Object -First 12 | ForEach-Object { "{0,5}x  {1}" -f $_.Count, $_.Name.Substring(0, [Math]::Min(180, $_.Name.Length)) }
