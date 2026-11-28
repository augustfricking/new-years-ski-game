// Scrolls the open dialog's scrolling part down (pose.txt: how far, in interface units), for a screenshot of what is below.
string arg = System.IO.File.ReadAllText(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Screenshots/pose.txt")).Trim();
float down = float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture);
var layer = Ski.Game.Ui.Instance.DialogLayer;
var list = new System.Collections.Generic.List<UnityEngine.UIElements.ScrollView>();
UnityEngine.UIElements.UQueryExtensions.Query<UnityEngine.UIElements.ScrollView>(layer).ToList(list);
foreach (var v in list) v.scrollOffset = new UnityEngine.Vector2(0f, down);
return "scrolled " + list.Count;
