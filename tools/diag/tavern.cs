using Ski.Game;
using UnityEngine;

// Walks into the tavern and looks along the bar (for screenshots). Arguments in pose.txt: x z yaw pitch.
SaveData.NameAsked = true; Dialogs.Close();
var app = GameApp.Instance;
if (app.Tavern == null || !app.Tavern.gameObject.activeInHierarchy) app.ShowTavern();
string[] a = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../Screenshots/pose.txt")).Trim().Split(' ');
float F(int i, float d) => a.Length > i && a[i].Length > 0 ? float.Parse(a[i], System.Globalization.CultureInfo.InvariantCulture) : d;
app.Tavern.DebugView(new Vector3(F(0, 2.9f), 0f, F(1, -3.0f)), F(2, 55f), F(3, 2f));
return "tavern";
