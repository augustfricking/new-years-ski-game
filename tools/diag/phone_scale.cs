// The interface as a phone draws it (touch controls, 1440x810 interface units), for screenshots in the Editor.
// pose.txt: "on" or "off". "off" puts the panel back to 1920x1080 and the touch controls away: always run it after.
string arg = System.IO.File.ReadAllText(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Screenshots/pose.txt")).Trim();
var ps = UnityEngine.Resources.Load<UnityEngine.UIElements.PanelSettings>("UI/Panel");
bool on = arg == "on";
Ski.Game.Device.ForceTouch = on;
ps.referenceResolution = on ? new UnityEngine.Vector2Int(1440, 810) : new UnityEngine.Vector2Int(1920, 1080);
return "phone scale " + (on ? "on" : "off") + ", panel " + ps.referenceResolution;
