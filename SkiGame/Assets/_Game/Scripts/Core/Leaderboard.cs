using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Ski.Core
{
    public interface IKeyValueStore
    {
        string Get(string key);
        void Set(string key, string value);
    }

    public sealed class MemoryStore : IKeyValueStore
    {
        readonly Dictionary<string, string> _d = new Dictionary<string, string>();
        public string Get(string key) => _d.TryGetValue(key, out var v) ? v : null;
        public void Set(string key, string value) => _d[key] = value;
    }

    public struct BoardEntry
    {
        public string Name;
        public float Value;
        public bool IsPlayer;
        public CharacterId Character;
    }

    public struct SubmitResult
    {
        public int Rank;            // 1-based position on the board
        public bool PersonalBest;   // better than every earlier run of the player on this slope
        public bool TopOfBoard;     // new number one
        public float PreviousBest;  // NaN if none
    }

    /// <summary>The tavern's chugging board: fastest stein wins. Seeded with the regulars.</summary>
    public sealed class ChugBoard
    {
        readonly IKeyValueStore _store;
        readonly List<BoardEntry> _list = new List<BoardEntry>();
        static readonly string[] Regulars = { "Berit (bar)", "Sigge", "Tuva", "Olle", "Freja", "Viggo", "Ebba", "Nisse" };
        static readonly float[] Times = { 8.9f, 9.8f, 10.9f, 12.2f, 13.6f, 15.2f, 17.5f, 20.5f };
        public const float HouseRecord = 8.9f;

        public IReadOnlyList<BoardEntry> Entries => _list;

        public ChugBoard(IKeyValueStore store)
        {
            _store = store;
            for (int i = 0; i < Regulars.Length; i++) _list.Add(new BoardEntry { Name = Regulars[i], Value = Times[i] });
            string raw = store.Get("board_chug");
            if (string.IsNullOrEmpty(raw)) return;
            foreach (var part in raw.Split(';'))
            {
                var f = part.Split('|');
                if (f.Length != 2 || !int.TryParse(f[0], out int who) || who < 0 || who > 3) continue;
                if (!float.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) continue;
                Insert(new BoardEntry { Name = SkierInfo.Get((CharacterId)who).Name, Value = v, IsPlayer = true, Character = (CharacterId)who });
            }
        }

        int Insert(BoardEntry e)
        {
            int pos = 0;
            while (pos < _list.Count && _list[pos].Value <= e.Value) pos++;
            _list.Insert(pos, e);
            return pos;
        }

        public float PersonalBest
        {
            get { foreach (var e in _list) if (e.IsPlayer) return e.Value; return float.NaN; }
        }

        public SubmitResult Submit(CharacterId who, float seconds)
        {
            float prev = PersonalBest;
            var r = new SubmitResult { PreviousBest = prev, PersonalBest = float.IsNaN(prev) || seconds < prev };
            int pos = Insert(new BoardEntry { Name = SkierInfo.Get(who).Name, Value = seconds, IsPlayer = true, Character = who });
            r.Rank = pos + 1; r.TopOfBoard = pos == 0;
            int mine = 0;
            for (int i = 0; i < _list.Count; i++)
                if (_list[i].IsPlayer && ++mine > 5) { _list.RemoveAt(i); i--; }
            var sb = new StringBuilder();
            foreach (var e in _list)
            {
                if (!e.IsPlayer) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append((int)e.Character).Append('|').Append(e.Value.ToString("0.###", CultureInfo.InvariantCulture));
            }
            _store.Set("board_chug", sb.ToString());
            return r;
        }
    }

    /// <summary>
    /// Local leaderboards, one per slope. Seeded with rival skiers so a first run has something to
    /// beat. Lower is better for times; higher is better for park points.
    /// </summary>
    public sealed class Leaderboards
    {
        readonly IKeyValueStore _store;
        readonly List<BoardEntry>[] _boards = new List<BoardEntry>[5];

        static readonly string[] Rivals = { "Sigge", "Ebba", "Olle", "Tuva", "Viggo", "Alva", "Nisse", "Freja" };
        // Rival results per slope: (best, step). Tuned so a decent first run lands mid-table.
        static readonly float[,] RivalRange =
        {
            { 44f, 3.2f },     // green, seconds
            { 9500f, -950f },  // blue, points
            { 49f, 3.0f },     // red
            { 44f, 2.2f },     // black
            { 88f, 7f },       // off-piste
        };

        public Leaderboards(IKeyValueStore store)
        {
            _store = store;
            for (int s = 0; s < 5; s++) Load((SlopeId)s);
        }

        public IReadOnlyList<BoardEntry> Board(SlopeId slope) => _boards[(int)slope];

        public float PersonalBest(SlopeId slope)
        {
            float best = float.NaN;
            bool higher = Courses.ScoreBased(slope);
            foreach (var e in _boards[(int)slope])
                if (e.IsPlayer && (float.IsNaN(best) || (higher ? e.Value > best : e.Value < best))) best = e.Value;
            return best;
        }

        public SubmitResult Submit(SlopeId slope, CharacterId who, float value)
        {
            var list = _boards[(int)slope];
            bool higher = Courses.ScoreBased(slope);
            float prev = PersonalBest(slope);
            var r = new SubmitResult { PreviousBest = prev };
            r.PersonalBest = float.IsNaN(prev) || (higher ? value > prev : value < prev);
            var entry = new BoardEntry { Name = SkierInfo.Get(who).Name, Value = value, IsPlayer = true, Character = who };
            int pos = 0;
            while (pos < list.Count && (higher ? list[pos].Value >= value : list[pos].Value <= value)) pos++;
            list.Insert(pos, entry);
            r.Rank = pos + 1;
            r.TopOfBoard = pos == 0;
            // Keep the rivals and the player's best five.
            int mine = 0;
            for (int i = 0; i < list.Count; i++)
                if (list[i].IsPlayer && ++mine > 5) { list.RemoveAt(i); i--; }
            Save(slope);
            return r;
        }

        void Load(SlopeId slope)
        {
            var list = _boards[(int)slope] = new List<BoardEntry>();
            int s = (int)slope;
            for (int i = 0; i < Rivals.Length; i++)
                list.Add(new BoardEntry { Name = Rivals[(i + s * 3) % Rivals.Length], Value = RivalRange[s, 0] + RivalRange[s, 1] * i });
            string raw = _store.Get("board_" + s);
            if (!string.IsNullOrEmpty(raw))
            {
                bool higher = Courses.ScoreBased(slope);
                foreach (var part in raw.Split(';'))
                {
                    var f = part.Split('|');
                    if (f.Length != 2 || !int.TryParse(f[0], out int who) || who < 0 || who > 3) continue;
                    if (!float.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) continue;
                    var e = new BoardEntry { Name = SkierInfo.Get((CharacterId)who).Name, Value = v, IsPlayer = true, Character = (CharacterId)who };
                    int pos = 0;
                    while (pos < list.Count && (higher ? list[pos].Value >= v : list[pos].Value <= v)) pos++;
                    list.Insert(pos, e);
                }
            }
        }

        void Save(SlopeId slope)
        {
            var sb = new StringBuilder();
            foreach (var e in _boards[(int)slope])
            {
                if (!e.IsPlayer) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append((int)e.Character).Append('|').Append(e.Value.ToString("0.###", CultureInfo.InvariantCulture));
            }
            _store.Set("board_" + (int)slope, sb.ToString());
        }

        public static string Format(SlopeId slope, float value)
        {
            if (Courses.ScoreBased(slope)) return ((int)value).ToString("N0", CultureInfo.InvariantCulture) + " p";
            int min = (int)(value / 60f);
            float sec = value - min * 60f;
            return min > 0 ? min + ":" + sec.ToString("00.00", CultureInfo.InvariantCulture) : sec.ToString("0.00", CultureInfo.InvariantCulture) + " s";
        }
    }
}
