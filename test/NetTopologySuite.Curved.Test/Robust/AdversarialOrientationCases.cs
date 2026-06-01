using System;
using System.Collections.Generic;
using System.Numerics;
using NetTopologySuite.Algorithm;

namespace NetTopologySuite.Test.Robust
{
    /// <summary>
    /// One orientation test case: three points plus the <i>exact</i> sign of their orientation
    /// determinant (known by construction). All coordinates are integer-valued and exactly
    /// representable as <c>double</c>.
    /// </summary>
    internal readonly record struct OrientCase(
        double P1x, double P1y, double P2x, double P2y, double Qx, double Qy, int ExactSign);

    /// <summary>
    /// Shared generators and reference predicates for the DD orientation hunts.
    /// <para/>
    /// The hard cases are built by a Diophantine construction rather than by perturbing a point
    /// near a long segment: a point merely close to a long segment has determinant base*height,
    /// which stays large. Instead we solve <c>dx*ey - dy*ex = target</c> for a small integer
    /// <c>target</c> (with <c>gcd(dx,dy)=1</c>), then slide q along the direction so the two
    /// products <c>dx*ey</c> and <c>dy*ex</c> individually overflow the 53-bit double mantissa.
    /// The true determinant is exactly <c>target</c>, but the naive double subtraction loses it.
    /// </summary>
    internal static class AdversarialOrientationCases
    {
        /// <summary>Extended Euclid: g = gcd(a,b) with a*x + b*y = g.</summary>
        internal static long ExtGcd(long a, long b, out long x, out long y)
        {
            if (b == 0) { x = 1; y = 0; return a; }
            long g = ExtGcd(b, a % b, out long x1, out long y1);
            x = y1;
            y = x1 - (a / b) * y1;
            return g;
        }

        /// <summary>
        /// Near-collinear cases with exact determinant in {-2..2}, formed from products in
        /// roughly 2^54..2^58 (well past the 53-bit mantissa).
        /// </summary>
        internal static IEnumerable<OrientCase> NearCollinear(int count, int seed)
            => Build(count, seed, minTarget: -2, maxTarget: 2);

        /// <summary>Minimal-determinant cases: exact determinant in {-1, 0, +1}.</summary>
        internal static IEnumerable<OrientCase> MinimalDeterminant(int count, int seed)
            => Build(count, seed, minTarget: -1, maxTarget: 1);

        private static IEnumerable<OrientCase> Build(int count, int seed, int minTarget, int maxTarget)
        {
            var rng = new Random(seed);
            int built = 0;
            while (built < count)
            {
                long dx = rng.NextInt64(1L << 25, 1L << 28);
                long dy = rng.NextInt64(1L << 25, 1L << 28);
                if (rng.Next(2) == 0) dy = -dy;
                if (ExtGcd(dx, dy, out long s, out long t) != 1) continue; // need gcd 1 to hit |det|=1

                long p1x = rng.NextInt64(-(1L << 20), 1L << 20);
                long p1y = rng.NextInt64(-(1L << 20), 1L << 20);
                long p2x = p1x + dx, p2y = p1y + dy;

                int target = rng.Next(minTarget, maxTarget + 1);
                long ey = s * target;
                long ex = -t * target;
                long k = rng.NextInt64(2, 16) * (rng.Next(2) == 0 ? 1 : -1);
                ey += k * dy;
                ex += k * dx;

                long qx = p2x + ex, qy = p2y + ey;
                if (Math.Abs(qx) >= (1L << 52) || Math.Abs(qy) >= (1L << 52)) continue; // exact as double

                yield return new OrientCase(p1x, p1y, p2x, p2y, qx, qy, Math.Sign(target));
                built++;
            }
        }

        // ---- reference predicates -------------------------------------------------------------

        /// <summary>Exact sign of the orientation determinant over the reals, via BigInteger.</summary>
        internal static int ExactOrientation(
            double p1x, double p1y, double p2x, double p2y, double qx, double qy)
        {
            Span<double> ords = stackalloc double[6] { p1x, p1y, p2x, p2y, qx, qy };
            var mant = new BigInteger[6];
            var exp = new int[6];
            int minExp = int.MaxValue;
            for (int i = 0; i < 6; i++)
            {
                (mant[i], exp[i]) = Decompose(ords[i]);
                if (exp[i] < minExp) minExp = exp[i];
            }
            BigInteger N(int i) => mant[i] << (exp[i] - minExp);
            BigInteger dx1 = N(2) - N(0), dy1 = N(3) - N(1);
            BigInteger dx2 = N(4) - N(2), dy2 = N(5) - N(3);
            return (dx1 * dy2 - dy1 * dx2).Sign;
        }

        private static (BigInteger mant, int exp) Decompose(double d)
        {
            long bits = BitConverter.DoubleToInt64Bits(d);
            int rawExp = (int)((bits >> 52) & 0x7FF);
            long rawMant = bits & 0xF_FFFF_FFFF_FFFFL;
            BigInteger mant;
            int exp;
            if (rawExp == 0) { mant = rawMant; exp = -1074; }
            else { mant = rawMant | 0x10_0000_0000_0000L; exp = rawExp - 1075; }
            if (bits < 0) mant = -mant;
            return (mant, exp);
        }

        /// <summary>The fragile predicate DD replaced: a plain <c>double</c> cross product.</summary>
        internal static int NaiveOrientation(
            double p1x, double p1y, double p2x, double p2y, double qx, double qy)
        {
            double dx1 = p2x - p1x, dy1 = p2y - p1y;
            double dx2 = qx - p2x, dy2 = qy - p2y;
            return Math.Sign(dx1 * dy2 - dy1 * dx2);
        }

        /// <summary>The double-double predicate under test (NTS mirror of JTS CGAlgorithmsDD).</summary>
        internal static int DdOrientation(
            double p1x, double p1y, double p2x, double p2y, double qx, double qy)
            => CGAlgorithmsDD.OrientationIndex(p1x, p1y, p2x, p2y, qx, qy);
    }
}
