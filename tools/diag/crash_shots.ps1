# Sets up a crash of one kind during a kept run (tools/diag/crash_test.cs), forces it, and captures frames in slow motion.
# Usage: tools/diag/crash_shots.ps1 -Kind Person|Trees|HardLanding|BadLanding [-Force] [-Frames 5] [-Scale 0.12]
param([string]$Kind = "Person", [switch]$Force, [int]$Frames = 5, [double]$Scale = 0.12)
$root = Resolve-Path "$PSScriptRoot\..\.."
$u = "$root\tools\u.ps1"
$pose = "$root\SkiGame\Screenshots\pose.txt"
Set-Content $pose $Kind
& $u command script eval --file "$root\tools\diag\crash_test.cs" | Select-Object -Last 1
# Slow time first, so the crash lasts long enough to be photographed (each capture takes a moment).
$inv = [System.Globalization.CultureInfo]::InvariantCulture
& $u command script eval ("UnityEngine.Time.timeScale = " + $Scale.ToString($inv) + "f; return 1;") | Out-Null
if ($Force) { Set-Content $pose "$Kind go"; & $u command script eval --file "$root\tools\diag\crash_test.cs" | Select-Object -Last 1 }
foreach ($k in 1..$Frames) {
    & $u command capture game_view -- --source camera --output-path "Screenshots/crash_$($Kind.ToLower())_$k.png" | Out-Null
    & $u command script eval 'var rc = UnityEngine.Object.FindFirstObjectByType<Ski.Game.RunController>(); return "crashed " + rc.Session.Sim.Crashed + " limp " + rc.Rig.Limp + " reason " + rc.Session.Sim.LastCrash;' | Select-Object -Last 1
}
& $u command script eval 'UnityEngine.Time.timeScale = 1f; return 1;' | Out-Null
