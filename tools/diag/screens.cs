using Ski.Core;
using Ski.Game;
using UnityEngine;
// Opens one screen for a screenshot (pose.txt: "shop", "settings 0..3", "beer", "mapinfo 0..4").
SaveData.NameAsked = true; Dialogs.Close();
string[] a = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../Screenshots/pose.txt")).Trim().Split(' ');
var app = GameApp.Instance;
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;
switch (a[0])
{
    case "shop":
        ShopPanel.Open(app.transform, app.Character, null);
        return "shop";
    case "settings":
        typeof(Dialogs).GetField("_section", flags).SetValue(null, int.Parse(a[1]));
        Dialogs.Settings();
        return "settings";
    case "beer":
        if (app.Tavern == null) app.ShowTavern();
        app.Tavern.StartGame();
        return "beer";
    case "mapinfo":
        if (app.Map == null) app.ShowMap();
        typeof(MapScene).GetMethod("ShowInfo", flags).Invoke(app.Map, new object[] { int.Parse(a[1]) });
        return "map info";
}
return "?";
