using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ski.Game
{
    /// <summary>
    /// New Year's fireworks over the village for a new high score: rockets climb from behind the
    /// plaza, burst into palette-coloured stars and light the snow for a moment.
    /// </summary>
    public sealed class Fireworks : MonoBehaviour
    {
        ParticleSystem _trail, _burst, _glitter;
        Light _flash;
        readonly List<Rocket> _rockets = new List<Rocket>();
        Vector3 _origin;
        float _spread, _next, _timeLeft;
        int _seed = 1;

        struct Rocket { public Vector3 Pos, Vel; public float Fuse; public Color Colour; }

        static readonly Color[] Colours =
        {
            Palette.Lamp, Palette.SignalRed, Palette.Jackets[0], Palette.Jackets[3], Palette.PisteGreen, Palette.Jackets[1], Palette.White,
        };

        /// <summary>Starts a show centred on origin (ground level), lasting the given seconds.</summary>
        public static Fireworks Launch(Transform parent, Vector3 origin, float spread = 34f, float seconds = 14f)
        {
            var go = new GameObject("Fireworks");
            go.transform.SetParent(parent, false);
            var f = go.AddComponent<Fireworks>();
            f._origin = origin; f._spread = spread; f._timeLeft = seconds;
            f.Build();
            return f;
        }

        ParticleSystem Make(string name, float size, float lifetime, float gravity, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false; main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = lifetime; main.startSize = size; main.startSpeed = 0f;
            main.gravityModifier = gravity; main.maxParticles = max;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.dampen = 0.06f; lim.limit = 3f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mats.Particle(Mats.SoftDot, true, 0f);
            r.sharedMaterial.SetColor("_BaseColor", new Color(2.4f, 2.4f, 2.4f, 1f)); // bright enough to bloom
            r.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        void Build()
        {
            _trail = Make("Trails", 0.5f, 0.7f, 0.05f, 1200);
            _burst = Make("Bursts", 1.1f, 1.9f, 0.22f, 5000);
            _glitter = Make("Glitter", 0.45f, 2.6f, 0.5f, 3000);
            var lg = new GameObject("Flash");
            lg.transform.SetParent(transform, false);
            _flash = lg.AddComponent<Light>();
            _flash.type = LightType.Point; _flash.range = 160f; _flash.intensity = 0f; _flash.shadows = LightShadows.None;
        }

        float Rnd() { _seed = _seed * 1103515245 + 12345; return ((_seed >> 8) & 0xFFFF) / 65535f; }

        void Update()
        {
            float dt = Time.deltaTime;
            _timeLeft -= dt;
            _next -= dt;
            if (_timeLeft > 0f && _next <= 0f)
            {
                _next = 0.25f + Rnd() * 0.75f;
                Vector3 p = _origin + new Vector3((Rnd() - 0.5f) * 2f * _spread, 0f, (Rnd() - 0.5f) * 14f);
                _rockets.Add(new Rocket
                {
                    Pos = p, Vel = new Vector3((Rnd() - 0.5f) * 7f, 30f + Rnd() * 12f, (Rnd() - 0.5f) * 5f),
                    Fuse = 1.25f + Rnd() * 0.55f, Colour = Colours[(int)(Rnd() * (Colours.Length - 0.01f))],
                });
                AudioDirector.Sfx("fw_launch", 0.35f, 0.85f + Rnd() * 0.4f);
            }

            for (int i = _rockets.Count - 1; i >= 0; i--)
            {
                var r = _rockets[i];
                r.Vel += Vector3.down * 9f * dt;
                r.Pos += r.Vel * dt;
                r.Fuse -= dt;
                var ep = new ParticleSystem.EmitParams { position = r.Pos, startColor = Palette.Lamp, velocity = -r.Vel * 0.1f };
                _trail.Emit(ep, 2);
                if (r.Fuse <= 0f) { Burst(r); _rockets.RemoveAt(i); }
                else _rockets[i] = r;
            }
            _flash.intensity = Mathf.MoveTowards(_flash.intensity, 0f, dt * 900f);
            if (_timeLeft < -4f && _rockets.Count == 0) Destroy(gameObject);
        }

        void Burst(Rocket r)
        {
            int n = 70 + (int)(Rnd() * 50f);
            float power = 13f + Rnd() * 9f;
            bool ring = Rnd() < 0.25f;
            Color second = Colours[(int)(Rnd() * (Colours.Length - 0.01f))];
            for (int i = 0; i < n; i++)
            {
                // Even-ish directions on a sphere (or a tilted ring).
                float u = Rnd() * 2f - 1f, a = Rnd() * Mathf.PI * 2f, s = Mathf.Sqrt(1f - u * u);
                Vector3 dir = ring ? Quaternion.Euler(40f, 20f, 0f) * new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) : new Vector3(s * Mathf.Cos(a), u, s * Mathf.Sin(a));
                var ep = new ParticleSystem.EmitParams
                {
                    position = r.Pos, velocity = dir * power * (0.85f + Rnd() * 0.3f) + r.Vel * 0.15f,
                    startColor = i % 3 == 0 ? second : r.Colour,
                };
                _burst.Emit(ep, 1);
                if (i % 4 == 0)
                {
                    ep.velocity *= 0.6f; ep.startColor = Palette.White;
                    _glitter.Emit(ep, 1);
                }
            }
            _flash.transform.position = r.Pos;
            _flash.color = r.Colour;
            _flash.intensity = 420f;
            AudioDirector.Sfx("fw_boom", 0.8f, 0.8f + Rnd() * 0.4f);
            if (Rnd() < 0.6f) AudioDirector.Sfx("fw_crackle", 0.5f, 0.9f + Rnd() * 0.3f);
        }
    }
}
