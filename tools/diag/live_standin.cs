using System.Collections.Generic;
using Ski.Core;
using Ski.Game;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

// A second, separately signed-in player inside the Editor, for testing live players against the
// real room service. It only ever joins the private test room "lastrun-test-1". Arguments in
// Screenshots/pose.txt:  join | send N ski|done|tavern | punch <playerId> | leave
string[] a = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../Screenshots/pose.txt")).Trim().Split(' ');
var dom = System.AppDomain.CurrentDomain;
var session = dom.GetData("standin") as ISession;
switch (a[0])
{
    case "join":
    {
        System.Func<System.Threading.Tasks.Task> go = async () =>
        {
            try
            {
                var services = dom.GetData("standin_services") as IUnityServices;
                if (services == null)
                {
                    services = UnityServices.CreateServices("standin");
                    dom.SetData("standin_services", services);
                    await services.InitializeAsync(new InitializationOptions().SetProfile("standin"));
                    await services.GetAuthenticationService().SignInAnonymouslyAsync();
                }
                var options = new SessionOptions { MaxPlayers = 16, PlayerProperties = new Dictionary<string, PlayerProperty> { { "n", new PlayerProperty("Tester", VisibilityPropertyOptions.Member) } } };
                var s = await services.GetMultiplayerService().CreateOrJoinSessionAsync("lastrun-test-1", options);
                dom.SetData("standin", s);
                Debug.Log("[StandIn] in " + s.Id + " as " + s.CurrentPlayer.Id);
            }
            catch (System.Exception e) { Debug.Log("[StandIn] failed: " + e); }
        };
        go();
        return "joining";
    }
    case "look":
    {
        // What the stand-in sees of the others: their latest data.
        if (session == null) return "not joined";
        var sb = new System.Text.StringBuilder();
        foreach (var pl in session.Players)
        {
            if (pl.Id == session.CurrentPlayer.Id) continue;
            sb.Append(pl.Id).Append(':');
            if (pl.Properties != null)
                foreach (var kv in pl.Properties)
                {
                    var pk = kv.Key == "s" ? LivePacket.Decode(kv.Value.Value) : null;
                    sb.Append(' ').Append(kv.Key).Append('=').Append(pk != null ? pk.Mode + " seq " + pk.Seq + " n=" + pk.Samples.Count + " first " + pk.Samples[0].Pos : kv.Value.Value);
                }
            sb.Append(" ;; ");
        }
        return sb.ToString();
    }
    case "leave":
        if (session != null) { session.LeaveAsync(); dom.SetData("standin", null); }
        return "left";
    case "punch":
    {
        if (session == null) return "not joined";
        session.CurrentPlayer.SetProperty("h", new PlayerProperty(a[1] + "|" + a[2] + "|200|" + LiveRooms.Now.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), VisibilityPropertyOptions.Member));
        session.SaveCurrentPlayerDataAsync();
        return "punched";
    }
    default:
    {
        if (session == null) return "not joined";
        int n = int.Parse(a[1]);
        string mode = a.Length > 2 ? a[2] : "ski";
        var p = new LivePacket { Seq = (int)(LiveRooms.Now * 2.0) & 0xFFFF, Start = LiveRooms.Now - 2.0, Character = 3 };
        if (mode == "tavern")
        {
            p.Mode = LiveMode.Tavern;
            for (int i = 0; i < 16; i++)
            {
                float t = n * 2f + i * LivePacket.Interval;
                p.Samples.Add(new LiveSample { Pos = new Vector3(Mathf.Lerp(-3.5f, 0.3f, Mathf.Clamp01(t / 6f)), 0f, -0.9f), Yaw = 90f });
            }
        }
        else
        {
            Vector3 me = GameApp.Instance.Run != null ? GameApp.Instance.Run.Session.Sim.Pos : Vector3.zero;
            p.Mode = mode == "done" ? LiveMode.Finished : LiveMode.Skiing;
            for (int i = 0; i < 16; i++)
            {
                float t = n * 2f + i * LivePacket.Interval;
                p.Samples.Add(new LiveSample { Pos = new Vector3(me.x + 3f + Mathf.Sin(t * 1.3f) * 2.5f, 0f, me.z + 9f + t * 2.5f), Yaw = Mathf.Cos(t * 1.3f) * 30f });
            }
        }
        session.CurrentPlayer.SetProperty("s", new PlayerProperty(p.Encode(), VisibilityPropertyOptions.Member));
        session.SaveCurrentPlayerDataAsync();
        return "sent " + n;
    }
}
