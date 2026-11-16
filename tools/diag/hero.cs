// A close look at one sculpted character: whole figure and face. Run in Play mode on the menu.
var old = UnityEngine.GameObject.Find("HeroLook"); if (old != null) UnityEngine.Object.DestroyImmediate(old);
var root = new UnityEngine.GameObject("HeroLook");
root.transform.position = new UnityEngine.Vector3(0f, 500f, 0f);
string[] who = { "Erik", "Jonte", "Maja", "Linn" };
for (int i = 0; i < 4; i++)
{
    var look = Ski.Game.PersonLook.For((Ski.Core.CharacterId)System.Enum.Parse(typeof(Ski.Core.CharacterId), who[i]));
    if (i == 1) look.HasHelmet = false;
    if (i == 2) { look.HasHelmet = false; look.Beanie = true; }
    if (i == 3) look.GogglesDown = true;
    var p = Ski.Game.Person.Create(look, root.transform, who[i]);
    p.transform.localPosition = new UnityEngine.Vector3((i - 1.5f) * 0.85f, 0f, 0f);
    p.transform.localRotation = UnityEngine.Quaternion.Euler(0f, 180f + (i - 1.5f) * -8f, 0f);
    p.Snap(Ski.Game.Pose.Stand);
    if (i == 0) p.Smile = 1f;
    if (i == 1) p.Talking = true;
}
var lift = Ski.Game.GameApp.Instance.Lift; if (lift != null) lift.enabled = false;
Ski.Game.SaveData.NameAsked = true; Ski.Game.Dialogs.Close();
Ski.Game.Ui.Instance.ScreenLayer.style.display = UnityEngine.UIElements.DisplayStyle.None;
string[] arg = System.IO.File.ReadAllText(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Screenshots/pose.txt")).Trim().Split(' ');
float cx = float.Parse(arg[0], System.Globalization.CultureInfo.InvariantCulture), cy = float.Parse(arg[1], System.Globalization.CultureInfo.InvariantCulture), cz = float.Parse(arg[2], System.Globalization.CultureInfo.InvariantCulture);
var cam = UnityEngine.Camera.main;
cam.transform.position = root.transform.position + new UnityEngine.Vector3(cx, cy, cz);
cam.transform.rotation = UnityEngine.Quaternion.Euler(2f, 0f, 0f);
cam.fieldOfView = arg.Length > 3 ? float.Parse(arg[3], System.Globalization.CultureInfo.InvariantCulture) : 40f;
return "ok";
