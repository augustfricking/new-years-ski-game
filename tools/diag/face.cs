// Close-up of one friend on the lift: freezes the lift scene's camera work, hides the menu
// and frames a head. Edit `who`, `dist`, `side` and `fov` before running.
int who = 3; float dist = 1.1f; float side = 0.45f; float fov = 40f;
var l = Ski.Game.GameApp.Instance.Lift;
l.enabled = false;
Ski.Game.Ui.Instance.ScreenLayer.style.display = UnityEngine.UIElements.DisplayStyle.None;
var cam = UnityEngine.Camera.main;
var f = l.Friends[who];
var head = f.Head.position + UnityEngine.Vector3.up * 0.1f;
cam.transform.position = head + f.transform.forward * dist + f.transform.right * side;
cam.transform.LookAt(head - UnityEngine.Vector3.up * 0.12f);
cam.fieldOfView = fov;
return cam.transform.position.ToString();
