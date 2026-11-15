using UnityEngine;

namespace Ski.Game
{
    /// <summary>Skis, boards, poles and boots. Local space: lying on the snow, +Z forward, origin under the boot.</summary>
    public static class GearModels
    {
        /// <summary>One ski. The tip curls up; twin-tips curl up at the tail too.</summary>
        public static void Ski(MeshBuilder b, float length, bool twinTip, Color colour, Color accent, float width = 0.09f)
        {
            float front = length * 0.56f, back = -length * 0.44f;
            const float th = 0.022f;
            float tipLen = Mathf.Min(0.22f, length * 0.16f);
            // Flat running surface.
            float flat0 = back + (twinTip ? tipLen : 0.03f), flat1 = front - tipLen;
            b.Col = colour;
            b.Box(new Vector3(0, th * 0.5f, (flat0 + flat1) * 0.5f), new Vector3(width, th, flat1 - flat0));
            // Top sheet stripe and binding plates.
            b.Col = accent;
            b.Box(new Vector3(0, th + 0.003f, flat1 - 0.22f), new Vector3(width * 0.6f, 0.006f, 0.3f));
            b.Col = Palette.Ink;
            b.Box(new Vector3(0, th + 0.02f, 0.17f), new Vector3(width * 0.8f, 0.04f, 0.1f));
            b.Box(new Vector3(0, th + 0.025f, -0.17f), new Vector3(width * 0.8f, 0.05f, 0.1f));
            // Curled ends.
            Curl(b, flat1, 1f, tipLen, width, th, colour);
            if (twinTip) Curl(b, flat0, -1f, tipLen, width, th, colour);
            else { b.Col = colour; b.Box(new Vector3(0, th * 0.5f, back + 0.015f), new Vector3(width * 0.9f, th, 0.03f)); }
        }

        static void Curl(MeshBuilder b, float z0, float dir, float len, float width, float th, Color colour)
        {
            const int n = 4;
            Vector3 prev = new Vector3(0, 0, z0);
            float prevW = width;
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n;
                float ang = t * 0.95f;
                Vector3 p = new Vector3(0, len * 0.62f * (1f - Mathf.Cos(ang)), z0 + dir * len * Mathf.Sin(ang) / Mathf.Sin(0.95f));
                float w = width * (1f - 0.55f * t * t);
                b.Col = colour;
                Vector3 l0 = prev + Vector3.left * prevW * 0.5f, r0 = prev + Vector3.right * prevW * 0.5f;
                Vector3 l1 = p + Vector3.left * w * 0.5f, r1 = p + Vector3.right * w * 0.5f;
                Vector3 up = Vector3.up * th;
                if (dir > 0f) { b.Quad(l0 + up, l1 + up, r1 + up, r0 + up); b.Quad(r0, r1, l1, l0); b.Quad(l0, l1, l1 + up, l0 + up); b.Quad(r0 + up, r1 + up, r1, r0); }
                else { b.Quad(r0 + up, r1 + up, l1 + up, l0 + up); b.Quad(l0, l1, r1, r0); b.Quad(l0 + up, l1 + up, l1, l0); b.Quad(r0, r1, r1 + up, r0 + up); }
                prev = p; prevW = w;
            }
        }

        /// <summary>A snowboard, long axis along +Z, origin at its centre.</summary>
        public static void Board(MeshBuilder b, float length, Color colour, Color accent)
        {
            const float w = 0.27f, th = 0.024f;
            float tip = 0.2f, half = length * 0.5f - tip;
            b.Col = colour;
            b.Box(new Vector3(0, th * 0.5f, 0), new Vector3(w, th, half * 2f));
            b.Col = accent;
            b.Box(new Vector3(0, th + 0.003f, 0), new Vector3(w * 0.5f, 0.006f, half * 0.9f));
            Curl(b, half, 1f, tip, w, th, colour);
            Curl(b, -half, -1f, tip, w, th, colour);
            // Bindings.
            b.Col = Palette.Ink;
            for (int s = -1; s <= 1; s += 2)
            {
                b.Box(new Vector3(0, th + 0.02f, s * 0.26f), new Vector3(w * 0.95f, 0.04f, 0.13f));
                b.Box(new Vector3(-w * 0.42f, th + 0.1f, s * 0.26f), new Vector3(0.03f, 0.2f, 0.13f));
            }
        }

        /// <summary>A ski pole hanging down from the grip at the origin, length along -Y.</summary>
        public static void Pole(MeshBuilder b, float length, Color colour)
        {
            b.Col = Palette.Ink;
            b.Cylinder(new Vector3(0, -0.02f, 0), 0.02f, 0.017f, 0.15f, 6);
            b.Col = colour;
            b.Cylinder(new Vector3(0, -length, 0), 0.006f, 0.011f, length, 5, true, false);
            b.Col = Palette.Ink;
            b.Cylinder(new Vector3(0, -length + 0.07f, 0), 0.045f, 0.045f, 0.008f, 8);
        }

        /// <summary>A ski boot standing on the origin, toe toward +Z.</summary>
        public static void Boot(MeshBuilder b, Color colour)
        {
            // Sole.
            b.Col = Palette.Ink;
            b.Box(new Vector3(0, 0.012f, 0.02f), new Vector3(0.104f, 0.024f, 0.31f));
            // Foot shell: a rounded shape from heel to toe (the loft runs along +Z).
            b.Col = colour;
            b.Push(new Vector3(0, 0.02f, 0), Quaternion.Euler(90f, 0f, 0f));
            b.Loft(new[]
            {
                new MeshBuilder.Ring(-0.13f, 0.044f, 0.04f, -0.045f),
                new MeshBuilder.Ring(-0.09f, 0.052f, 0.056f, -0.058f),
                new MeshBuilder.Ring(0.0f, 0.054f, 0.06f, -0.06f),
                new MeshBuilder.Ring(0.08f, 0.053f, 0.046f, -0.048f),
                new MeshBuilder.Ring(0.145f, 0.047f, 0.034f, -0.036f),
                new MeshBuilder.Ring(0.172f, 0.03f, 0.022f, -0.026f),
            }, 8, true, true);
            b.Pop();
            // Cuff, leaning forward.
            b.Loft(new[]
            {
                new MeshBuilder.Ring(0.09f, 0.052f, 0.064f, -0.03f),
                new MeshBuilder.Ring(0.2f, 0.05f, 0.058f, -0.018f),
                new MeshBuilder.Ring(0.32f, 0.054f, 0.06f, -0.004f),
            }, 8, false, true);
            // Buckles.
            b.Col = Palette.Ink;
            b.Box(new Vector3(0.02f, 0.1f, 0.115f), new Vector3(0.07f, 0.016f, 0.02f));
            b.Box(new Vector3(0.02f, 0.2f, 0.042f), new Vector3(0.08f, 0.018f, 0.02f));
            b.Box(new Vector3(0.02f, 0.275f, 0.054f), new Vector3(0.08f, 0.018f, 0.02f));
        }
    }
}
