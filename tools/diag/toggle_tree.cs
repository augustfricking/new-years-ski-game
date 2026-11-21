using UnityEngine.UIElements;

// Prints the element tree of the first tick box (class "check") with each element's classes and laid-out size.
if (Ski.Game.Ui.Instance.Root.Q(className: "check") == null) { Ski.Game.Dialogs.Close(); Ski.Game.Dialogs.Welcome(); return "opened: run again"; }
var t = Ski.Game.Ui.Instance.Root.Q(className: "check");
if (t == null) return "no tick box";
var sb = new System.Text.StringBuilder();
void Walk(VisualElement e, int depth)
{
    sb.Append(new string(' ', depth * 2) + e.GetType().Name + " [" + string.Join(" ", e.GetClasses()) + "] " + e.layout.width.ToString("0") + "x" + e.layout.height.ToString("0")
              + " display=" + e.resolvedStyle.display + " bg=" + e.resolvedStyle.backgroundColor + "\n");
    foreach (var c in e.Children()) Walk(c, depth + 1);
}
Walk(t, 0);
return sb.ToString();
