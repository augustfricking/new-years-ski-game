# Stages a real crash of one kind during a kept run (tools/diag/crash_test.cs) and captures frames in slow motion,
# from the moment the run finds the crash. Usage: tools/diag/crash_shots.ps1 -Kind Person|Trees|HardLanding|BadLanding [-Frames 5] [-Scale 0.05]
param([string]$Kind = "Person", [int]$Frames = 5, [double]$Scale = 0.05)
$root = Resolve-Path "$PSScriptRoot\..\.."
$u = "$root\tools\u.ps1"
Set-Content "$root\SkiGame\Screenshots\pose.txt" $Kind
$inv = [System.Globalization.CultureInfo]::InvariantCulture
# Slow first, so the crash can be caught and photographed (each capture takes a moment).
& $u command script eval ("UnityEngine.Time.timeScale = " + $Scale.ToString($inv) + "f; return 1;") | Out-Null
& $u command script eval --file "$root\tools\diag\crash_test.cs" | Select-Object -Last 1
$state = 'var rc = UnityEngine.Object.FindFirstObjectByType<Ski.Game.RunController>(); return "crashed " + rc.Session.Sim.Crashed + " limp " + rc.Rig.Limp + " reason " + rc.Session.Sim.LastCrash + " hit " + rc.Session.PeopleHit;'
for ($w = 0; $w -lt 30; $w++) { $st = (& $u command script eval $state | Select-Object -Last 1); if ($st -match "limp True") { break } }
"found: $st"
foreach ($k in 1..$Frames) {
    & $u command capture game_view -- --source camera --output-path "Screenshots/crash_$($Kind.ToLower())_$k.png" | Out-Null
    & $u command script eval $state | Select-Object -Last 1
}
& $u command script eval 'UnityEngine.Time.timeScale = 1f; return 1;' | Out-Null
