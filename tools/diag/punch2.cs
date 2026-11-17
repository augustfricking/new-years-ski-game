using Ski.Game;
using UnityEngine;

// Punches the guest at a bar stool with a chosen blow (pose.txt: "blow" 0 jab, 1 hook, 2 uppercut,
// 3 gut, 4 haymaker), with the viewer standing back to watch.
var t = GameApp.Instance.Tavern;
string[] a = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../Screenshots/pose.txt")).Trim().Split(' ');
int blow = int.Parse(a[0]);
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
t.DebugView(new Vector3(1.2f, 0f, -2.4f), 70f, 6f);
var guestsField = typeof(TavernScene).GetField("_guests", flags);
var guests = (System.Collections.IList)guestsField.GetValue(t);
object pick = null; float best = 1e9f;
foreach (var g in guests)
{
    var p = (Person)g.GetType().GetField("P").GetValue(g);
    var pos = t.transform.InverseTransformPoint(p.Hips.position);
    float d = (pos - new Vector3(3.75f, 0.9f, -3f)).sqrMagnitude;
    if (d < best) { best = d; pick = g; }
}
var blowType = typeof(TavernScene).GetNestedType("Blow", flags);
var launch = typeof(TavernScene).GetMethod("Launch", flags);
launch.Invoke(t, new object[] { pick, new Vector3(1f, 0f, 0.2f), 1f, System.Enum.ToObject(blowType, blow) });
return "launched " + System.Enum.ToObject(blowType, blow);
