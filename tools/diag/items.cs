// The shop's earnable items on the four friends, side by side. Arguments in pose.txt: set 0 or 1.
var old = UnityEngine.GameObject.Find("ItemLook"); if (old != null) UnityEngine.Object.DestroyImmediate(old);
string[] a = System.IO.File.ReadAllText(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Screenshots/pose.txt")).Trim().Split(' ');
int set = int.Parse(a[0]);
var root = new UnityEngine.GameObject("ItemLook");
root.transform.position = new UnityEngine.Vector3(0f, 500f, 0f);
var outfits = set == 2
    ? new[] { new Ski.Core.Outfit { headgear = 7, pattern = 7 }, new Ski.Core.Outfit { headgear = 8, pattern = 8 }, new Ski.Core.Outfit { headgear = 9, pattern = 9, trim = 0 }, new Ski.Core.Outfit { headgear = 10, pattern = 10, jacket = 1 } }
    : set == 3
    ? new[] { new Ski.Core.Outfit { suit = 7, headgear = 11 }, new Ski.Core.Outfit { suit = 4 }, new Ski.Core.Outfit { suit = 5, headgear = 9 }, new Ski.Core.Outfit { suit = 6, headgear = 10 } }
    : set == 4
    ? new[] { new Ski.Core.Outfit { suit = 3 }, new Ski.Core.Outfit { pattern = 11, headgear = 11 }, new Ski.Core.Outfit { suit = 3, headgear = 7 }, new Ski.Core.Outfit { pattern = 11 } }
    : set == 0
    ? new[] { new Ski.Core.Outfit { headgear = 3, pattern = 5 }, new Ski.Core.Outfit { headgear = 2, pattern = 6 }, new Ski.Core.Outfit { headgear = 5, suit = 1 }, new Ski.Core.Outfit { headgear = 6, pattern = 4 } }
    : new[] { new Ski.Core.Outfit { suit = 2 }, new Ski.Core.Outfit { headgear = 4, pattern = 2, jacket = 2 }, new Ski.Core.Outfit { headgear = 1, pattern = 1, jacket = 8, trim = 0 }, new Ski.Core.Outfit { pattern = 3, jacket = 11, trim = 0 } };
for (int i = 0; i < 4; i++)
{
    var look = Ski.Game.PersonLook.For((Ski.Core.CharacterId)i, outfits[i]);
    look.GogglesDown = false;
    var p = Ski.Game.Person.Create(look, root.transform, "P" + i);
    p.transform.localPosition = new UnityEngine.Vector3((i - 1.5f) * 0.85f, 0f, 0f);
    p.transform.localRotation = UnityEngine.Quaternion.Euler(0f, 180f + (i - 1.5f) * -10f, 0f);
    p.Snap(Ski.Game.Pose.Stand);
}
var lift = Ski.Game.GameApp.Instance.Lift; if (lift != null) lift.enabled = false;
Ski.Game.Ui.Instance.ScreenLayer.style.display = UnityEngine.UIElements.DisplayStyle.None;
var cam = UnityEngine.Camera.main;
cam.transform.position = root.transform.position + new UnityEngine.Vector3(0f, 1.25f, -3.3f);
cam.transform.rotation = UnityEngine.Quaternion.Euler(5f, 0f, 0f);
cam.fieldOfView = 40f;
return "ok";
