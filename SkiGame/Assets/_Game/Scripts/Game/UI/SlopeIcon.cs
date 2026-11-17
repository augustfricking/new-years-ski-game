using Ski.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ski.Game
{
    /// <summary>
    /// The grade symbol of a run, drawn as on the piste map: a green circle, a blue square, a red
    /// rectangle lying on its side, a black diamond, and a warning triangle for the off-piste route.
    /// </summary>
    public class SlopeIcon : VisualElement
    {
        readonly SlopeId _id;

        public SlopeIcon(SlopeId id)
        {
            _id = id;
            AddToClassList("slope-icon");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext mgc)
        {
            var p = mgc.painter2D;
            Rect r = contentRect;
            Vector2 c = r.center;
            float s = Mathf.Min(r.width, r.height) * 0.5f;
            Color ink = Palette.Ink, fill = Palette.Slope(_id);
            p.lineJoin = LineJoin.Miter;
            p.lineWidth = 3f;
            p.strokeColor = ink;
            p.fillColor = fill;
            p.BeginPath();
            switch (_id)
            {
                case SlopeId.Green:
                    p.Arc(c, s - 1.5f, Angle.Degrees(0f), Angle.Degrees(360f));
                    break;
                case SlopeId.Blue:
                    Shape(p, c + new Vector2(-s * 0.8f, -s * 0.8f), c + new Vector2(s * 0.8f, -s * 0.8f), c + new Vector2(s * 0.8f, s * 0.8f), c + new Vector2(-s * 0.8f, s * 0.8f));
                    break;
                case SlopeId.Red:
                    Shape(p, c + new Vector2(-s, -s * 0.55f), c + new Vector2(s, -s * 0.55f), c + new Vector2(s, s * 0.55f), c + new Vector2(-s, s * 0.55f));
                    break;
                case SlopeId.Black:
                    p.strokeColor = Palette.White;
                    Shape(p, c + new Vector2(0f, -s), c + new Vector2(s, 0f), c + new Vector2(0f, s), c + new Vector2(-s, 0f));
                    break;
                default:
                    p.fillColor = Palette.SignalYellow;
                    Shape(p, c + new Vector2(0f, -s), c + new Vector2(s, s * 0.8f), c + new Vector2(-s, s * 0.8f));
                    break;
            }
            p.ClosePath();
            p.Fill();
            p.Stroke();
            if (_id == SlopeId.OffPiste)
            {
                // The exclamation mark.
                p.fillColor = ink;
                p.BeginPath();
                Shape(p, c + new Vector2(-s * 0.09f, -s * 0.42f), c + new Vector2(s * 0.09f, -s * 0.42f), c + new Vector2(s * 0.06f, s * 0.3f), c + new Vector2(-s * 0.06f, s * 0.3f));
                p.ClosePath();
                p.Fill();
                p.BeginPath();
                p.Arc(c + new Vector2(0f, s * 0.52f), s * 0.1f, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Fill();
            }
        }

        static void Shape(Painter2D p, params Vector2[] pts)
        {
            p.MoveTo(pts[0]);
            for (int i = 1; i < pts.Length; i++) p.LineTo(pts[i]);
        }
    }
}
