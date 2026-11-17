using Ski.Game;
using UnityEngine;
// Turns the run camera: pose.txt holds "yaw pitch [third]" (degrees; yaw up to +-120 from ahead).
string[] a = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../Screenshots/pose.txt")).Trim().Split(' ');
var run = GameApp.Instance.Run;
run.InputEnabled = false;
Ski.Game.Ui.Instance.HudLayer.style.display = UnityEngine.UIElements.DisplayStyle.None;
run.Rig.Look = new Vector2(float.Parse(a[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture));
run.Rig.SetThirdPerson(a.Length > 2 && a[2] == "third");
return "look " + run.Rig.Look;
