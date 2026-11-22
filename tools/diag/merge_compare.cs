// A child and a grown-up from the crowd, each twice side by side in front of the camera: as parts (left) and
// merged into one skinned mesh (right), in the same pose, to check the merge changes nothing you can see.
var cam = UnityEngine.Camera.main;
var root = new UnityEngine.GameObject("MergeCompare").transform;
var f = cam.transform.forward; f.y = 0f; f.Normalize();
var right = UnityEngine.Vector3.Cross(UnityEngine.Vector3.up, f);
root.position = cam.transform.position + f * 5.5f + UnityEngine.Vector3.down * 1.9f;
root.rotation = UnityEngine.Quaternion.LookRotation(-f);
var rng = new Ski.Core.Rng(7);
var kid = Ski.Game.PersonLook.Random(rng, true, 0.8f);
var adult = Ski.Game.PersonLook.Random(new Ski.Core.Rng(11), false);
float x = -1.9f;
foreach (var look in new[] { kid, adult })
    foreach (bool merge in new[] { false, true })
    {
        Ski.Game.Person.MergeCrowd = merge;
        var p = Ski.Game.Person.Create(look, root, merge ? "Merged" : "Parts");
        p.transform.localPosition = new UnityEngine.Vector3(x, 0f, 0f);
        p.Snap(Ski.Game.Pose.Plough.WithArms(55, 12, 35, 55, 12, 35)); p.enabled = false;   // frozen: no idle motion, so both stand exactly alike
        x += 1.25f;
    }
Ski.Game.Person.MergeCrowd = true;
int n = 0; foreach (var r in root.GetComponentsInChildren<UnityEngine.Renderer>()) n++;
return "placed, renderers " + n;
