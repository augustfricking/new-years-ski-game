# Wrapper for the Unity CLI, pinned to this repo's project. Usage: tools\u.ps1 <unity args...>
$env:UNITY_PROJECT_PATH = (Resolve-Path "$PSScriptRoot\..\SkiGame").Path
& "$env:LOCALAPPDATA\Unity\bin\unity.exe" @args 2>$null
exit $LASTEXITCODE
