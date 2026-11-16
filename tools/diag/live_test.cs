using Ski.Core;
using Ski.Game;
using UnityEngine;

// Live players, tested against the real room service in a private test room ("lastrun-test-1"),
// never one real players use. Arguments in Screenshots/pose.txt:
//   join              -> the Editor joins the test room (sending allowed for this test only)
//   packet N mode     -> returns packet N (0, 1, 2...) of a made-up skier near the camera, as text,
//                        for the UGS CLI to put on a stand-in player; mode: ski | done | tavern
//   status            -> who the Editor sees in the room
//   leave             -> leaves the room and switches sending off again
string[] a = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../Screenshots/pose.txt")).Trim().Split(' ');
switch (a[0])
{
    case "join":
        LiveRooms.SendFromEditor = true;
        LiveRooms.Enter("test");
        return "joining";
    case "leave":
        LiveRooms.Enter(null);
        LiveRooms.SendFromEditor = false;
        return "left";
    case "status":
    {
        var sb = new System.Text.StringBuilder("place=" + LiveRooms.Place + " room=" + LiveRooms.RoomId + " me=" + LiveRooms.MyId + " connected=" + LiveRooms.Connected);
        foreach (var r in LiveRooms.Others.Values)
        {
            bool vis = r.Timeline.Visible(LiveRooms.Now);
            r.Timeline.At(LiveRooms.Now, out var s, out float sp, out var m);
            sb.Append("\n  ").Append(r.Id).Append(" '").Append(r.Name).Append("' ").Append(r.Who).Append(" samples=").Append(r.Timeline.Count).Append(" visible=").Append(vis).Append(" at=").Append(s.Pos).Append(" mode=").Append(m);
        }
        return sb.ToString();
    }
    default:
    {
        int n = int.Parse(a[1]);
        string mode = a.Length > 2 ? a[2] : "ski";
        var p = new LivePacket { Seq = n + 1, Start = 100.0 + n * 2.0, Character = 3 };
        if (mode == "tavern")
        {
            p.Mode = LiveMode.Tavern;
            for (int i = 0; i < 16; i++)
            {
                float t = n * 2f + i * LivePacket.Interval;
                p.Samples.Add(new LiveSample { Pos = new Vector3(0.5f + Mathf.Sin(t * 0.5f) * 1.2f, 0f, -1.2f + t * 0.25f), Yaw = 200f, Flags = LiveFlags.None });
            }
            return p.Encode();
        }
        var run = GameApp.Instance.Run;
        Vector3 me = run.Session.Sim.Pos;
        p.Mode = mode == "done" ? LiveMode.Finished : LiveMode.Skiing;
        for (int i = 0; i < 16; i++)
        {
            float t = n * 2f + i * LivePacket.Interval;
            p.Samples.Add(new LiveSample { Pos = new Vector3(me.x + 3f + Mathf.Sin(t * 1.3f) * 2.5f, 0f, me.z + 10f + t * 3f), Yaw = Mathf.Cos(t * 1.3f) * 30f, Flags = LiveFlags.None });
        }
        return p.Encode();
    }
}
