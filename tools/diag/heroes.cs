// The four characters side by side, for judging a set of models. Run in Play mode on the menu.
// pose.txt: "cx cy cz fov suffix helmets"  camera offset from the line-up, field of view, a model
// name suffix ("_own" for the hand-built set, "-" for the current one), helmets (1) or bare heads (0).
var old = UnityEngine.GameObject.Find("HeroLook"); if (old != null) UnityEngine.Object.DestroyImmediate(old);
var root = new UnityEngine.GameObject("HeroLook");
root.transform.position = new UnityEngine.Vector3(0f, 500f, 0f);
string[] arg = System.IO.File.ReadAllText(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Screenshots/pose.txt")).Trim().Split(' ');
float F(int i, float d) => arg.Length > i ? float.Parse(arg[i], System.Globalization.CultureInfo.InvariantCulture) : d;
string suffix = arg.Length > 4 && arg[4] != "-" ? arg[4] : "";
bool helmets = arg.Length > 5 && arg[5] == "1";
string[] who = { "Erik", "Jonte", "Maja", "Linn" };
for (int i = 0; i < 4; i++)
{
    var look = Ski.Game.PersonLook.For((Ski.Core.CharacterId)System.Enum.Parse(typeof(Ski.Core.CharacterId), who[i]));
    if (look.Model != null) look.Model += suffix;
    if (!helmets) { look.HasHelmet = false; look.Beanie = false; }
    var p = Ski.Game.Person.Create(look, root.transform, who[i]);
    p.transform.localPosition = new UnityEngine.Vector3((i - 1.5f) * 0.9f, 0f, 0f);
    p.transform.localRotation = UnityEngine.Quaternion.Euler(0f, 180f + (i - 1.5f) * -10f, 0f);
    p.Snap(Ski.Game.Pose.Stand);
}
var lift = Ski.Game.GameApp.Instance.Lift; if (lift != null) lift.enabled = false;
Ski.Game.SaveData.NameAsked = true; Ski.Game.Dialogs.Close();
Ski.Game.Ui.Instance.ScreenLayer.style.display = UnityEngine.UIElements.DisplayStyle.None;
var cam = UnityEngine.Camera.main;
cam.transform.position = root.transform.position + new UnityEngine.Vector3(F(0, 0f), F(1, 1.0f), F(2, -4.5f));
cam.transform.rotation = UnityEngine.Quaternion.Euler(2f, 0f, 0f);
cam.fieldOfView = F(3, 40f);
return "ok " + suffix;
