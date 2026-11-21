// Goes to the ski map the way the pause menu's SKI MAP does (for checking that the walkman plays on).
var app = Ski.Game.GameApp.Instance;
Ski.Game.Dialogs.Close();
if (app.Run != null) app.Run.Paused = false;
var t = typeof(Ski.Game.GameApp);
var show = t.GetMethod("ShowMap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
var tr = t.GetMethod("Transition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
tr.Invoke(app, new object[] { (System.Action)(() => show.Invoke(app, null)) });
return "to map";
