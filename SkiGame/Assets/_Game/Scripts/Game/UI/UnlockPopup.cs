using System.Collections.Generic;
using Ski.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ski.Game
{
    /// <summary>
    /// The card that slides in at the bottom left (empty after a run) when something is earned: what was done, what it unlocked (with a
    /// small picture of it) and that it waits in the shop. One at a time; several queue up; each
    /// stays a few seconds and slides away.
    /// </summary>
    public static class UnlockPopup
    {
        public const float Seconds = 5f;
        static readonly Queue<Unlock> Waiting = new Queue<Unlock>();
        static bool _showing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Waiting.Clear(); _showing = false; }

        public static void Show(Unlock u)
        {
            Waiting.Enqueue(u);
            if (!_showing) Next();
        }

        /// <summary>How many cards are on screen or waiting (for tests and views).</summary>
        public static int Pending => Waiting.Count + (_showing ? 1 : 0);

        static void Next()
        {
            var ui = Ui.Instance;
            if (ui == null || Waiting.Count == 0) { _showing = false; return; }
            _showing = true;
            var u = Waiting.Dequeue();
            var card = Ui.Div(ui.TopLayer, "unlock-card unlock-hidden");
            card.pickingMode = PickingMode.Ignore;
            Ui.Text(card, "UNLOCKED!", "unlock-head").pickingMode = PickingMode.Ignore;
            var row = Ui.Div(card, "row unlock-row");
            row.pickingMode = PickingMode.Ignore;
            var pic = Preview(row, u);
            pic.pickingMode = PickingMode.Ignore;
            var words = Ui.Div(row, "unlock-words");
            words.pickingMode = PickingMode.Ignore;
            Ui.Text(words, u.Name.ToUpperInvariant(), "unlock-name").pickingMode = PickingMode.Ignore;
            Ui.Text(words, u.Goal, "unlock-goal").pickingMode = PickingMode.Ignore;
            Ui.Text(words, SkierInfo.Get(u.Who).Name.ToUpperInvariant() + "  ·  NEW IN SHOP", "unlock-where").pickingMode = PickingMode.Ignore;
            AudioDirector.Sfx("cheer", 0.7f, 1.2f);
            float t = 0f;
            System.Action<float> tick = null;
            tick = ui.AddTicker(dt =>
            {
                t += dt;
                if (t > 0.05f) card.RemoveFromClassList("unlock-hidden");
                if (t > Seconds) card.AddToClassList("unlock-hidden");
                if (t > Seconds + 0.6f)
                {
                    card.RemoveFromHierarchy();
                    ui.RemoveTicker(tick);
                    Next();
                }
            });
        }

        /// <summary>A small picture of the thing: the colour itself, a patch of the print, or a badge.</summary>
        static VisualElement Preview(VisualElement parent, Unlock u)
        {
            var style = CharacterStyle.Get(u.Who);
            var box = Ui.Div(parent, "unlock-pic");
            switch (u.Kind)
            {
                case ItemKind.Colour:
                    box.style.backgroundColor = u.Value >= 0 && u.Value < OutfitPaint.Colours.Length ? OutfitPaint.Colours[u.Value] : style.Jacket;
                    box.AddToClassList("unlock-pic-round");
                    break;
                case ItemKind.Pattern:
                    box.style.backgroundImage = new StyleBackground(OutfitPaint.Preview(u.Value, style.Jacket, style.JacketTrim));
                    break;
                default:
                    box.style.backgroundColor = style.Jacket;
                    bool board = SkierInfo.Get(u.Who).Gear == Gear.Snowboard;
                    string kind = u.Kind == ItemKind.Headgear ? "HAT" : u.Kind == ItemKind.Suit ? "SUIT" : board ? "BOARD" : "SKIS";
                    Ui.Text(box, kind, "unlock-pic-label").pickingMode = PickingMode.Ignore;
                    break;
            }
            return box;
        }
    }
}
