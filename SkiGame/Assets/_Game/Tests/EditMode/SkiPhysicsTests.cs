using NUnit.Framework;
using Ski.Core;
using UnityEngine;

namespace Ski.Tests
{
    /// <summary>A plain tilted plane with a flat start, for physics tests.</summary>
    sealed class RampCourse : Course
    {
        readonly float _tan;
        public RampCourse(float degrees, bool flatStart = false)
        {
            _tan = Mathf.Tan(degrees * Mathf.Deg2Rad);
            Walled = false; MinX = -500f; MaxX = 500f; MinZ = -50f; MaxZ = 5000f; FinishZ = 4000f;
            StartZ = flatStart ? -20f : 5f;
        }
        public override float Height(float x, float z) => z < 0f ? 0f : -_tan * z;
    }

    public class SkiPhysicsTests
    {
        const float Dt = SkiSim.FixedDt;

        static SkiSim Make(float degrees, CharacterId who = CharacterId.Erik, bool flatStart = false)
            => new SkiSim(new RampCourse(degrees, flatStart), SkiParams.For(who));

        static void Run(SkiSim s, float seconds, SkiInput inp)
        {
            int n = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < n; i++) { s.Step(inp, Dt); s.Events.Clear(); }
        }

        static float TopSpeed(float degrees, CharacterId who, bool tuck)
        {
            var s = Make(degrees, who);
            Run(s, 90f, new SkiInput { Tuck = tuck });
            return s.Speed;
        }

        [Test]
        public void SlidesDownhillWithExpectedAcceleration()
        {
            var s = Make(15f);
            Run(s, 2f, default);
            float a = 9.81f * (Mathf.Sin(15f * Mathf.Deg2Rad) - s.P.Mu * Mathf.Cos(15f * Mathf.Deg2Rad));
            Assert.That(s.Speed, Is.EqualTo(a * 2f).Within(0.35f), "speed after 2 s");
            Assert.IsTrue(s.Grounded);
            Assert.That(s.Pos.x, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void StaysPutOnFlatGround()
        {
            var s = Make(0f);
            Run(s, 3f, default);
            Assert.That(s.Speed, Is.LessThan(0.01f));
        }

        [Test]
        public void TuckRaisesTopSpeed()
        {
            float up = TopSpeed(20f, CharacterId.Erik, false), tuck = TopSpeed(20f, CharacterId.Erik, true);
            Assert.That(tuck, Is.GreaterThan(up * 1.2f));
            Assert.That(up, Is.InRange(18f, 30f));
        }

        [Test]
        public void SnowploughBrakesToAStop()
        {
            var s = Make(9f);
            Run(s, 8f, default);
            float before = s.Speed;
            Run(s, 6f, new SkiInput { Brake = true });
            Assert.That(before, Is.GreaterThan(6f));
            Assert.That(s.Speed, Is.LessThan(0.5f));
            Assert.That(s.Plough, Is.EqualTo(1f));
        }

        /// <summary>Alternating short turns that keep the lean inside the carve band.</summary>
        static float SpeedAfterWiggle(CharacterId who, float holdSeconds, float seconds, float degrees = 10f)
        {
            var s = Make(degrees, who);
            Run(s, 5f, default);
            int n = Mathf.RoundToInt(seconds / Dt);
            float t = 0f; float dir = 1f;
            for (int i = 0; i < n; i++)
            {
                t += Dt;
                if (t > holdSeconds) { t = 0f; dir = -dir; }
                s.Step(new SkiInput { Steer = dir }, Dt);
                s.Events.Clear();
            }
            return s.Speed;
        }

        [Test]
        public void CarvingGainsSpeedOverGoingStraight()
        {
            var s = Make(10f);
            Run(s, 13f, default);
            float straight = s.Speed;
            float carved = SpeedAfterWiggle(CharacterId.Erik, 0.42f, 8f);
            Assert.That(carved, Is.GreaterThan(straight + 0.5f), $"carved {carved} vs straight {straight}");
        }

        [Test]
        public void TurningTooHardBrakes()
        {
            var s = Make(10f);
            Run(s, 10f, default);
            float before = s.Speed;
            Run(s, 1.6f, new SkiInput { Steer = 1f });
            Assert.That(s.Speed, Is.LessThan(before * 0.8f));
            Assert.That(s.OverLean, Is.GreaterThan(0.9f));
        }

        static float PoleSpeed(CharacterId who, float pressPhase, int pushes)
        {
            var s = Make(0f, who);
            int done = 0;
            for (int i = 0; i < 20000 && done < pushes; i++)
            {
                bool press = s.PoleAvailable && s.PolePhase >= pressPhase && s.PolePhase < pressPhase + 0.03f;
                s.Step(new SkiInput { Pole = press }, Dt);
                foreach (var e in s.Events) if (e.Type == SkiEventType.PolePush) done++;
                s.Events.Clear();
            }
            return s.Speed;
        }

        [Test]
        public void WellTimedPolePushesAreStronger()
        {
            float good = PoleSpeed(CharacterId.Erik, 0.74f, 6), bad = PoleSpeed(CharacterId.Erik, 0.3f, 6);
            Assert.That(good, Is.GreaterThan(3f));
            Assert.That(good, Is.GreaterThan(bad * 2f), $"good {good} bad {bad}");
        }

        [Test]
        public void PolesStopWorkingAtSpeed()
        {
            var s = Make(15f);
            Run(s, 8f, default);
            Assert.IsFalse(s.PoleAvailable);
        }

        [Test]
        public void JumpGoesUpAndLandsCleanly()
        {
            var s = Make(8f);
            Run(s, 6f, default);
            s.Step(new SkiInput { Jump = true }, Dt);
            Assert.IsFalse(s.Grounded);
            float air = 0f; int guard = 0;
            while (!s.Grounded && guard++ < 2000) { s.Step(default, Dt); air += Dt; }
            Assert.That(air, Is.InRange(0.5f, 1.1f));
            Assert.IsFalse(s.Crashed);
        }

        [Test]
        public void Landing360IsScoredAndHalfSpinCrashesOrRidesSwitch()
        {
            var c = new BlueCourse();
            var s = new SkiSim(c, SkiParams.For(CharacterId.Linn));
            // Roll into the first kicker on the jump line.
            s.Teleport(-9f, 125f, 0f);
            s.Vel = new Vector3(0f, -4f, 15f);
            TrickResult landed = null; bool crashed = false;
            for (int i = 0; i < 1500; i++)
            {
                var inp = new SkiInput();
                if (s.RealAir && Mathf.Abs(s.AirYaw) < 2f * Mathf.PI - 1.3f) inp.Steer = 1f;
                if (s.RealAir && s.AirTime < 0.8f) inp.Trick = 1;
                s.Step(inp, Dt);
                foreach (var e in s.Events)
                {
                    if (e.Type == SkiEventType.Landed) landed = e.Trick;
                    if (e.Type == SkiEventType.Crash) crashed = true;
                }
                s.Events.Clear();
                if (landed != null || crashed) break;
            }
            Assert.IsFalse(crashed, "crashed on the small kicker");
            Assert.IsNotNull(landed, "never landed a real jump");
            Assert.That(landed.Points, Is.GreaterThan(200));
            StringAssert.Contains("360", landed.Name);
        }

        [Test]
        public void SameInputsGiveTheSameRun()
        {
            Vector3 Play()
            {
                var session = new RunSession(SlopeId.Red, CharacterId.Maja);
                var bot = new Bot(session);
                for (int i = 0; i < 2400; i++) { session.Step(bot.Decide(), Dt); session.Events.Clear(); }
                return session.Sim.Pos;
            }
            Assert.AreEqual(Play(), Play());
        }

        // ---- traits ------------------------------------------------------------------------

        [Test]
        public void JonteIsSlightlyFasterInATuck()
        {
            float jonte = TopSpeed(24f, CharacterId.Jonte, true), erik = TopSpeed(24f, CharacterId.Erik, true);
            Assert.That(jonte / erik, Is.InRange(1.02f, 1.08f));
            Assert.That(TopSpeed(24f, CharacterId.Jonte, false), Is.EqualTo(TopSpeed(24f, CharacterId.Erik, false)).Within(0.01f));
        }

        [Test]
        public void MajaTurnsTighterWithNoSpeedPenalty()
        {
            float maja = TopSpeed(24f, CharacterId.Maja, true), erik = TopSpeed(24f, CharacterId.Erik, true);
            Assert.That(maja, Is.EqualTo(erik).Within(0.01f), "quick feet must not cost top speed");

            float Loss(CharacterId who)
            {
                var s = Make(10f, who);
                Run(s, 10f, default);
                float before = s.Speed;
                Run(s, 1.2f, new SkiInput { Steer = 1f });
                return 1f - s.Speed / before;
            }
            float m = Loss(CharacterId.Maja), e = Loss(CharacterId.Erik);
            Assert.That(m, Is.LessThan(e * 0.95f));
            Assert.That(m, Is.GreaterThan(e * 0.6f), "the perk should stay modest");
        }

        [Test]
        public void ErikGainsMoreFromCarving()
        {
            float erik = SpeedAfterWiggle(CharacterId.Erik, 0.42f, 8f), linn = SpeedAfterWiggle(CharacterId.Linn, 0.42f, 8f);
            Assert.That(erik / linn, Is.InRange(1.015f, 1.1f));
        }

        [Test]
        public void LinnHasAnEasierPoleRhythm()
        {
            // Slightly early pushes still count as perfect for Linn, not for the others.
            float linn = PoleSpeed(CharacterId.Linn, 0.61f, 6), erik = PoleSpeed(CharacterId.Erik, 0.61f, 6);
            Assert.That(linn, Is.GreaterThan(erik * 1.2f));
        }
    }
}
