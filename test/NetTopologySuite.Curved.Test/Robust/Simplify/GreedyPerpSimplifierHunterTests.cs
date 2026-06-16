// =============================================================================
// GreedyPerpSimplifierHunterTests
// -----------------------------------------------------------------------------
// Adversarial property hunts for GreedyPerpSimplifier.Simplify.  The drop rule
// is  cross(kept,r,q)^2 <= eps^2 * distSq(kept,r)  in binary64; that rounded
// decision is the proven b64-layer spec, so the hunts target the structural
// guarantees and the geometric soundness of the rounded decision rather than
// bit-equality with an "exact" simplifier.
//
//   1. STRUCTURAL INVARIANTS (Coq-proved, hunted on adversarial inputs):
//      head + tail preserved, output is an order-preserving subsequence of the
//      input, 2 <= |output| <= |input|.  Non-vacuous: many inputs are simplified.
//
//   2. DECISION SOUNDNESS (exact BigInteger check on every decision the greedy
//      makes over large general-position polylines): a DROPPED vertex's exact
//      squared perpendicular ratio cross^2 / (eps^2 * distSq) never materially
//      exceeds 1, and a vertex that forces a SPLIT never sits materially below
//      1 -- i.e. the rounded decision agrees with the exact threshold to within
//      a tight relative slack.
//
//   3. NAN-SAFETY: NaN coordinates never throw and never silently corrupt the
//      head/tail contract.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Numerics;
using NetTopologySuite.Robust.Simplify;
using NUnit.Framework;

namespace NetTopologySuite.Test.Robust.Simplify
{
    [TestFixture]
    public class GreedyPerpSimplifierHunterTests
    {
        // ---- exact ground truth -----------------------------------------------------------------

        private static BigInteger CrossE(BPoint a, BPoint b, BPoint q)
            => (BigInteger)((long)b.X - (long)a.X) * ((long)q.Y - (long)a.Y)
             - (BigInteger)((long)q.X - (long)a.X) * ((long)b.Y - (long)a.Y);

        private static BigInteger DistSqE(BPoint a, BPoint b)
            => (BigInteger)((long)a.X - (long)b.X) * ((long)a.X - (long)b.X)
             + (BigInteger)((long)a.Y - (long)b.Y) * ((long)a.Y - (long)b.Y);

        private static (BigInteger num, BigInteger den) EpsSqRat(double eps)
        {
            long bits = BitConverter.DoubleToInt64Bits(eps);
            int re = (int)((bits >> 52) & 0x7FF); long rm = bits & 0xF_FFFF_FFFF_FFFFL;
            BigInteger m; int e;
            if (re == 0) { m = rm; e = -1074; } else { m = rm | 0x10_0000_0000_0000L; e = re - 1075; }
            BigInteger num = m * m; int ee = 2 * e;
            return ee >= 0 ? (num * BigInteger.Pow(2, ee), BigInteger.One) : (num, BigInteger.Pow(2, -ee));
        }

        /// <summary>Exact cross^2 / (eps^2 * distSq) as a double (1.0 == the drop threshold).</summary>
        private static double Ratio(BPoint a, BPoint b, BPoint q, double eps)
        {
            var c = CrossE(a, b, q); var d = DistSqE(a, b);
            var (en, ed) = EpsSqRat(eps);
            BigInteger numr = c * c * ed, denr = en * d;
            return denr.IsZero ? double.PositiveInfinity : (double)numr / (double)denr;
        }

        private static bool IsSubsequence(IReadOnlyList<BPoint> outp, IReadOnlyList<BPoint> inp)
        {
            int j = 0;
            foreach (var o in outp)
            {
                while (j < inp.Count && !(inp[j].X == o.X && inp[j].Y == o.Y)) j++;
                if (j >= inp.Count) return false;
                j++;
            }
            return true;
        }

        // ---- adversarial input generator --------------------------------------------------------

        private static List<BPoint> MakePolyline(Random rng, out double eps)
        {
            eps = new[] { 0.5, 1.0, 3.0, 17.0, 100.0, 5000.0 }[rng.Next(6)];
            int kind = rng.Next(6);
            int n = rng.Next(2, 40);
            var pts = new List<BPoint>(n);
            switch (kind)
            {
                case 0: // small general position
                    for (int i = 0; i < n; i++) pts.Add(new BPoint(rng.Next(-100, 101), rng.Next(-100, 101)));
                    break;
                case 1: // large scale general position
                    long sc = 1L << rng.Next(10, 27);
                    for (int i = 0; i < n; i++)
                        pts.Add(new BPoint(rng.Next(-1000, 1001) * sc / 1000 + rng.Next(-3, 4),
                                           rng.Next(-1000, 1001) * sc / 1000 + rng.Next(-3, 4)));
                    break;
                case 2: // exactly collinear (all on a line) -> interior must collapse
                    long x0 = rng.Next(-50, 51), y0 = rng.Next(-50, 51), dx = rng.Next(1, 9), dy = rng.Next(-8, 9);
                    for (int i = 0; i < n; i++) pts.Add(new BPoint(x0 + i * dx, y0 + i * dy));
                    break;
                case 3: // many duplicate points
                    var rep = new BPoint(rng.Next(-20, 21), rng.Next(-20, 21));
                    for (int i = 0; i < n; i++) pts.Add(rng.Next(3) == 0 ? new BPoint(rng.Next(-20, 21), rng.Next(-20, 21)) : rep);
                    break;
                case 4: // tight cluster near a point (sub-eps wiggles)
                    long bx = rng.Next(-30, 31), by = rng.Next(-30, 31);
                    for (int i = 0; i < n; i++) pts.Add(new BPoint(bx + rng.Next(-1, 2), by + rng.Next(-1, 2)));
                    break;
                default: // mixed magnitude
                    for (int i = 0; i < n; i++)
                        pts.Add(new BPoint(rng.Next(-3, 4) * (1L << rng.Next(0, 27)), rng.Next(-3, 4) * (1L << rng.Next(0, 27))));
                    break;
            }
            return pts;
        }

        // -------------------------------------------------------------------------
        [Test]
        public void StructuralInvariants_HoldOnAdversarialPolylines()
        {
            var rng = new Random(1106);
            int total = 0, simplified = 0, fails = 0;
            for (int t = 0; t < 200_000; t++)
            {
                var pts = MakePolyline(rng, out double eps);
                var outp = GreedyPerpSimplifier.Simplify(eps, pts);
                total++;
                bool ok = outp.Count >= Math.Min(2, pts.Count)
                          && outp.Count <= pts.Count
                          && outp[0].X == pts[0].X && outp[0].Y == pts[0].Y
                          && outp[^1].X == pts[^1].X && outp[^1].Y == pts[^1].Y
                          && IsSubsequence(outp, pts);
                if (!ok) fails++;
                if (outp.Count < pts.Count) simplified++;
            }

            TestContext.WriteLine($"cases={total:N0}  simplified={simplified:N0}  invariant failures={fails:N0}");
            Assert.Multiple(() =>
            {
                Assert.That(fails, Is.Zero, "Structural invariant (head/tail/subsequence/length) violated.");
                Assert.That(simplified, Is.GreaterThan(0), "No input was ever simplified -- generator too easy.");
            });
        }

        // -------------------------------------------------------------------------
        [Test]
        public void DroppedAndSplitDecisions_AgreeWithExactThreshold()
        {
            const double Slack = 1e-6; // relative margin around the eps^2 threshold
            var rng = new Random(2106);
            double maxDropRatio = 0, minSplitRatio = double.PositiveInfinity;
            long drops = 0, splits = 0;

            for (int t = 0; t < 80_000; t++)
            {
                var pts = MakePolyline(rng, out double eps);
                int n = pts.Count;
                if (n < 3) continue;

                int keptIdx = 0, qIdx = 1; double epsSq = eps * eps;
                while (qIdx < n - 1)
                {
                    BPoint kept = pts[keptIdx], q = pts[qIdx], r = pts[qIdx + 1];
                    double c = B64Ops.Cross(kept, r, q);
                    bool drop = B64Ops.Le(c * c, epsSq * B64Ops.DistSq(kept, r));
                    double ratio = Ratio(kept, r, q, eps);
                    if (!double.IsNaN(ratio) && !double.IsInfinity(ratio))
                    {
                        if (drop) { drops++; if (ratio > maxDropRatio) maxDropRatio = ratio; }
                        else { splits++; if (ratio < minSplitRatio) minSplitRatio = ratio; }
                    }
                    if (!drop) keptIdx = qIdx;
                    qIdx++;
                }
            }

            TestContext.WriteLine($"drops={drops:N0} maxDropRatio={maxDropRatio:R}   splits={splits:N0} minSplitRatio={minSplitRatio:R}");
            Assert.Multiple(() =>
            {
                Assert.That(drops, Is.GreaterThan(0));
                Assert.That(splits, Is.GreaterThan(0));
                Assert.That(maxDropRatio, Is.LessThanOrEqualTo(1.0 + Slack),
                    "A dropped vertex sat materially farther than eps from its chord.");
                Assert.That(minSplitRatio, Is.GreaterThanOrEqualTo(1.0 - Slack),
                    "A split was forced by a vertex materially closer than eps to its chord.");
            });
        }

        // -------------------------------------------------------------------------
        [Test]
        public void NaNCoordinates_DoNotThrow_AndPreserveEndpoints()
        {
            var rng = new Random(3106);
            for (int t = 0; t < 20_000; t++)
            {
                int n = rng.Next(2, 12);
                var pts = new List<BPoint>(n);
                for (int i = 0; i < n; i++)
                {
                    double x = rng.Next(3) == 0 ? double.NaN : rng.Next(-50, 51);
                    double y = rng.Next(3) == 0 ? double.NaN : rng.Next(-50, 51);
                    pts.Add(new BPoint(x, y));
                }
                List<BPoint> outp = null;
                Assert.DoesNotThrow(() => outp = GreedyPerpSimplifier.Simplify(7.0, pts), "Simplify threw on NaN input.");
                Assert.That(outp.Count, Is.InRange(Math.Min(2, n), n));
                // head/tail bit-preserved (NaN compares unequal, so compare bits)
                Assert.That(BitConverter.DoubleToInt64Bits(outp[0].X), Is.EqualTo(BitConverter.DoubleToInt64Bits(pts[0].X)));
                Assert.That(BitConverter.DoubleToInt64Bits(outp[^1].Y), Is.EqualTo(BitConverter.DoubleToInt64Bits(pts[^1].Y)));
            }
        }

        // ---- deterministic ------------------------------------------------------
        [Test]
        public void Collinear_CollapsesToEndpoints()
        {
            var pts = new List<BPoint>();
            for (int i = 0; i <= 10; i++) pts.Add(new BPoint(i, 2 * i)); // exactly on y = 2x
            var outp = GreedyPerpSimplifier.Simplify(0.5, pts);
            Assert.That(outp.Count, Is.EqualTo(2));
            Assert.That(outp[0].X, Is.EqualTo(0.0));
            Assert.That(outp[^1].X, Is.EqualTo(10.0));
        }

        [Test]
        public void Spike_AboveEps_IsKept()
        {
            // A spike of height 10 against a horizontal chord, eps = 1 -> kept.
            var pts = new List<BPoint> { new BPoint(0, 0), new BPoint(5, 10), new BPoint(10, 0) };
            var outp = GreedyPerpSimplifier.Simplify(1.0, pts);
            Assert.That(outp.Count, Is.EqualTo(3));
        }
    }
}
