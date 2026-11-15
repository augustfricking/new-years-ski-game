using Ski.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ski.Game
{
    /// <summary>
    /// Snow thrown up by the skis: a plume to the outside of a hard turn, a fan from the
    /// snowplough, a puff on landings and pole plants, a cloud in a crash.
    /// </summary>
    public sealed class SnowFx : MonoBehaviour
    {
        ParticleSystem _spray;
        float _carry;
        uint _seed = 7;

        public static SnowFx Create(Transform parent)
        {
            var go = new GameObject("SnowFx");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<SnowFx>();
            fx.Build();
            return fx;
        }

        void Build()
        {
            _spray = gameObject.AddComponent<ParticleSystem>();
            var main = _spray.main;
            main.playOnAwake = false; main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 0.7f; main.startSpeed = 0f;
            main.startSize = 0.2f;
            main.gravityModifier = 0.9f;
            main.maxParticles = 1500;
            var em = _spray.emission; em.enabled = false;
            var sh = _spray.shape; sh.enabled = false;
            var size = _spray.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1.5f)));
            var col = _spray.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Palette.Snow, 0f), new GradientColorKey(Palette.Snow, 1f) },
                      new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mats.Particle(Mats.SoftDot, false, 0.4f);
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        float Rnd() { _seed ^= _seed << 13; _seed ^= _seed >> 17; _seed ^= _seed << 5; return (_seed & 0xFFFF) / 65535f; }

        /// <summary>Call every frame while skiing.</summary>
        public void Tick(SkiSim sim, float dt)
        {
            if (!sim.Contact || sim.Crashed) return;
            float speed = sim.Speed;
            if (speed < 2.5f) return;
            float amount = Mathf.Max(sim.OverLean, sim.SkidAmount * 0.8f, sim.Plough, sim.CarveQuality * 0.3f) * Mathf.Clamp01(speed / 11f);
            if (amount < 0.05f) return;
            _carry += amount * dt * 170f;
            int n = (int)_carry;
            _carry -= n;
            Vector3 fwd = sim.Forward, right = Vector3.Cross(Vector3.up, fwd);
            float outside = sim.Plough > 0.5f ? 0f : -Mathf.Sign(sim.Lean);
            for (int i = 0; i < n; i++)
            {
                float side = sim.Plough > 0.5f ? (Rnd() < 0.5f ? -1f : 1f) : outside;
                var ep = new ParticleSystem.EmitParams
                {
                    position = sim.Pos + right * (side * 0.3f + (Rnd() - 0.5f) * 0.3f) + fwd * (Rnd() * 0.9f - 0.2f) + Vector3.up * 0.05f,
                    velocity = sim.Vel * 0.55f + right * side * (1.5f + speed * 0.16f) * (0.5f + Rnd()) + Vector3.up * (1.2f + Rnd() * 2f + speed * 0.05f),
                    startSize = 0.14f + Rnd() * 0.26f,
                    startLifetime = 0.45f + Rnd() * 0.5f,
                };
                _spray.Emit(ep, 1);
            }
        }

        /// <summary>A round puff at a point (landing, crash, pole plant).</summary>
        public void Puff(Vector3 pos, Vector3 carry, int count, float power)
        {
            for (int i = 0; i < count; i++)
            {
                float a = Rnd() * Mathf.PI * 2f;
                var ep = new ParticleSystem.EmitParams
                {
                    position = pos + new Vector3(Mathf.Cos(a), 0.1f, Mathf.Sin(a)) * 0.3f,
                    velocity = carry * 0.5f + new Vector3(Mathf.Cos(a), 0.5f + Rnd(), Mathf.Sin(a)) * power * (0.4f + Rnd()),
                    startSize = 0.2f + Rnd() * 0.4f,
                    startLifetime = 0.5f + Rnd() * 0.6f,
                };
                _spray.Emit(ep, 1);
            }
        }
    }
}
