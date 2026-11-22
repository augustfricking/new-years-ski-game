// Opens the shop for one friend wearing a tall hat (pose.txt: "who headgear"), for checking the framing and the lock notes.
string[] a = System.IO.File.ReadAllText(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Screenshots/pose.txt")).Trim().Split(' ');
var who = (Ski.Core.CharacterId)System.Enum.Parse(typeof(Ski.Core.CharacterId), a.Length > 0 ? a[0] : "Maja");
int hat = a.Length > 1 ? int.Parse(a[1]) : 6;
Ski.Game.SaveData.NameAsked = true; Ski.Game.Dialogs.Close();
var o = Ski.Game.SaveData.Outfit(who);
var keep = o.Copy();
o.headgear = hat;
// Shown on the stand as worn, without saving it or earning it.
var shop = Ski.Game.ShopPanel.Open(Ski.Game.GameApp.Instance.transform, who, null);
var stage = UnityEngine.Object.FindFirstObjectByType<Ski.Game.ShopStage>();
if (stage != null) stage.Show(Ski.Game.PersonLook.For(who, o));
return "shop " + who + " hat " + hat;
