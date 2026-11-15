// Close-up of one friend on the lift: freezes the lift scene's camera work, hides the menu
// and frames a head. Edit `who`, `dist`, `side` and `fov` before running.
var l = Ski.Game.GameApp.Instance.Lift;
string[] arg = System.IO.File.ReadAllText(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Screenshots/pose.txt")).Trim().Split(' ');
int who = int.Parse(arg[0]); float dist = arg.Length > 1 ? float.Parse(arg[1], System.Globalization.CultureInfo.InvariantCulture) : 0.8f; float side = arg.Length > 2 ? float.Parse(arg[2], System.Globalization.CultureInfo.InvariantCulture) : 0.25f; float fov = 40f;
if (arg.Length > 3) l.Friends[who].SetGogglesUp(arg[3] != "down", true);
l.enabled = false;
Ski.Game.Ui.Instance.ScreenLayer.style.display = UnityEngine.UIElements.DisplayStyle.None;
var cam = UnityEngine.Camera.main;
var f = l.Friends[who];
var head = f.Head.position + UnityEngine.Vector3.up * 0.1f;
cam.transform.position = head + f.transform.forward * dist + f.transform.right * side;
cam.transform.LookAt(head - UnityEngine.Vector3.up * 0.12f);
cam.fieldOfView = fov;
return cam.transform.position.ToString();
