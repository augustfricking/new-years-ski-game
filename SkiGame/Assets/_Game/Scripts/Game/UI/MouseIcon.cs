using UnityEngine;
using UnityEngine.UIElements;

namespace Ski.Game
{
    /// <summary>
    /// A small drawn computer mouse for the key hints: the left (or right) button lit up for a
    /// click, neither for "move the mouse".
    /// </summary>
    public sealed class MouseIcon : VisualElement
    {
        public enum Lit { None, Left, Right }
        readonly Lit _lit;

        static readonly Color Ink = new Color(0.106f, 0.078f, 0.188f), Paper = new Color(0.96f, 0.92f, 0.85f), Glow = new Color(1f, 0.78f, 0.29f);

        public MouseIcon(Lit lit)
        {
            _lit = lit;
            AddToClassList("mouse-icon");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            float w = r.width, h = r.height, line = Mathf.Max(2f, w * 0.09f);
            var p = ctx.painter2D;
            float x0 = line * 0.5f, x1 = w - line * 0.5f, y0 = line * 0.5f, y1 = h - line * 0.5f, mid = (x0 + x1) * 0.5f, split = h * 0.42f;
            float rad = (x1 - x0) * 0.5f;

            void Body()
            {
                p.BeginPath();
                p.MoveTo(new Vector2(x0, y0 + rad));
                p.ArcTo(new Vector2(x0, y0), new Vector2(mid, y0), rad);
                p.ArcTo(new Vector2(x1, y0), new Vector2(x1, y0 + rad), rad);
                p.LineTo(new Vector2(x1, y1 - rad));
                p.ArcTo(new Vector2(x1, y1), new Vector2(mid, y1), rad);
                p.ArcTo(new Vector2(x0, y1), new Vector2(x0, y1 - rad), rad);
                p.ClosePath();
            }
            p.fillColor = Paper;
            Body();
            p.Fill();
            // The lit button: the top half of one side.
            if (_lit != Lit.None)
            {
                bool left = _lit == Lit.Left;
                float a = left ? x0 : mid, b = left ? mid : x1;
                p.fillColor = Glow;
                p.BeginPath();
                if (left)
                {
                    p.MoveTo(new Vector2(x0, split));
                    p.LineTo(new Vector2(x0, y0 + rad));
                    p.ArcTo(new Vector2(x0, y0), new Vector2(mid, y0), rad);
                    p.LineTo(new Vector2(mid, split));
                }
                else
                {
                    p.MoveTo(new Vector2(mid, split));
                    p.LineTo(new Vector2(mid, y0));
                    p.ArcTo(new Vector2(x1, y0), new Vector2(x1, y0 + rad), rad);
                    p.LineTo(new Vector2(x1, split));
                }
                p.ClosePath();
                p.Fill();
            }
            p.strokeColor = Ink;
            p.lineWidth = line;
            Body();
            p.Stroke();
            p.BeginPath();
            p.MoveTo(new Vector2(x0, split));
            p.LineTo(new Vector2(x1, split));
            p.MoveTo(new Vector2(mid, y0));
            p.LineTo(new Vector2(mid, split));
            p.Stroke();
        }
    }
}
