using Ski.Core;
using Ski.Game;
using UnityEngine;

// Lists a world board's rows with what their recordings say (character, length, how straight the line is).
string board = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../Screenshots/pose.txt")).Trim();
var list = OnlineBoards.Cached(board);
if (list == null) { OnlineBoards.Fetch(board, null); return "fetching, run again"; }
var sb = new System.Text.StringBuilder();
foreach (var e in list)
{
    var t = GhostTrack.Decode(e.Ghost);
    sb.Append(e.Rank + 1).Append(". ").Append(e.Name).Append(" ").Append(e.Score.ToString("0.00")).Append(" char=").Append(e.Character);
    if (t != null)
    {
        // How much the line wanders side to side: a bot follows its racing line, people weave.
        float wander = 0f; float minX = 1e9f, maxX = -1e9f;
        for (int i = 2; i < t.Count; i++) { wander += Mathf.Abs(t[i].X - 2f * t[i - 1].X + t[i - 2].X); minX = Mathf.Min(minX, t[i].X); maxX = Mathf.Max(maxX, t[i].X); }
        sb.Append(" rec=").Append(t.Duration.ToString("0.0")).Append("s samples=").Append(t.Count).Append(" x=[").Append(minX.ToString("0")).Append(",").Append(maxX.ToString("0")).Append("] wiggle=").Append((wander / t.Count).ToString("0.00"));
    }
    sb.Append("\n");
}
return sb.ToString();
