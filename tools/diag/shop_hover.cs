// With the shop open (tools/diag/shop_view.cs), rests the mouse on the first locked thing (pose.txt's third
// word: "item" or "swatch") to see its note saying how to earn it. Batch mode has no mouse: one is added.
string[] a = System.IO.File.ReadAllText(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Screenshots/pose.txt")).Trim().Split(' ');
string cls = a.Length > 2 && a[2] == "swatch" ? "shop-swatch-locked" : "shop-item-locked";
var root = Ski.Game.Ui.Instance.Root;
var found = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.VisualElement>(root, null, cls);
if (found == null) return "no " + cls;
var mouse = UnityEngine.InputSystem.Mouse.current ?? UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
var c = found.worldBound.center;
float k = UnityEngine.Screen.width / root.layout.width;
var pos = new UnityEngine.Vector2(c.x * k, UnityEngine.Screen.height - c.y * k);
UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = pos });
UnityEngine.InputSystem.InputSystem.Update();
return "mouse at " + pos + " over " + found.worldBound;
