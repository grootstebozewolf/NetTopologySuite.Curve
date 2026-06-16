// =============================================================================
// ArcPassesThroughHotPixelHunterTests
// -----------------------------------------------------------------------------
// ArcPassesThroughHotPixel.Evaluate is a six-way disjunction: four pixel-edge
// chord-crossings (the same sp*sq<0 in-circle test) plus two half-open
// endpoint-in-pixel tests.  On the mantissa-safe regime (integer pixel centre,
// even integer side -> integer corners) every branch carries exact signs, so
// Evaluate must coincide exactly with the disjunction computed from the exact
// BigInteger in-circle sign and exact half-open membership.  We hunt that
// agreement and pin the endpoint-in-pixel => Evaluate implication.
// =============================================================================

using System;
using System.Numerics;
using NetTopologySuite.Robust.Arc;
using NetTopologySuite.Robust.Simplify;
using NUnit.Framework;

namespace NetTopologySuite.Test.Robust.Arc
{
    [TestFixture]
    public class ArcPassesThroughHotPixelHunterTests
    {
        private static int ExactSign(long ax_, long ay_, long bx_, long by_, long cx_, long cy_, long px, long py)
        {
            BigInteger ax = ax_ - px, ay = ay_ - py, bx = bx_ - px, by = by_ - py, cx = cx_ - px, cy = cy_ - py;
            BigInteger na = ax * ax + ay * ay, nb = bx * bx + by * by, nc = cx * cx + cy * cy;
            BigInteger det = ax * (by * nc - cy * nb) - ay * (bx * nc - cx * nb) + na * (bx * cy - cx * by);
            return det.Sign;
        }

        private static BPoint BP(long x, long y) => new BPoint(x, y);

        private static bool ExactCross(long ax, long ay, long bx, long by, long cx, long cy,
                                       long px, long py, long qx, long qy)
            => ExactSign(ax, ay, bx, by, cx, cy, px, py) * ExactSign(ax, ay, bx, by, cx, cy, qx, qy) < 0;

        private static bool ExactInHalfOpen(long px, long py, long cx, long cy, long r)
            => px >= cx - r && px < cx + r && py >= cy - r && py < cy + r;

        // Exact replica of Evaluate over integer corners (centre integer, even side).
        private static bool ExactEval(long ax, long ay, long bx, long by, long cx, long cy,
                                      long centerX, long centerY, long side)
        {
            long r = side / 2;
            long blx = centerX - r, bly = centerY - r, brx = centerX + r, bry = centerY - r;
            long trx = centerX + r, tryy = centerY + r, tlx = centerX - r, tly = centerY + r;
            return ExactCross(ax, ay, bx, by, cx, cy, blx, bly, brx, bry)
                || ExactCross(ax, ay, bx, by, cx, cy, brx, bry, trx, tryy)
                || ExactCross(ax, ay, bx, by, cx, cy, trx, tryy, tlx, tly)
                || ExactCross(ax, ay, bx, by, cx, cy, tlx, tly, blx, bly)
                || ExactInHalfOpen(ax, ay, centerX, centerY, r)
                || ExactInHalfOpen(cx, cy, centerX, centerY, r);
        }

        [Test]
        public void Evaluate_MatchesExactDisjunction_OnSmallRegime()
        {
            var rng = new Random(1106);
            int mismatches = 0, trueCount = 0, n = 0;
            (long, long, long, long, long, long, long, long, long) firstBad = default;

            for (int i = 0; i < 300_000; i++)
            {
                long ax = rng.Next(-40, 41), ay = rng.Next(-40, 41); // arcStart
                long bx = rng.Next(-40, 41), by = rng.Next(-40, 41); // arcMid
                long cx = rng.Next(-40, 41), cy = rng.Next(-40, 41); // arcEnd
                long centerX = rng.Next(-40, 41), centerY = rng.Next(-40, 41);
                long side = 2L * rng.Next(1, 20); // even side -> integer corners and radius

                bool eval = ArcPassesThroughHotPixel.Evaluate(
                    BP(ax, ay), BP(bx, by), BP(cx, cy), BP(centerX, centerY), side);
                bool exact = ExactEval(ax, ay, bx, by, cx, cy, centerX, centerY, side);

                if (eval != exact) { if (mismatches == 0) firstBad = (ax, ay, bx, by, cx, cy, centerX, centerY, side); mismatches++; }
                if (eval) trueCount++;
                n++;
            }

            TestContext.WriteLine($"cases={n:N0}  Evaluate==TRUE={trueCount:N0}  mismatches-vs-exact={mismatches:N0}");
            Assert.Multiple(() =>
            {
                Assert.That(mismatches, Is.Zero,
                    $"Evaluate diverged from the exact disjunction on a mantissa-safe config: {firstBad}");
                Assert.That(trueCount, Is.GreaterThan(0), "No pass-through case sampled -- generator is vacuous.");
            });
        }

        [Test]
        public void EndpointStrictlyInsidePixel_ImpliesEvaluate()
        {
            var rng = new Random(2106);
            int inside = 0, violations = 0;
            for (int i = 0; i < 300_000; i++)
            {
                long centerX = rng.Next(-40, 41), centerY = rng.Next(-40, 41);
                long side = 2L * rng.Next(2, 20);
                long r = side / 2;
                // arcStart strictly inside the half-open pixel interior.
                long ax = centerX + rng.Next((int)(-r + 1), (int)r), ay = centerY + rng.Next((int)(-r + 1), (int)r);
                long bx = rng.Next(-80, 81), by = rng.Next(-80, 81);
                long cx = rng.Next(-80, 81), cy = rng.Next(-80, 81);

                if (ExactInHalfOpen(ax, ay, centerX, centerY, r))
                {
                    inside++;
                    if (!ArcPassesThroughHotPixel.Evaluate(BP(ax, ay), BP(bx, by), BP(cx, cy), BP(centerX, centerY), side))
                        violations++;
                }
            }
            Assert.Multiple(() =>
            {
                Assert.That(violations, Is.Zero, "An arc endpoint strictly inside the pixel did not yield Evaluate=true.");
                Assert.That(inside, Is.GreaterThan(0), "No inside-endpoint cases sampled.");
            });
        }

        // ---- deterministic ------------------------------------------------------
        [Test]
        public void ArcEndpointInsidePixel_IsTrue()
        {
            Assert.That(ArcPassesThroughHotPixel.Evaluate(
                BP(0, 0), BP(50, 5), BP(60, 60), BP(0, 0), 4.0), Is.True);
        }

        [Test]
        public void ArcCircleFarFromPixel_IsFalse()
        {
            // Tiny arc circle near origin; pixel far away -> no edge crossing, no endpoint inside.
            Assert.That(ArcPassesThroughHotPixel.Evaluate(
                BP(1, 0), BP(0, 1), BP(-1, 0), BP(1000, 1000), 2.0), Is.False);
        }
    }
}
