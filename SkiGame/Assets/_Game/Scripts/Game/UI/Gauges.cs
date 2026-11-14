using UnityEngine;
using UnityEngine.UIElements;

namespace Ski.Game
{
    /// <summary>
    /// The pole-push timing ring: a dot runs around the ring; press Shift while it is inside the
    /// bright zone. The zone and the speed of the dot come from the simulation.
    /// </summary>
    public sealed class PoleRing : VisualElement
    {
        public float Phase, SweetSpot = 0.75f, Window = 0.11f, OkWindow = 0.24f;
        public float Flash;          // 0..1, fades after a push
        public Color FlashColour = Color.white;

        public PoleRing()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("gauge");
            generateVisualContent += Draw;
        }

        static Angle A(float phase) => Angle.Degrees(phase * 360f - 90f);

        void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            var r = contentRect;
            Vector2 c = r.center;
            float radius = Mathf.Min(r.width, r.height) * 0.5f - 16f;

            p.lineCap = LineCap.Butt;
            p.lineWidth = 22f; p.strokeColor = Palette.Ink;
            p.BeginPath(); p.Arc(c, radius, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
            p.lineWidth = 14f; p.strokeColor = new Color(0.93f, 0.95f, 0.98f, 0.35f);
            p.BeginPath(); p.Arc(c, radius, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
            // OK zone, then the perfect zone on top.
            p.strokeColor = Palette.UiAccent;
            p.BeginPath(); p.Arc(c, radius, A(SweetSpot - OkWindow), A(SweetSpot + OkWindow)); p.Stroke();
            p.strokeColor = Palette.UiGood;
            p.BeginPath(); p.Arc(c, radius, A(SweetSpot - Window), A(SweetSpot + Window)); p.Stroke();

            // The runner.
            float a = (Phase * 360f - 90f) * Mathf.Deg2Rad;
            Vector2 dot = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            p.fillColor = Palette.Ink;
            p.BeginPath(); p.Arc(dot, 15f, Angle.Degrees(0), Angle.Degrees(360)); p.Fill();
            p.fillColor = Palette.White;
            p.BeginPath(); p.Arc(dot, 10f, Angle.Degrees(0), Angle.Degrees(360)); p.Fill();

            if (Flash > 0.01f)
            {
                var fc = FlashColour; fc.a = Flash;
                p.lineWidth = 8f; p.strokeColor = fc;
                p.BeginPath(); p.Arc(c, radius + 14f + (1f - Flash) * 22f, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
            }
        }
    }

    /// <summary>
    /// The edge meter: shows how hard the skis are edged. Green zones on both sides are the carve
    /// band (speed gain), the red ends are skidding (braking).
    /// </summary>
    public sealed class CarveMeter : VisualElement
    {
        public float Lean, CarveMin = 0.18f, CarveMax = 0.72f, Quality;

        public CarveMeter()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("gauge-wide");
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            var r = contentRect;
            float x0 = r.xMin + 16f, x1 = r.xMax - 16f, y = r.center.y + 6f, mid = (x0 + x1) * 0.5f, half = (x1 - x0) * 0.5f;
            p.lineCap = LineCap.Round;
            p.lineWidth = 26f; p.strokeColor = Palette.Ink;
            p.BeginPath(); p.MoveTo(new Vector2(x0, y)); p.LineTo(new Vector2(x1, y)); p.Stroke();
            p.lineCap = LineCap.Butt;
            p.lineWidth = 16f;
            p.strokeColor = new Color(0.93f, 0.95f, 0.98f, 0.35f);
            p.BeginPath(); p.MoveTo(new Vector2(x0, y)); p.LineTo(new Vector2(x1, y)); p.Stroke();
            for (int s = -1; s <= 1; s += 2)
            {
                p.strokeColor = Color.Lerp(Palette.UiGood, Palette.White, Quality * 0.45f);
                p.BeginPath(); p.MoveTo(new Vector2(mid + s * half * CarveMin, y)); p.LineTo(new Vector2(mid + s * half * CarveMax, y)); p.Stroke();
                p.strokeColor = Palette.UiBad;
                p.BeginPath(); p.MoveTo(new Vector2(mid + s * half * CarveMax, y)); p.LineTo(new Vector2(mid + s * half, y)); p.Stroke();
            }
            // Marker.
            float mx = mid + Mathf.Clamp(Lean, -1f, 1f) * half;
            p.fillColor = Palette.Ink;
            p.BeginPath(); p.MoveTo(new Vector2(mx - 15f, y - 34f)); p.LineTo(new Vector2(mx + 15f, y - 34f)); p.LineTo(new Vector2(mx, y - 6f)); p.ClosePath(); p.Fill();
            p.fillColor = Palette.UiAccent;
            p.BeginPath(); p.MoveTo(new Vector2(mx - 9f, y - 31f)); p.LineTo(new Vector2(mx + 9f, y - 31f)); p.LineTo(new Vector2(mx, y - 13f)); p.ClosePath(); p.Fill();
        }
    }

    /// <summary>A horizontal bar with a target zone and a moving marker (beer minigame, generic meters).</summary>
    public sealed class ZoneBar : VisualElement
    {
        public float Value;                 // 0..1 marker
        public float ZoneMin = 0.4f, ZoneMax = 0.6f;
        public float Fill = -1f;            // 0..1 filled part, or negative for none
        public Color ZoneColour = Palette.UiGood, FillColour = Palette.UiAccent;

        public ZoneBar()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("mini-bar");
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            var r = contentRect;
            float x0 = r.xMin + 14f, x1 = r.xMax - 14f, y = r.center.y;
            p.lineCap = LineCap.Round;
            p.lineWidth = r.height - 6f; p.strokeColor = Palette.Ink;
            p.BeginPath(); p.MoveTo(new Vector2(x0, y)); p.LineTo(new Vector2(x1, y)); p.Stroke();
            p.lineCap = LineCap.Butt;
            p.lineWidth = r.height - 22f; p.strokeColor = Palette.UiPaper;
            p.BeginPath(); p.MoveTo(new Vector2(x0, y)); p.LineTo(new Vector2(x1, y)); p.Stroke();
            if (Fill >= 0f)
            {
                p.strokeColor = FillColour;
                p.BeginPath(); p.MoveTo(new Vector2(x0, y)); p.LineTo(new Vector2(Mathf.Lerp(x0, x1, Mathf.Clamp01(Fill)), y)); p.Stroke();
            }
            if (ZoneMax > ZoneMin)
            {
                p.strokeColor = ZoneColour;
                p.BeginPath(); p.MoveTo(new Vector2(Mathf.Lerp(x0, x1, ZoneMin), y)); p.LineTo(new Vector2(Mathf.Lerp(x0, x1, ZoneMax), y)); p.Stroke();
            }
            float mx = Mathf.Lerp(x0, x1, Mathf.Clamp01(Value));
            p.lineWidth = 10f; p.strokeColor = Palette.Ink; p.lineCap = LineCap.Round;
            p.BeginPath(); p.MoveTo(new Vector2(mx, r.yMin + 2f)); p.LineTo(new Vector2(mx, r.yMax - 2f)); p.Stroke();
            p.lineWidth = 4f; p.strokeColor = Palette.SignalRed;
            p.BeginPath(); p.MoveTo(new Vector2(mx, r.yMin + 5f)); p.LineTo(new Vector2(mx, r.yMax - 5f)); p.Stroke();
        }
    }
}
