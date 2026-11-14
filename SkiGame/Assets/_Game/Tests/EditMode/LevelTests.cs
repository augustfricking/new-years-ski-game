using System.Text;
using NUnit.Framework;
using Ski.Core;
using UnityEngine;

namespace Ski.Tests
{
    public class LevelTests
    {
        static RunSession PlayBot(SlopeId slope, CharacterId who, out bool finished)
        {
            var s = new RunSession(slope, who);
            finished = Bot.Play(s, new Bot(s));
            return s;
        }

        [TestCase(SlopeId.Green)]
        [TestCase(SlopeId.Blue)]
        [TestCase(SlopeId.Red)]
        [TestCase(SlopeId.Black)]
        [TestCase(SlopeId.OffPiste)]
        public void BotFinishesTheSlope(SlopeId slope)
        {
            var s = PlayBot(slope, CharacterId.Erik, out bool finished);
            Debug.Log($"{slope}: finished={finished} time={s.Time:0.0} penalty={s.Penalty:0} score={s.Score} top={s.TopSpeed * 3.6f:0} km/h crashes={s.Sim.CrashCount} z={s.Sim.Pos.z:0}/{s.Course.FinishZ:0}");
            Assert.IsTrue(finished, $"bot stuck at z={s.Sim.Pos.z:0} of {s.Course.FinishZ:0}");
            Assert.IsFalse(float.IsNaN(s.Sim.Pos.x + s.Sim.Pos.y + s.Sim.Pos.z));
            Assert.That(s.Time, Is.InRange(20f, 240f));
        }

        [Test]
        public void SlalomBotClearsMostGates()
        {
            var s = PlayBot(SlopeId.Red, CharacterId.Erik, out _);
            Assert.That(s.Gates.Count, Is.GreaterThan(25));
            Assert.That(s.GatesMissed, Is.LessThanOrEqualTo(3), "gates missed");
        }

        [Test]
        public void MissingAGateCostsTime()
        {
            var s = new RunSession(SlopeId.Red, CharacterId.Erik);
            // Go straight down the middle: this misses gates.
            for (int i = 0; i < 6000 && s.NextGate < 4; i++) { s.Step(new SkiInput { Pole = i % 90 == 0 }, SkiSim.FixedDt); }
            Assert.That(s.GatesMissed, Is.GreaterThan(0));
            Assert.That(s.Penalty, Is.EqualTo(s.GatesMissed * RunSession.GatePenalty));
        }

        [Test]
        public void ParkBotScoresPoints()
        {
            var s = PlayBot(SlopeId.Blue, CharacterId.Linn, out _);
            Assert.That(s.Score, Is.GreaterThan(500));
        }

        [Test]
        public void TricksDoNotScoreOutsideThePark()
        {
            var s = PlayBot(SlopeId.Black, CharacterId.Linn, out _);
            Assert.AreEqual(0, s.Score);
        }

        [Test]
        public void GreenSlopeHasAllFourKindsOfChildren()
        {
            var s = new RunSession(SlopeId.Green, CharacterId.Erik);
            int trav = 0, harness = 0, school = 0;
            foreach (var k in s.Crowd.Kids)
            {
                if (k.Kind == KidKind.Traverser) trav++;
                if (k.Kind == KidKind.HarnessKid) harness++;
                if (k.Kind == KidKind.SchoolKid) school++;
            }
            Assert.That(trav, Is.GreaterThan(3));
            Assert.That(harness, Is.GreaterThan(2));
            Assert.That(school, Is.GreaterThan(12));
            // Bombers arrive during the run.
            for (int i = 0; i < 120 * 30; i++) s.Step(new SkiInput { Pole = i % 80 == 0 }, SkiSim.FixedDt);
            int bombers = 0;
            foreach (var k in s.Crowd.Kids) if (k.Kind == KidKind.Bomber) bombers++;
            Assert.That(bombers, Is.GreaterThan(1));
        }

        [Test]
        public void HittingAChildCrashesAndCostsTime()
        {
            var s = new RunSession(SlopeId.Green, CharacterId.Erik);
            Kid target = null;
            foreach (var k in s.Crowd.Kids) if (k.Kind == KidKind.Traverser) { target = k; break; }
            s.Sim.Teleport(target.X, target.Z - 0.3f, 0f);
            s.Sim.Vel = new Vector3(0f, 0f, 6f);
            s.Step(default, SkiSim.FixedDt);
            s.Step(default, SkiSim.FixedDt);
            Assert.AreEqual(1, s.PeopleHit);
            Assert.IsTrue(s.Sim.Crashed);
            Assert.AreEqual(RunSession.PersonPenalty, s.Penalty);
        }

        [Test]
        public void OffPisteHasFewWaysDownAndMoreDeadEnds()
        {
            var c = new OffPisteCourse();
            Assert.That(c.Solutions.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(c.DeadEnds.Count, Is.GreaterThan(c.Solutions.Count));
            // Every way-point of a valid route is on open ground.
            foreach (var route in c.Solutions)
                foreach (var p in route)
                    if (p.y < c.FinishZ) Assert.That(c.Clear(p.x, p.y), Is.GreaterThan(3f), $"route point {p} is in the forest");
            Debug.Log($"Off-piste: {c.Solutions.Count} routes, {c.DeadEnds.Count} dead ends, {c.Obstacles.Count} obstacles, {c.Segments.Count} glade segments");
        }

        [Test]
        public void OffPisteCliffSendsYouBackToTheFork()
        {
            var c = new OffPisteCourse();
            OffPisteCourse.DeadEnd cliff = default; bool found = false;
            foreach (var d in c.DeadEnds) if (d.Kind == OffPisteCourse.DeadEndKind.Cliff) { cliff = d; found = true; break; }
            Assume.That(found, "no cliff generated with this seed");
            var s = new RunSession(SlopeId.OffPiste, CharacterId.Erik, c);
            s.Phase = RunPhase.Running;
            s.Sim.Teleport(cliff.Zone.X, cliff.Zone.Z - 40f, 0f);
            s.Sim.Vel = new Vector3(0f, 0f, 14f);
            bool sentBack = false;
            for (int i = 0; i < 120 * 12 && !sentBack; i++)
            {
                s.Step(default, SkiSim.FixedDt);
                foreach (var e in s.Events) if (e.Type == RunEventType.DeadEnd) sentBack = true;
                s.Events.Clear();
            }
            Assert.IsTrue(sentBack, "falling off the cliff did not send the skier back");
            Assert.That(s.Sim.Pos.z, Is.LessThan(cliff.Zone.Z - 30f));
            Assert.That(s.Penalty, Is.GreaterThan(0f));
        }

        [Test]
        public void BlackSlopePitsCrashAtFullSpeedButNotSlowly()
        {
            var c = new BlackCourse();
            Pit pit = null;
            foreach (var f in c.Features) if (f is Pit p && p.Zc > 300f) { pit = p; break; }
            Assert.IsNotNull(pit);

            bool CrashesAt(float speed)
            {
                var s = new SkiSim(c, SkiParams.For(CharacterId.Erik));
                s.Teleport(pit.X, pit.Zc - 30f, 0f);
                s.Vel = new Vector3(0f, -speed * 0.45f, speed * 0.89f);
                for (int i = 0; i < 400; i++)
                {
                    s.Step(new SkiInput { Brake = speed < 15f }, SkiSim.FixedDt);
                    if (s.Crashed) return true;
                    if (s.Pos.z > pit.Zc + pit.Radius + 10f) break;
                }
                return false;
            }
            Assert.IsTrue(CrashesAt(33f), "a pit at 120 km/h should crash you");
            Assert.IsFalse(CrashesAt(9f), "a pit at walking pace should be harmless");
        }

        [Test]
        public void NoCharacterIsBestEverywhere()
        {
            var sb = new StringBuilder("Balance (bot results)\n");
            int[] wins = new int[4];
            for (int slope = 0; slope < 5; slope++)
            {
                float best = 0f; int bestWho = -1;
                sb.Append(((SlopeId)slope).ToString().PadRight(9));
                for (int who = 0; who < 4; who++)
                {
                    var s = PlayBot((SlopeId)slope, (CharacterId)who, out bool fin);
                    float v = s.ResultValue;
                    bool better = bestWho < 0 || (s.ScoreBased ? v > best : v < best);
                    if (fin && better) { best = v; bestWho = who; }
                    sb.Append($" {SkierInfo.Get((CharacterId)who).Name}={v:0.00}{(fin ? "" : "(DNF)")}");
                }
                if (bestWho >= 0) wins[bestWho]++;
                sb.Append($"  -> {(bestWho >= 0 ? SkierInfo.Get((CharacterId)bestWho).Name : "nobody")}\n");
            }
            Debug.Log(sb.ToString());
            foreach (int w in wins) Assert.That(w, Is.LessThan(5), "one character wins every slope");
        }

        [Test]
        public void LeaderboardRanksAndRemembersBest()
        {
            var store = new MemoryStore();
            var lb = new Leaderboards(store);
            var first = lb.Submit(SlopeId.Red, CharacterId.Erik, 70f);
            Assert.IsTrue(first.PersonalBest);
            Assert.IsFalse(first.TopOfBoard);
            var worse = lb.Submit(SlopeId.Red, CharacterId.Erik, 80f);
            Assert.IsFalse(worse.PersonalBest);
            var best = lb.Submit(SlopeId.Red, CharacterId.Maja, 30f);
            Assert.IsTrue(best.PersonalBest);
            Assert.IsTrue(best.TopOfBoard);
            Assert.AreEqual(1, best.Rank);
            // Points: higher is better.
            Assert.IsTrue(lb.Submit(SlopeId.Blue, CharacterId.Linn, 99999f).TopOfBoard);
            // Reloading keeps the player's results.
            var again = new Leaderboards(store);
            Assert.AreEqual(30f, again.PersonalBest(SlopeId.Red));
            Assert.AreEqual("30.00 s", Leaderboards.Format(SlopeId.Red, 30f));
        }
    }
}
