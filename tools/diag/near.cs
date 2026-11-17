using UnityEngine;
// Names the things drawn around the player that float above the snow (for finding stray props).
var run = Ski.Game.GameApp.Instance.Run;
Vector3 me = run.Session.Sim.Pos;
var c = run.Session.Course;
var sb = new System.Text.StringBuilder();
var seen = new System.Collections.Generic.HashSet<string>();
foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
{
    if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
    var b = r.bounds;
    if ((b.center - me).magnitude > 60f) continue;
    float ground = c.Height(b.center.x, b.center.z);
    if (b.min.y - ground < 0.8f) continue;
    if (b.size.magnitude > 40f) continue;
    string path = r.transform.parent != null ? r.transform.parent.name + "/" + r.name : r.name;
    if (!seen.Add(path + (b.center - me).ToString("0"))) continue;
    sb.Append(path).Append(" at ").Append((b.center - me).ToString("0.0")).Append(" size ").Append(b.size.ToString("0.0")).Append(" above ").Append((b.min.y - ground).ToString("0.0")).Append('\n');
}
return sb.ToString();
