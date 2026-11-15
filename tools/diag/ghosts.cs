using Ski.Core;
using Ski.Game;
using UnityEngine;

// Reports where the ghosts are relative to the player, and frames the nearest one.
var run = GameApp.Instance.Run;
var sim = run.Session.Sim;
var root = run.transform.Find("Ghosts");
string report = "player z=" + sim.Pos.z.ToString("0") + " phase=" + run.Session.Phase + " |";
Transform nearest = null; float best = 1e9f;
foreach (Transform g in root)
{
    report += " " + g.name + (g.gameObject.activeSelf ? "" : "(hidden)") + " z=" + g.position.z.ToString("0") + " x=" + g.position.x.ToString("0");
    float d = (g.position - sim.Pos).magnitude;
    if (g.gameObject.activeSelf && d < best) { best = d; nearest = g; }
}
if (nearest != null)
{
    run.Paused = true;
    var cam = run.Rig.Cam;
    Ui.Instance.ShowGoggles(false);
    run.enabled = false;   // freeze the run so the camera stays where it is put
    // Stand a few metres downhill of the ghost and look back at it.
    cam.transform.position = nearest.position + new Vector3(1.5f, 1.5f, 4.5f);
    cam.transform.LookAt(nearest.position + Vector3.up * 1.3f);
    cam.fieldOfView = 50f;
}
return report;
