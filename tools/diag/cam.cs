using Ski.Game;
using UnityEngine;
// A free camera on the current run. pose.txt holds "x dz h  tx tdz th": x across the run, dz past
// the finish line (negative is up the slope), h above the snow; then the point it looks at.
string[] a = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../Screenshots/pose.txt")).Trim().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
float F(int i) => float.Parse(a[i], System.Globalization.CultureInfo.InvariantCulture);
var run = GameApp.Instance.Run;
var c = (Ski.Core.PisteCourse)run.Session.Course;
run.InputEnabled = false;
run.Autopilot = null;
Ski.Game.Ui.Instance.HudLayer.style.display = UnityEngine.UIElements.DisplayStyle.None;
Vector3 At(float x, float dz, float h) { float z = c.FinishZ + dz; return new Vector3(x, c.SnowHeight(x, z) + h, z); }
var main = Camera.main;
var go = new GameObject("Diag camera");
var cam = go.AddComponent<Camera>();
cam.CopyFrom(main);
cam.depth = main.depth + 10;
cam.farClipPlane = 2000f;
var data = main.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
var mine = go.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() ?? go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
if (data != null) { mine.renderPostProcessing = data.renderPostProcessing; mine.antialiasing = data.antialiasing; }
main.tag = "Untagged";
main.enabled = false;
go.tag = "MainCamera";
go.transform.position = At(F(0), F(1), F(2));
go.transform.LookAt(At(F(3), F(4), F(5)));
return "cam at " + go.transform.position;
