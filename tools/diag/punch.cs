using Ski.Game;
using UnityEngine;

// Stands the player by the bar, facing a guest there, and throws a punch (for screenshots).
var t = GameApp.Instance.Tavern;
t.DebugView(new Vector3(2.9f, 0f, -3.0f), 55f, 2f);
typeof(TavernScene).GetMethod("Punch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(t, null);
return "knockouts=" + t.Knockouts;
