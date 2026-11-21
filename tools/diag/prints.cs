// The four friends wearing the earned prints and rare colours (ducks, hearts, lightning, leopard), for a look.
// Run in Play mode on the menu. pose.txt: "cx cy cz fov yaw"  camera offset, field of view, how far they turn.
var old = UnityEngine.GameObject.Find("PrintLook"); if (old != null) UnityEngine.Object.DestroyImmediate(old);
var root = new UnityEngine.GameObject("PrintLook");
root.transform.position = new UnityEngine.Vector3(0f, 500f, 0f);
string[] arg = System.IO.File.ReadAllText(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Screenshots/pose.txt")).Trim().Split(' ');
float F(int i, float d) => arg.Length > i ? float.Parse(arg[i], System.Globalization.CultureInfo.InvariantCulture) : d;
string[] who = { "Jonte", "Maja", "Erik", "Linn" };
int free = Ski.Core.Unlocks.FreeColours;
var outfits = new[]
{
    new Ski.Core.Outfit { pattern = Ski.Core.Unlocks.PatternDucks, jacket = 8, pants = free },             // ducks on blue, toxic green pants
    new Ski.Core.Outfit { pattern = Ski.Core.Unlocks.PatternHearts, jacket = free + 1, trim = 0 },        // hearts on neon pink
    new Ski.Core.Outfit { pattern = Ski.Core.Unlocks.PatternLightning, jacket = free + 2, trim = 1 },     // lightning on electric blue
    new Ski.Core.Outfit { pattern = Ski.Core.Unlocks.PatternLeopard, jacket = 4, pants = free + 3 },      // leopard, ultraviolet pants
};
for (int i = 0; i < 4; i++)
{
    var look = Ski.Game.PersonLook.For((Ski.Core.CharacterId)System.Enum.Parse(typeof(Ski.Core.CharacterId), who[i]), outfits[i]);
    var p = Ski.Game.Person.Create(look, root.transform, who[i]);
    p.transform.localPosition = new UnityEngine.Vector3((i - 1.5f) * 0.95f, 0f, 0f);
    p.transform.localRotation = UnityEngine.Quaternion.Euler(0f, 180f + (i - 1.5f) * -10f + F(4, 0f), 0f);
    p.Snap(Ski.Game.Pose.Stand);
}
var lift = Ski.Game.GameApp.Instance.Lift; if (lift != null) lift.enabled = false;
Ski.Game.SaveData.NameAsked = true; Ski.Game.Dialogs.Close();
Ski.Game.Ui.Instance.ScreenLayer.style.display = UnityEngine.UIElements.DisplayStyle.None;
var cam = UnityEngine.Camera.main;
cam.transform.position = root.transform.position + new UnityEngine.Vector3(F(0, 0f), F(1, 1.1f), F(2, -4.2f));
cam.transform.rotation = UnityEngine.Quaternion.Euler(2f, 0f, 0f);
cam.fieldOfView = F(3, 40f);
return "ok";
