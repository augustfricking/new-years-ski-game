using UnityEngine;

namespace Ski.Core
{
    /// <summary>
    /// The tavern minigame: empty a stein as fast as you can. A marker sweeps back and forth over a
    /// bar; press when it is inside the green zone to take a gulp. Every gulp moves and shrinks the
    /// zone and speeds the marker up. A press outside the zone spills beer and costs time.
    /// </summary>
    public sealed class ChugGame
    {
        public const int GulpsToFinish = 14;
        public const float SpillPenalty = 0.8f;

        public float Marker;                 // 0..1
        public float ZoneMin, ZoneMax;
        public int Gulps, Spills;
        public float Time;                   // seconds incl. penalties
        public bool Running, Finished;
        public float Level => 1f - Gulps / (float)GulpsToFinish; // beer left in the glass

        float _phase, _speed;
        readonly Rng _rng;

        public ChugGame(int seed = 1)
        {
            _rng = new Rng(seed);
            _speed = 0.9f;
            NewZone();
        }

        void NewZone()
        {
            float width = Mathf.Lerp(0.24f, 0.1f, Gulps / (float)GulpsToFinish);
            float centre = _rng.Range(0.12f + width * 0.5f, 0.88f - width * 0.5f);
            ZoneMin = centre - width * 0.5f; ZoneMax = centre + width * 0.5f;
        }

        public void Start() { Running = true; }

        public void Step(float dt)
        {
            if (!Running || Finished) return;
            Time += dt;
            _phase += dt * _speed;
            // Triangle wave: sweeps 0 -> 1 -> 0.
            float t = _phase % 2f;
            Marker = t < 1f ? t : 2f - t;
        }

        /// <summary>Player pressed. Returns true for a gulp, false for a spill.</summary>
        public bool Press()
        {
            if (!Running || Finished) return false;
            if (Marker >= ZoneMin && Marker <= ZoneMax)
            {
                Gulps++;
                _speed += 0.12f;
                if (Gulps >= GulpsToFinish) { Finished = true; Running = false; }
                else NewZone();
                return true;
            }
            Spills++;
            Time += SpillPenalty;
            return false;
        }
    }
}
