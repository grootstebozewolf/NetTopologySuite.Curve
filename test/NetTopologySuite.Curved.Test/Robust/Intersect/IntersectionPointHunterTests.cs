// =============================================================================
// IntersectionPointHunterTests
// -----------------------------------------------------------------------------
// Forward-error hunt for RobustLineIntersector.IntersectionPoint.  The method
// returns the *rounded* binary64 approximation of the true rational
// intersection of two crossing segments; a forward-error soundness theorem
// against the true rational is deferred on the Coq side (see the method's
// remarks).  This hunt quantifies that error empirically:
//
//   * compute the exact rational intersection (Cramer's rule in BigInteger),
//   * measure |returned - exact| relative to the input coordinate scale, in
//     units of eps = 2^-52, and
//   * assert it stays within a small bound on every proper crossing, including
//     adversarial near-grazing crossings (small denominator, ill-conditioned)
//     at mantissa scale.
//
// Also pins the consistency contract: IntersectionPoint returns non-null
// exactly when SignFiltered reports Point, and a handful of exact crossings.
// =============================================================================

using System;
using System.Numerics;
using NetTopologySuite.Robust.Intersect;
using NetTopologySuite.Robust.Simplify;
using NUnit.Framework;

namespace NetTopologySuite.Test.Robust.Intersect
{
    [TestFixture]
    public class IntersectionPointHunterTests
    {
        private const double Eps = 2.220446049250313e-16; // 2^-52

        // Forward-error budget, in units of eps relative to the input scale.
        // Observed maxima: ~1 (well-conditioned) and ~5.3 (near-grazing); the
        // bound leaves headroom while still tripping on a formula regression
        // (e.g. losing a rounding stage would blow far past this).
        private const double MaxRelEps = 64.0;

        private static BPoint P(long x, long y) => new BPoint(x, y);

        private static BigInteger Orient(long ax, long ay, long bx, long by, long cx, long cy)
            => (BigInteger)(bx - ax) * (cy - ay) - (BigInteger)(cx - ax) * (by - ay);

        private static (BigInteger m, int e) Decompose(double x)
        {
            long bits = BitConverter.DoubleToInt64Bits(x);
            int rawExp = (int)((bits >> 52) & 0x7FF);
            long rawMant = bits & 0xF_FFFF_FFFF_FFFFL;
            BigInteger m; int e;
            if (rawExp == 0) { m = rawMant; e = -1074; }
            else { m = rawMant | 0x10_0000_0000_0000L; e = rawExp - 1075; }
            if (bits < 0) m = -m;
            return (m, e);
        }

        /// <summary>Exact |d - N/D| as a double (D != 0).</summary>
        private static double AbsError(double d, BigInteger N, BigInteger D)
        {
            if (D.Sign < 0) { N = -N; D = -D; }
            var (m, e) = Decompose(d);
            BigInteger rn, rd;
            if (e >= 0) { var p = BigInteger.Pow(2, e); rn = m * p * D - N; rd = D; }
            else { var p = BigInteger.Pow(2, -e); rn = m * D - N * p; rd = D * p; }
            return Math.Abs((double)rn / (double)rd);
        }

        // Worst-coordinate forward error of the returned point, in units of eps
        // relative to the input scale S; returns -1 when there is no crossing.
        private static double RelEps(long p0x, long p0y, long p1x, long p1y,
                                     long q0x, long q0y, long q1x, long q1y, double scale)
        {
            var P0 = P(p0x, p0y); var P1 = P(p1x, p1y); var Q0 = P(q0x, q0y); var Q1 = P(q1x, q1y);
            if (RobustLineIntersector.SignFiltered(P0, P1, Q0, Q1) != IntersectSign.Point) return -1;
            var X = RobustLineIntersector.IntersectionPoint(P0, P1, Q0, Q1).Value;

            BigInteger qp0 = Orient(q0x, q0y, q1x, q1y, p0x, p0y);
            BigInteger qp1 = Orient(q0x, q0y, q1x, q1y, p1x, p1y);
            BigInteger den = qp0 - qp1;
            BigInteger nx = p0x * den + qp0 * (p1x - p0x);
            BigInteger ny = p0y * den + qp0 * (p1y - p0y);

            double err = Math.Max(AbsError(X.X, nx, den), AbsError(X.Y, ny, den));
            return err / (Math.Max(scale, 1.0) * Eps);
        }

        // -------------------------------------------------------------------------
        [Test]
        public void IntersectionPoint_ForwardError_BoundedOnRandomCrossings()
        {
            var rng = new Random(1106);
            double max = 0; int n = 0;
            (long, long, long, long, long, long, long, long) worst = default;

            for (int i = 0; i < 400_000 && n < 80_000; i++)
            {
                long p0x = rng.Next(-40, 41), p0y = rng.Next(-40, 41), p1x = rng.Next(-40, 41), p1y = rng.Next(-40, 41);
                long q0x = rng.Next(-40, 41), q0y = rng.Next(-40, 41), q1x = rng.Next(-40, 41), q1y = rng.Next(-40, 41);
                double r = RelEps(p0x, p0y, p1x, p1y, q0x, q0y, q1x, q1y, 40);
                if (r < 0) continue;
                if (r > max) { max = r; worst = (p0x, p0y, p1x, p1y, q0x, q0y, q1x, q1y); }
                n++;
            }

            TestContext.WriteLine($"random proper crossings={n:N0}  max forward error={max:F3} eps  worst={worst}");
            Assert.That(n, Is.GreaterThan(1000), "Too few proper crossings sampled.");
            Assert.That(max, Is.LessThanOrEqualTo(MaxRelEps),
                $"IntersectionPoint forward error {max:F3} eps exceeded budget on {worst}");
        }

        // -------------------------------------------------------------------------
        [Test]
        public void IntersectionPoint_ForwardError_BoundedOnNearGrazingCrossings()
        {
            var rng = new Random(2106);
            double max = 0; int n = 0;
            (long, long, long, long, long, long, long, long) worst = default;

            for (int i = 0; i < 800_000 && n < 80_000; i++)
            {
                long sc = 1L << rng.Next(12, 26);
                long p0x = rng.Next(-30, 31) * sc, p0y = rng.Next(-30, 31) * sc;
                long p1x = rng.Next(-30, 31) * sc, p1y = rng.Next(-30, 31) * sc;
                // Q endpoints near the same lattice but perturbed by a few units,
                // so a crossing of P is shallow (small orientation determinants).
                long q0x = rng.Next(-30, 31) * sc + rng.Next(-3, 4), q0y = rng.Next(-30, 31) * sc + rng.Next(-3, 4);
                long q1x = rng.Next(-30, 31) * sc + rng.Next(-3, 4), q1y = rng.Next(-30, 31) * sc + rng.Next(-3, 4);
                double r = RelEps(p0x, p0y, p1x, p1y, q0x, q0y, q1x, q1y, (double)sc * 30);
                if (r < 0) continue;
                if (r > max) { max = r; worst = (p0x, p0y, p1x, p1y, q0x, q0y, q1x, q1y); }
                n++;
            }

            TestContext.WriteLine($"near-grazing proper crossings={n:N0}  max forward error={max:F3} eps  worst={worst}");
            Assert.That(n, Is.GreaterThan(1000), "Too few near-grazing proper crossings sampled.");
            Assert.That(max, Is.LessThanOrEqualTo(MaxRelEps),
                $"IntersectionPoint forward error {max:F3} eps exceeded budget on {worst}");
        }

        // -------------------------------------------------------------------------
        // Consistency contract: non-null exactly when the sign is Point.
        // -------------------------------------------------------------------------
        [Test]
        public void IntersectionPoint_NonNull_IffSignFilteredPoint()
        {
            var rng = new Random(3106);
            int checkedCount = 0;
            for (int i = 0; i < 200_000 && checkedCount < 50_000; i++)
            {
                var P0 = P(rng.Next(-40, 41), rng.Next(-40, 41));
                var P1 = P(rng.Next(-40, 41), rng.Next(-40, 41));
                var Q0 = P(rng.Next(-40, 41), rng.Next(-40, 41));
                var Q1 = P(rng.Next(-40, 41), rng.Next(-40, 41));
                bool isPoint = RobustLineIntersector.SignFiltered(P0, P1, Q0, Q1) == IntersectSign.Point;
                bool hasPt = RobustLineIntersector.IntersectionPoint(P0, P1, Q0, Q1).HasValue;
                Assert.That(hasPt, Is.EqualTo(isPoint),
                    $"IntersectionPoint nullability disagreed with SignFiltered==Point for {P0.X},{P0.Y} {P1.X},{P1.Y} {Q0.X},{Q0.Y} {Q1.X},{Q1.Y}");
                checkedCount++;
            }
            Assert.That(checkedCount, Is.GreaterThan(0));
        }

        // ---- deterministic exact crossings --------------------------------------
        [Test]
        public void IntersectionPoint_DiagonalsCrossAtCenter()
        {
            var x = RobustLineIntersector.IntersectionPoint(P(0, 0), P(10, 10), P(0, 10), P(10, 0)).Value;
            Assert.Multiple(() =>
            {
                Assert.That(x.X, Is.EqualTo(5.0));
                Assert.That(x.Y, Is.EqualTo(5.0));
            });
        }

        [Test]
        public void IntersectionPoint_PerpendicularCrossAtAxis()
        {
            var x = RobustLineIntersector.IntersectionPoint(P(-10, 0), P(10, 0), P(0, -7), P(0, 7)).Value;
            Assert.Multiple(() =>
            {
                Assert.That(x.X, Is.EqualTo(0.0));
                Assert.That(x.Y, Is.EqualTo(0.0));
            });
        }

        [Test]
        public void IntersectionPoint_NullWhenParallelDisjoint()
        {
            Assert.That(RobustLineIntersector.IntersectionPoint(P(0, 0), P(1, 0), P(0, 1), P(1, 1)), Is.Null);
        }
    }
}
