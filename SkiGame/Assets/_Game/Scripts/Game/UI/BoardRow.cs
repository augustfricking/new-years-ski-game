using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ski.Game
{
    /// <summary>
    /// The parts of a world-board row shared by the results card, the map and the beer board: the name
    /// (cut short with "..." and sliding left under the pointer so all of it can be read, with a GUEST tag
    /// for someone without an account) and the clip button that plays the run back.
    /// </summary>
    public static class BoardRow
    {
        /// <summary>The player's name in a row (fills the space the row leaves).</summary>
        public static VisualElement Name(VisualElement row, OnlineBoards.Entry e)
        {
            var box = Ui.Div(row, "board-name name-box");
            var label = Ui.Text(box, e.Name + (e.Me ? " (you)" : ""), "name-text");
            if (e.Guest) Ui.Text(row, "GUEST", "board-guest");
            IVisualElementScheduledItem slide = null;
            float t = 0f, over = 0f;
            box.pickingMode = PickingMode.Position;
            box.RegisterCallback<PointerEnterEvent>(_ =>
            {
                over = label.MeasureTextSize(label.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x - box.layout.width;
                if (over <= 2f) return;
                label.AddToClassList("name-sliding");
                t = 0f;
                // A short wait, then left at a reading pace; it waits at the end and comes back.
                slide = label.schedule.Execute(() =>
                {
                    t += 0.016f;
                    float travel = over + 6f, dur = travel / 55f;
                    float x = Mathf.PingPong(Mathf.Max(0f, t - 0.4f), dur + 0.8f);
                    label.style.translate = new Translate(-Mathf.Min(travel, x / dur * travel), 0f);
                }).Every(16);
            });
            box.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                slide?.Pause();
                slide = null;
                label.style.translate = new Translate(0f, 0f);
                label.RemoveFromClassList("name-sliding");
            });
            return box;
        }

        /// <summary>The button that plays a recorded run back: a small film clip with a play mark.</summary>
        public static Button Watch(VisualElement row, Action onClick)
        {
            var b = Ui.Btn(row, "", onClick, "btn-watch");
            b.Add(new ClipIcon());
            return b;
        }

        sealed class ClipIcon : VisualElement
        {
            public ClipIcon()
            {
                pickingMode = PickingMode.Ignore;
                AddToClassList("clip-icon");
                generateVisualContent += Draw;
            }

            void Draw(MeshGenerationContext ctx)
            {
                var r = contentRect;
                if (r.width < 2f || r.height < 2f) return;
                var p = ctx.painter2D;
                Color ink = Palette.Ink;
                // The frame of a film clip, with sprocket holes along the top and bottom...
                p.strokeColor = ink;
                p.lineWidth = 2f;
                p.BeginPath();
                p.MoveTo(new Vector2(r.xMin + 1f, r.yMin + 1f));
                p.LineTo(new Vector2(r.xMax - 1f, r.yMin + 1f));
                p.LineTo(new Vector2(r.xMax - 1f, r.yMax - 1f));
                p.LineTo(new Vector2(r.xMin + 1f, r.yMax - 1f));
                p.ClosePath();
                p.Stroke();
                p.fillColor = ink;
                for (int i = 0; i < 3; i++)
                {
                    float x = r.xMin + r.width * (0.2f + i * 0.3f);
                    foreach (float y in new[] { r.yMin + 2.5f, r.yMax - 4.5f })
                    {
                        p.BeginPath();
                        p.MoveTo(new Vector2(x - 1f, y)); p.LineTo(new Vector2(x + 1f, y)); p.LineTo(new Vector2(x + 1f, y + 2f)); p.LineTo(new Vector2(x - 1f, y + 2f));
                        p.ClosePath(); p.Fill();
                    }
                }
                // ...and the play mark in the middle.
                Vector2 c = r.center;
                float h = r.height * 0.24f;
                p.BeginPath();
                p.MoveTo(new Vector2(c.x - h * 0.75f, c.y - h));
                p.LineTo(new Vector2(c.x + h, c.y));
                p.LineTo(new Vector2(c.x - h * 0.75f, c.y + h));
                p.ClosePath();
                p.Fill();
            }
        }
    }
}
