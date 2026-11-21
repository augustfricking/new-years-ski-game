// Lists every Person in the scene that has no look (they throw in Update), with where they hang.
var sb = new System.Text.StringBuilder();
int n = 0;
foreach (var p in UnityEngine.Object.FindObjectsByType<Ski.Game.Person>(UnityEngine.FindObjectsSortMode.None))
{
    n++;
    if (p.Look != null) continue;
    var t = p.transform; string path = t.name;
    while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
    sb.Append(path + (p.enabled ? " (on)" : " (off)") + "; ");
}
return n + " people; no look: " + sb;
