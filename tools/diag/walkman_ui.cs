using UnityEngine.UIElements;

// Opens the settings with a walkman, scrolls to the walkman rows and opens the song list (for a screenshot).
Ski.Game.SaveData.NameAsked = true; Ski.Game.SaveData.HasWalkman = true; Ski.Game.SaveData.WalkmanOn = true;
Ski.Game.Dialogs.Close();
Ski.Game.Dialogs.Settings();
var root = Ski.Game.Ui.Instance.Root;
Button songBtn = null;
root.Query<Button>().ForEach(b => { var l = b.parent != null ? b.parent.Q<Label>() : null; if (songBtn == null && l != null && l.text == "Song (M)") songBtn = b; });
if (songBtn == null) return "no song button";
using (var e = NavigationSubmitEvent.GetPooled()) { e.target = songBtn; songBtn.SendEvent(e); }
var scroll = root.Q<ScrollView>(className: "settings-scroll");
// (the list scrolls itself into view)
return "opened";
