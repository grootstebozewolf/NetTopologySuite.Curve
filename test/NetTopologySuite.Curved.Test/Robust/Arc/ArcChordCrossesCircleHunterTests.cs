// =============================================================================
// ArcChordCrossesCircleHunterTests
// -----------------------------------------------------------------------------
// ArcChordCrossesCircle.Evaluate is the sufficient condition sp*sq < 0 on two
// in-circle determinants; TRUE implies the chord crosses the arc's circle
// (Coq IVT theorem).  On the mantissa-safe small-coordinate regime the two
// determinants carry exact signs, so Evaluate must coincide exactly with the
// exact "endpoints strictly opposite the circle" predicate.  We hunt that
// agreement (a differential against the BigInteger in-circle sign) and pin the
// soundness direction (Evaluate TRUE ==> endpoints exactly straddle the circle).
// =============================================================================

using System;
using System.Numerics;
using NetTopologySuite.Robust.Arc;
using NetTopologySuite.Robust.Simplify;
using NUnit.Framework;

namespace NetTopologySuite.Test.Robust.Arc
{
    [TestFixture]
    public class ArcChordCrossesCircleHunterTests
    {
        private static int ExactSign(long ax_, long ay_, long bx_, long by_, long cx_, long cy_, long px, long py)
        {
            BigInteger ax = ax_ - px, ay = ay_ - py, bx = bx_ - px, by = by_ - py, cx = cx_ - px, cy = cy_ - py;
            BigInteger na = ax * ax + ay * ay, nb = bx * bx + by * by, nc = cx * cx + cy * cy;
            BigInteger det = ax * (by * nc - cy * nb) - ay * (bx * nc - cx * nb) + na * (bx * cy - cx * by);
            return det.Sign;
        }

        private static BPoint BP(long x, long y) => new BPoint(x, y);

        [Test]
        public void Evaluate_MatchesExactStraddle_OnSmallRegime()
        {
            var rng = new Random(1106);
            int mismatches = 0, trueCount = 0, n = 0;
            (long, long, long, long, long, long, long, long) firstBad = default;

            for (int i = 0; i < 300_000; i++)
            {
                long ax = rng.Next(-60, 61), ay = rng.Next(-60, 61), bx = rng.Next(-60, 61), by = rng.Next(-60, 61);
                long cx = rng.Next(-60, 61), cy = rng.Next(-60, 61);
                long px = rng.Next(-60, 61), py = rng.Next(-60, 61), qx = rng.Next(-60, 61), qy = rng.Next(-60, 61);

                bool eval = ArcChordCrossesCircle.Evaluate(BP(ax, ay), BP(bx, by), BP(cx, cy), BP(px, py), BP(qx, qy));
                int sp = ExactSign(ax, ay, bx, by, cx, cy, px, py);
                int sq = ExactSign(ax, ay, bx, by, cx, cy, qx, qy);
                bool exactStraddle = sp * sq < 0;

                if (eval != exactStraddle) { if (mismatches == 0) firstBad = (ax, ay, bx, by, cx, cy, px, py); mismatches++; }
                if (eval) trueCount++;
                n++;
            }

            TestContext.WriteLine($"cases={n:N0}  Evaluate==TRUE={trueCount:N0}  mismatches-vs-exact={mismatches:N0}");
            Assert.Multiple(() =>
            {
                Assert.That(mismatches, Is.Zero,
                    $"Evaluate diverged from the exact straddle predicate on a mantissa-safe config: {firstBad}");
                Assert.That(trueCount, Is.GreaterThan(0), "No crossing case sampled -- generator is vacuous.");
            });
        }

        // ---- deterministic ------------------------------------------------------
        [Test]
        public void ChordStraddlingCircle_IsTrue()
        {
            // Arc circle is radius 5 about origin; chord from inside (origin) to far outside.
            Assert.That(ArcChordCrossesCircle.Evaluate(
                BP(5, 0), BP(0, 5), BP(-5, 0), BP(0, 0), BP(50, 50)), Is.True);
        }

        [Test]
        public void ChordWhollyInside_IsFalse()
        {
            Assert.That(ArcChordCrossesCircle.Evaluate(
                BP(5, 0), BP(0, 5), BP(-5, 0), BP(0, 0), BP(1, 1)), Is.False);
        }

        [Test]
        public void ChordEndpointOnCircle_IsFalse_SufficientConditionOnly()
        {
            // sq = 0 (on the circle) -> sp*sq = 0, not < 0.
            Assert.That(ArcChordCrossesCircle.Evaluate(
                BP(5, 0), BP(0, 5), BP(-5, 0), BP(0, 0), BP(0, -5)), Is.False);
        }
    }
}
