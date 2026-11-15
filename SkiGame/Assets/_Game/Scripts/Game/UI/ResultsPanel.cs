using Ski.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ski.Game
{
    /// <summary>The result card and leaderboard shown at the bottom of a run, while you keep skiing.</summary>
    public sealed class ResultsPanel
    {
        readonly VisualElement _root, _banner, _board;
        readonly Label _boardTitle;
        readonly SlopeId _slope;

        public ResultsPanel(RunSession s, SubmitResult r)
        {
            var ui = Ui.Instance;
            _root = Ui.Div(ui.HudLayer, "layer");

            var card = Ui.Div(_root, "card shadowed results");
            var head = Ui.Div(card, "row");
            var chip = Ui.Div(head, "slope-chip" + (s.Slope == SlopeId.Green ? " slope-chip-round" : s.Slope == SlopeId.Black ? " slope-chip-diamond" : ""));
            chip.style.backgroundColor = Palette.Slope(s.Slope);
            Ui.Text(head, s.Course.Name.ToUpperInvariant(), "h2");
            Ui.Text(card, Leaderboards.Format(s.Slope, s.ResultValue), "results-big");
            if (s.ScoreBased)
            {
                if (!string.IsNullOrEmpty(s.BestTrickName)) Ui.Text(card, "Best trick: " + s.BestTrickName + " (" + s.BestTrickPoints + ")", "small");
            }
            else if (s.Penalty > 0f)
            {
                string why = s.GatesMissed > 0 ? s.GatesMissed + " missed gate" + (s.GatesMissed > 1 ? "s" : "") : s.PeopleHit > 0 ? s.PeopleHit + " bump" + (s.PeopleHit > 1 ? "s" : "") : "resets";
                Ui.Text(card, Hud.FormatTime(s.Time) + " s  +  " + s.Penalty.ToString("0") + " s penalty (" + why + ")", "small bad");
            }
            Ui.Text(card, "Top speed " + Mathf.RoundToInt(s.TopSpeed * 3.6f) + " km/h", "small");

            string verdict = r.TopOfBoard ? "NEW SLOPE RECORD!" : r.PersonalBest && !float.IsNaN(r.PreviousBest) ? "NEW PERSONAL BEST!" : "Place " + r.Rank;
            Ui.Text(card, verdict, "h2" + (r.TopOfBoard || r.PersonalBest && !float.IsNaN(r.PreviousBest) ? " good" : ""));

            _slope = s.Slope;
            _boardTitle = Ui.Text(card, OnlineBoards.Status == OnlineBoards.State.Failed ? "THIS DEVICE AND THE LOCALS" : "THIS DEVICE AND THE LOCALS  -  world board loading...", "tiny");
            var board = _board = Ui.Div(card, "board");
            var list = SaveData.Boards.Board(s.Slope);
            int shown = 0;
            bool markedMe = false;
            for (int i = 0; i < list.Count && shown < 9; i++)
            {
                // Always show the top rows and the row of this run.
                bool me = !markedMe && list[i].IsPlayer && Mathf.Approximately(list[i].Value, s.ResultValue) && i == r.Rank - 1;
                if (i >= 7 && !me && r.Rank - 1 > i) continue;
                var row = Ui.Div(board, "board-row" + (me ? " board-row-me" : ""));
                Ui.Text(row, (i + 1) + ".", "board-rank");
                Ui.Text(row, SaveData.BoardName(list[i]), "board-name");
                Ui.Text(row, Leaderboards.Format(s.Slope, list[i].Value));
                if (me) markedMe = true;
                shown++;
            }

            var hints = Ui.Div(card);
            hints.style.marginTop = 12;
            Ui.Text(hints, "Ski to the LIFT for another run", "small");
            Ui.Text(hints, "Ski to the TAVERN door for after-ski", "small");

            _banner = Ui.Div(_root, "banner");
            if (r.TopOfBoard) Ui.Text(_banner, "NEW RECORD!", "banner-text");
            else if (r.PersonalBest && !float.IsNaN(r.PreviousBest)) Ui.Text(_banner, "PERSONAL BEST!", "banner-text");
        }

        /// <summary>Replaces the local list with the world board, once it has arrived. Null: stay local.</summary>
        public void ShowWorld(System.Collections.Generic.List<OnlineBoards.Entry> list)
        {
            if (_root.parent == null) return;
            if (list == null || list.Count == 0) { _boardTitle.text = "THIS DEVICE AND THE LOCALS"; return; }
            _boardTitle.text = "WORLD BOARD";
            _board.Clear();
            int shown = 0;
            foreach (var e in list)
            {
                if (shown >= 9 && !e.Me) continue;
                var row = Ui.Div(_board, "board-row" + (e.Me ? " board-row-me" : ""));
                Ui.Text(row, (e.Rank + 1) + ".", "board-rank");
                Ui.Text(row, e.Name + (e.Me ? " (you)" : ""), "board-name");
                Ui.Text(row, Leaderboards.Format(_slope, (float)e.Score));
                shown++;
            }
        }

        public void Dispose() { _root.RemoveFromHierarchy(); }
    }
}
