using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ski.Game
{
    /// <summary>Creates and caches the game's materials. All of them use the Ski/* shaders.</summary>
    public static class Mats
    {
        static Shader _toon, _sky, _particle, _unlit;
        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        static Texture2D _softDot, _white;

        static Shader Load(ref Shader slot, string name)
        {
            if (slot == null) slot = Resources.Load<Shader>("Shaders/" + name);
            if (slot == null) Debug.LogError("Missing shader " + name);
            return slot;
        }

        public static Shader ToonShader => Load(ref _toon, "SkiToon");
        public static Shader SkyShader => Load(ref _sky, "SkiSky");
        public static Shader ParticleShader => Load(ref _particle, "SkiParticle");
        public static Shader UnlitShader => Load(ref _unlit, "SkiUnlit");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Cache.Clear(); _softDot = null; _white = null; }

        /// <summary>Vertex-coloured toon material with an ink outline. Most props share this one.</summary>
        public static Material Props => Toon("props", Color.white, 2f);
        public static Material PropsNoOutline => Toon("props_flat", Color.white, 0f);
        public static Material Trees
        {
            get
            {
                var m = Toon("trees", Color.white, 1.6f, wind: 1f);
                m.SetFloat("_FlatPrepass", 1f); // silhouette lines only, not one per facet
                return m;
            }
        }

        /// <summary>People: deeper inked shadows and a cool rim from behind, as on a comic page.</summary>
        public static Material Character
        {
            get
            {
                if (Cache.TryGetValue("character", out var m) && m != null) return m;
                m = NewToon(Color.white, 2.1f);
                m.name = "Character";
                m.SetFloat("_InkShadow", 0.26f);
                m.SetFloat("_BackRim", 0.55f);
                m.SetColor("_BackRimColor", new Color(0.5f, 0.78f, 1f));
                m.SetFloat("_RimStrength", 0.3f);
                m.SetFloat("_Paint", 0.35f);
                m.SetFloat("_Wobble", 0.45f);
                m.SetFloat("_MidBand", 0.3f);
                m.SetColor("_ShadowTint", new Color(0.6f, 0.54f, 0.76f));
                Cache["character"] = m;
                return m;
            }
        }

        public static Material Snow
        {
            get
            {
                if (Cache.TryGetValue("snow", out var m) && m != null) return m;
                m = NewToon(Color.white, 0f);
                m.name = "Snow";
                m.SetColor("_ShadowTint", Palette.SnowShadow);
                m.SetFloat("_Snow", 1f);
                m.SetFloat("_FlatPrepass", 1f);
                m.SetFloat("_Paint", 0f);
                m.SetFloat("_Wobble", 0f);
                m.SetFloat("_MidBand", 0f);
                m.SetFloat("_RimStrength", 0f);
                m.SetFloat("_BandEdge", -0.05f);
                Cache["snow"] = m;
                return m;
            }
        }

        public static Material Toon(string key, Color color, float outline = 2f, float wind = 0f, float emission = 0f)
        {
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = NewToon(color, outline);
            m.name = key;
            if (wind > 0f) m.SetFloat("_Wind", wind);
            if (emission > 0f) m.SetColor("_Emission", color * emission);
            Cache[key] = m;
            return m;
        }

        /// <summary>A plain coloured toon material, cached by colour.</summary>
        public static Material Colour(Color c, float outline = 2f) => Toon("c_" + ColorUtility.ToHtmlStringRGB(c) + "_" + outline, c, outline);

        static Material NewToon(Color color, float outline)
        {
            var m = new Material(ToonShader) { enableInstancing = true };
            m.SetColor("_BaseColor", color);
            m.SetColor("_ShadowTint", new Color(0.52f, 0.5f, 0.72f));
            m.SetColor("_OutlineColor", Palette.Ink);
            m.SetFloat("_OutlineWidth", outline);
            if (outline <= 0f) m.SetShaderPassEnabled("SRPDefaultUnlit", false);
            return m;
        }

        public static Material Glow(Color color, float strength = 1.6f)
        {
            string key = "glow_" + ColorUtility.ToHtmlStringRGB(color) + strength;
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(UnlitShader) { name = key };
            m.SetColor("_BaseColor", color * strength);
            m.SetFloat("_Fog", 0.5f);
            Cache[key] = m;
            return m;
        }

        public static Material Unlit(Texture tex, Color tint)
        {
            var m = new Material(UnlitShader);
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", tint);
            return m;
        }

        public static Material Particle(Texture tex, bool additive, float fog = 1f)
        {
            var m = new Material(ParticleShader);
            m.SetTexture("_BaseMap", tex != null ? tex : SoftDot);
            m.SetFloat("_SrcBlend", (float)(additive ? BlendMode.SrcAlpha : BlendMode.SrcAlpha));
            m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_Fog", fog);
            return m;
        }

        public static Material Sky()
        {
            var m = new Material(SkyShader) { name = "Sky" };
            m.SetColor("_Top", Palette.SkyTop);
            m.SetColor("_Mid", Palette.SkyMid);
            m.SetColor("_Horizon", Palette.SkyHorizon);
            m.SetColor("_Bottom", Palette.SnowShadow);
            m.SetColor("_SunColor", Palette.Sun);
            m.SetColor("_RidgeFar", Palette.RidgeFar);
            m.SetColor("_RidgeNear", Palette.RidgeNear);
            m.SetColor("_StormColor", Palette.Storm);
            return m;
        }

        /// <summary>Round soft dot, white with alpha falloff. For snow flakes, sparks and glows.</summary>
        public static Texture2D SoftDot
        {
            get
            {
                if (_softDot != null) return _softDot;
                const int n = 64;
                _softDot = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "SoftDot" };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                        float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                        a = a * a * (3f - 2f * a);
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                _softDot.SetPixels32(px);
                _softDot.Apply(false, true);
                return _softDot;
            }
        }

        public static Texture2D WhiteTex
        {
            get
            {
                if (_white != null) return _white;
                _white = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "White" };
                _white.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) });
                _white.Apply(false, true);
                return _white;
            }
        }
    }
}
