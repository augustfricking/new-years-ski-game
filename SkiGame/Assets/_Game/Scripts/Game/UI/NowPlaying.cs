using Ski.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ski.Game
{
    /// <summary>
    /// The small card at the bottom right when a song starts (the walkman's or the DJ's): its title
    /// and style, for a few seconds, then it fades away. A new song replaces a card still showing.
    /// </summary>
    public static class NowPlaying
    {
        public const float Seconds = 3.5f;
        static VisualElement _card;
        static System.Action<float> _tick;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { _card = null; _tick = null; }

        public static void Show(string id)
        {
            var ui = Ui.Instance;
            var song = Songs.Get(id);
            if (ui == null || song == null) return;
            if (_card != null) { _card.RemoveFromHierarchy(); if (_tick != null) ui.RemoveTicker(_tick); }
            var card = Ui.Div(ui.TopLayer, "now-playing now-playing-hidden");
            card.pickingMode = PickingMode.Ignore;
            // The song's cover, or a note where a song has none.
            var cover = Resources.Load<Texture2D>("Covers/" + id);
            if (cover != null)
            {
                var art = Ui.Div(card, "now-playing-cover");
                art.style.backgroundImage = new StyleBackground(cover);
                art.pickingMode = PickingMode.Ignore;
            }
            else Ui.Text(card, "♪", "now-playing-note");
            var words = Ui.Div(card);
            words.pickingMode = PickingMode.Ignore;
            Ui.Text(words, song.title.ToUpperInvariant(), "now-playing-title");
            Ui.Text(words, song.style, "now-playing-style");
            _card = card;
            float t = 0f;
            System.Action<float> tick = null;
            tick = ui.AddTicker(dt =>
            {
                t += dt;
                if (t > 0.05f) card.RemoveFromClassList("now-playing-hidden");
                if (t > Seconds) card.AddToClassList("now-playing-hidden");
                if (t > Seconds + 0.5f)
                {
                    card.RemoveFromHierarchy();
                    ui.RemoveTicker(tick);
                    if (_card == card) { _card = null; _tick = null; }
                }
            });
            _tick = tick;
        }
    }
}
