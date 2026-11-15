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
            Assert.That(s.Gates.Count, Is.GreaterThan(30));
            Assert.That(s.GatesMissed, Is.LessThanOrEqualTo(5), "gates missed");
            // The rhythm really varies: both tight flushes and long open turns are in the course.
            float minGap = float.MaxValue, maxGap = 0f;
            for (int i = 1; i < s.Gates.Count; i++) { float g = s.Gates[i].Z - s.Gates[i - 1].Z; minGap = Mathf.Min(minGap, g); maxGap = Mathf.Max(maxGap, g); }
            Assert.That(minGap, Is.LessThan(15f));
            Assert.That(maxGap, Is.GreaterThan(26f));
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
            Assert.That(trav, Is.GreaterThan(12));
            Assert.That(harness, Is.GreaterThan(6));
            Assert.That(school, Is.GreaterThan(28));
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
            Assert.AreEqual(3, c.Solutions.Count, "three ways down");
            Assert.That(c.DeadEnds.Count, Is.GreaterThan(c.Solutions.Count));
            // Every way-point of a valid route is on open ground.
            foreach (var route in c.Solutions)
                foreach (var p in route)
                    if (p.y < c.FinishZ) Assert.That(c.Clear(p.x, p.y), Is.GreaterThan(3f), $"route point {p} is in the forest");
            // No dead end may sit on a valid route.
            foreach (var route in c.Solutions)
                for (int i = 0; i < route.Count - 1; i++)
                    for (float t = 0f; t <= 1f; t += 0.1f)
                    {
                        Vector2 p = Vector2.Lerp(route[i], route[i + 1], t);
                        Assert.IsFalse(c.InDeadEnd(p.x, p.y, out _), $"a dead end covers the valid route at {p}");
                    }
            Debug.Log($"Off-piste: {c.Solutions.Count} routes, {c.DeadEnds.Count} dead ends, {c.Obstacles.Count} obstacles, {c.Segments.Count} glade segments");
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void EveryOffPisteRouteReachesTheBottom(int route)
        {
            var s = new RunSession(SlopeId.OffPiste, CharacterId.Erik);
            bool finished = Bot.Play(s, new Bot(s, route));
            Debug.Log($"Off-piste route {route} ({OffPisteCourse.RouteNames[route]}): finished={finished} time={s.FinalTime:0.0} crashes={s.Sim.CrashCount}");
            Assert.IsTrue(finished, $"route {route} stuck at z={s.Sim.Pos.z:0}");
        }

        [Test, Timeout(600000)]   // twelve full bot runs through the forest
        public void OffPisteRoutesAreCloseAndSuitDifferentSkiers()
        {
            // No route should be hopeless, and no route should be the best for everyone by a mile.
            var sb = new StringBuilder("Off-piste routes x skiers (bot times)\n");
            float slowest = 0f, fastest = float.MaxValue;
            for (int route = 0; route < 3; route++)
            {
                sb.Append(OffPisteCourse.RouteNames[route].PadRight(13));
                for (int who = 0; who < 4; who++)
                {
                    var s = new RunSession(SlopeId.OffPiste, (CharacterId)who);
                    bool fin = Bot.Play(s, new Bot(s, route));
                    sb.Append($" {SkierInfo.Get((CharacterId)who).Name}={s.FinalTime:0.0}{(fin ? "" : "(DNF)")}");
                    if (fin && who == 2) { slowest = Mathf.Max(slowest, s.FinalTime); fastest = Mathf.Min(fastest, s.FinalTime); }
                }
                sb.Append("\n");
            }
            Debug.Log(sb.ToString());
            Assert.That(slowest / fastest, Is.LessThan(1.35f), "one route is far slower than another");
        }

        [Test]
        public void DeadEndRespawnIsAtAForkOnAValidRoute()
        {
            var c = new OffPisteCourse();
            var s = new RunSession(SlopeId.OffPiste, CharacterId.Erik, c);
            // Ski the direct route for a while so a junction is passed, then ask to be put back.
            var bot = new Bot(s, 0);
            for (int i = 0; i < 120 * 25; i++) { s.Step(bot.Decide(), SkiSim.FixedDt); s.Events.Clear(); }
            Assert.That(s.Sim.Pos.z, Is.GreaterThan(150f));
            s.Respawn();
            float x = s.Sim.Pos.x, z = s.Sim.Pos.z;
            Assert.That(c.Clear(x, z), Is.GreaterThan(2f), "respawn point is not in open ground");
            bool nearJunction = false;
            foreach (var cp in c.Checkpoints) if (Mathf.Abs(cp.x - x) < 1f && Mathf.Abs(cp.y - 7f - z) < 1f) nearJunction = true;
            Assert.IsTrue(nearJunction, "respawn is not just above a junction of a valid route");
        }

        [Test]
        public void RocksAreAsBigAsTheyLook()
        {
            var c = new OffPisteCourse();
            int rocks = 0;
            foreach (var o in c.Obstacles)
            {
                if (o.Kind != ObstacleKind.Rock) continue;
                rocks++;
                Assert.That(o.Radius, Is.GreaterThanOrEqualTo(o.Scale), "collision circle smaller than the boulder");
                // Skiing straight into it at speed, crouched, must crash.
                var sim = new SkiSim(c, SkiParams.For(CharacterId.Erik));
                sim.Teleport(o.X, o.Z - 6f, 0f);
                sim.Vel = new Vector3(0f, -6f, 24f);
                bool crashed = false; float airAtRock = 0f;
                for (int i = 0; i < 80 && !crashed; i++)
                {
                    sim.Step(new SkiInput { Tuck = true }, SkiSim.FixedDt);
                    crashed = sim.Crashed;
                    if (Mathf.Abs(sim.Pos.z - o.Z) < o.Radius) airAtRock = Mathf.Max(airAtRock, sim.AirHeight);
                }
                // Either you hit it, or you were properly in the air above it (off a ledge, say).
                Assert.IsTrue(crashed || airAtRock > o.Height, $"skied through a rock at ({o.X:0},{o.Z:0}), air {airAtRock:0.00} m");
                if (rocks >= 12) break;
            }
            Assert.That(rocks, Is.GreaterThan(5));
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
        public void ChugGameRewardsTimingAndPunishesSpills()
        {
            var g = new ChugGame(3);
            g.Start();
            // Press only inside the zone: finishes without spills.
            for (int i = 0; i < 120 * 60 && !g.Finished; i++)
            {
                g.Step(SkiSim.FixedDt);
                if (g.Marker > g.ZoneMin + 0.02f && g.Marker < g.ZoneMax - 0.02f) Assert.IsTrue(g.Press());
            }
            Assert.IsTrue(g.Finished);
            Assert.AreEqual(0, g.Spills);
            Assert.AreEqual(ChugGame.GulpsToFinish, g.Gulps);
            Assert.That(g.Time, Is.InRange(3f, 25f));

            var bad = new ChugGame(3);
            bad.Start();
            bad.Step(0.01f);
            float before = bad.Time;
            // The marker starts at the left edge, outside any zone.
            Assert.IsFalse(bad.Press());
            Assert.AreEqual(1, bad.Spills);
            Assert.That(bad.Time - before, Is.EqualTo(ChugGame.SpillPenalty).Within(1e-4f));
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
