using Ski.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ski.Game
{
    /// <summary>The result card and leaderboard shown at the bottom of a run, while you keep skiing.</summary>
    public sealed class ResultsPanel
    {
        readonly VisualElement _root, _banner, _board;
        readonly Label _boardTitle, _verdict;
        readonly SlopeId _slope;
        readonly System.Collections.Generic.List<OnlineBoards.Entry> _watchable = new System.Collections.Generic.List<OnlineBoards.Entry>();
        /// <summary>Called with the row to watch.</summary>
        public System.Action<OnlineBoards.Entry> OnWatch;

        /// <summary>Watches the n-th row that has a recording (0-based). False if there is none.</summary>
        public bool Watch(int n)
        {
            if (n < 0 || n >= _watchable.Count || OnWatch == null || _root.parent == null) return false;
            OnWatch(_watchable[n]);
            return true;
        }

        public ResultsPanel(RunSession s, SubmitResult r)
        {
            var ui = Ui.Instance;
            _root = Ui.Div(ui.HudLayer, "layer");

            var card = Ui.Div(_root, "card shadowed results");
            var head = Ui.Div(card, "row");
            Ui.Div(head, "slope-chip").Add(new SlopeIcon(s.Slope));
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

            // Nothing about places until the world board is here: that is the one that counts.
            _verdict = Ui.Text(card, "", "h2");
            _slope = s.Slope;
            _session = s; _local = r;
            _boardTitle = Ui.Text(card, "WORLD BOARD", "tiny");
            _board = Ui.Div(card, "board");
            Ui.Text(_board, "Loading...", "small board-wait");
            // If the world does not answer, this player's own runs on this device, said to be offline.
            float waited = 0f;
            System.Action<float> tick = null;
            tick = ui.AddTicker(dt =>
            {
                waited += dt;
                if (_root.parent == null || _shown) { ui.RemoveTicker(tick); return; }
                if (waited > 8f) { ui.RemoveTicker(tick); ShowLocal(); }
            });

            var hints = Ui.Div(card);
            hints.style.marginTop = 12;
            Ui.Text(hints, "LIFT: again     TAVERN: after-ski", "small");

            _banner = Ui.Div(_root, "banner");
        }

        readonly RunSession _session;
        readonly SubmitResult _local;
        bool _shown;

        /// <summary>This device's own runs, when the world board cannot be reached.</summary>
        public void ShowLocal()
        {
            if (_root.parent == null || _shown) return;
            _shown = true;
            var s = _session; var r = _local;
            bool improved = r.PersonalBest && !float.IsNaN(r.PreviousBest);
            _verdict.text = improved ? "NEW PERSONAL BEST!" : r.PersonalBest ? "FIRST RUN IN THE BOOK" : "Your #" + r.Rank;
            _verdict.EnableInClassList("good", improved);
            if (improved) { _banner.Clear(); Ui.Text(_banner, "PERSONAL BEST!", "banner-text"); }
            _boardTitle.text = "YOUR RUNS  (world board offline)";
            _board.Clear();
            var list = SaveData.Boards.Board(s.Slope);
            int shown = 0;
            bool markedMe = false;
            for (int i = 0; i < list.Count && shown < 9; i++)
            {
                bool me = !markedMe && list[i].IsPlayer && Mathf.Approximately(list[i].Value, s.ResultValue) && i == r.Rank - 1;
                if (i >= 7 && !me && r.Rank - 1 > i) continue;
                var row = Ui.Div(_board, "board-row" + (me ? " board-row-me" : ""));
                Ui.Text(row, (i + 1) + ".", "board-rank");
                Ui.Text(row, SaveData.BoardName(list[i]), "board-name");
                Ui.Text(row, Leaderboards.Format(s.Slope, list[i].Value));
                if (me) markedMe = true;
                shown++;
            }
        }

        /// <summary>The character tag on a board row (the friend the run was skied as).</summary>
        public static void WhoTag(VisualElement row, int character)
        {
            if (character < 0 || character > 3) { Ui.Div(row, "board-who board-who-none"); return; }
            var id = (CharacterId)character;
            var tag = Ui.Text(row, SkierInfo.Get(id).Name.ToUpperInvariant(), "board-who");
            Color c = CharacterStyle.Get(id).Jacket;
            tag.style.backgroundColor = c;
            tag.style.color = c.grayscale > 0.55f ? Palette.Ink : Palette.White;
        }

        /// <summary>The world board, once it has arrived (null: it could not be reached). Returns true for a world record.</summary>
        public bool ShowWorld(OnlineBoards.WorldResult w)
        {
            if (_root.parent == null) return false;
            if (w == null || w.Rows == null) { _shown = false; ShowLocal(); return false; }
            _shown = true;
            var list = w.Rows;
            bool record = w.RunRank == 1 && w.NewBest && list.Count > 1;
            string place = w.RunRank > 0 ? "#" + w.RunRank + " IN THE WORLD" : "";
            if (record) { _verdict.text = "WORLD RECORD!"; }
            else if (w.NewBest && !w.First) _verdict.text = "NEW PERSONAL BEST!  " + place;
            else if (w.NewBest) _verdict.text = "FIRST RUN  " + place;
            else _verdict.text = "THIS RUN " + place + (w.BestRank > 0 ? "  -  YOUR BEST #" + w.BestRank : "");
            _verdict.EnableInClassList("good", record || w.NewBest);
            _banner.Clear();
            if (record) Ui.Text(_banner, "WORLD RECORD!", "banner-text");
            else if (w.NewBest && !w.First) Ui.Text(_banner, "PERSONAL BEST!", "banner-text");
            _board.Clear();
            _watchable.Clear();
            if (list.Count == 0) Ui.Text(_board, "Nobody yet", "small board-wait");
            int shown = 0;
            foreach (var e in list)
            {
                if (shown >= 9 && !e.Me) continue;
                var row = Ui.Div(_board, "board-row" + (e.Me ? " board-row-me" : ""));
                Ui.Text(row, (e.Rank + 1) + ".", "board-rank");
                Ui.Text(row, e.Name + (e.Me ? " (you)" : ""), "board-name");
                WhoTag(row, e.Character);
                Ui.Text(row, Leaderboards.Format(_slope, (float)e.Score), "board-value");
                // Each recorded run can be watched: its number key, or a click when the mouse is free.
                if (!string.IsNullOrEmpty(e.Ghost) && _watchable.Count < 9)
                {
                    var entry = e;
                    _watchable.Add(e);
                    Ui.Btn(row, _watchable.Count.ToString(), () => OnWatch?.Invoke(entry), "btn-watch");
                }
                else Ui.Div(row, "btn-watch-gap");
                shown++;
            }
            _boardTitle.text = _watchable.Count > 0 ? "WORLD BOARD      1-" + _watchable.Count + "  watch" : "WORLD BOARD";
            return record;
        }

        public void Dispose() { _root.RemoveFromHierarchy(); }
    }
}
