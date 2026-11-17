using Ski.Game;
using UnityEngine;
// A close look at the snowboarder's feet wherever he is (menu, cutscene): a second camera aimed at
// them. pose.txt: "yaw distance" (degrees round him, metres away).
string[] a = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../Screenshots/pose.txt")).Trim().Split(' ');
float yaw = float.Parse(a[0], System.Globalization.CultureInfo.InvariantCulture), dist = float.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture);
Person rider = null;
foreach (var p in Object.FindObjectsByType<Person>(FindObjectsSortMode.None))
    if (p.Look != null && p.Look.Gear == GearKind.Board) { rider = p; break; }
if (rider == null) return "no snowboarder";
Vector3 target = (rider.FootL.position + rider.FootR.position) * 0.5f;
var old = GameObject.Find("Feet camera"); if (old != null) Object.DestroyImmediate(old);
var main = Camera.main;
var go = new GameObject("Feet camera");
var cam = go.AddComponent<Camera>();
cam.CopyFrom(main);
cam.depth = main.depth + 10;
cam.nearClipPlane = 0.02f;
main.tag = "Untagged"; main.enabled = false; go.tag = "MainCamera";
Vector3 fwd = rider.transform.forward; fwd.y = 0f; fwd.Normalize();
go.transform.position = target + Quaternion.Euler(0f, yaw, 0f) * fwd * dist + Vector3.up * 0.3f;
go.transform.LookAt(target);
return "board under " + (rider.BoardOnRightFoot ? "right" : "left") + " foot, board at " + rider.Board.position + " foot " + rider.FootL.position + " / " + rider.FootR.position;
