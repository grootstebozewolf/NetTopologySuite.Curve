// =============================================================================
// AdversarialIntersectCases
// -----------------------------------------------------------------------------
// Adversarial generators and an exact BigInteger ground-truth classifier for
// the segment-pair intersection hunts (RobustLineIntersectorHunterTests).
//
// Segment 1 = A..B, segment 2 = C..D.  Two generators:
//
//   RandomSmall  -- small integer endpoints; the easy regime where the naive
//                   and filtered predicates must both agree with exact truth.
//
//   Adversarial  -- A..B is a Diophantine direction (gcd 1) and C is placed
//                   near the supporting line A..B so that orient(A,B,C) is in
//                   the mantissa-overflow band, while D is a perpendicular
//                   point well off the line.  This drives the underlying
//                   Stage A orientation filter into Uncertain / exact-zero
//                   cancellation, which is exactly the regime where the
//                   intersection predicate must NOT commit a wrong None/Point.
//
// All endpoint coordinates are integer-valued and exactly representable as
// double, so the exact classification reduces to the signs of four integer
// orientation determinants (computed in BigInteger -> never rounds).
// =============================================================================

using System;
using System.Collections.Generic;
using System.Numerics;
using NetTopologySuite.Robust.Intersect;
using NetTopologySuite.Robust.Simplify; // BPoint

namespace NetTopologySuite.Test.Robust.Intersect
{
    internal readonly record struct IntersectCase(
        long Ax, long Ay, long Bx, long By, long Cx, long Cy, long Dx, long Dy)
    {
        public BPoint A => new BPoint(Ax, Ay);
        public BPoint B => new BPoint(Bx, By);
        public BPoint C => new BPoint(Cx, Cy);
        public BPoint D => new BPoint(Dx, Dy);
    }

    internal static class AdversarialIntersectCases
    {
        private static long ExtGcd(long a, long b, out long x, out long y)
        {
            if (b == 0) { x = 1; y = 0; return a; }
            long g = ExtGcd(b, a % b, out long x1, out long y1);
            x = y1;
            y = x1 - (a / b) * y1;
            return g;
        }

        /// <summary>Easy regime: small integer segment pairs.</summary>
        internal static IEnumerable<IntersectCase> RandomSmall(int count, int seed)
        {
            var rng = new Random(seed);
            const int K = 40;
            long R() => rng.Next(-K, K + 1);
            for (int i = 0; i < count; i++)
                yield return new IntersectCase(R(), R(), R(), R(), R(), R(), R(), R());
        }

        /// <summary>
        /// Hard regime: segment A..B along a Diophantine direction (gcd 1).
        /// BOTH endpoints C and D of the second segment are placed near the
        /// supporting line A..B with prescribed tiny exact orientations
        /// (orient(A,B,C) == tC, orient(A,B,D) == tD) using Bezout coefficients,
        /// then slid far along the line so the orientation products overflow the
        /// 53-bit mantissa.  Picking opposite-sign targets makes C..D a
        /// near-grazing crossing of A..B; this yields a mix of None / Point /
        /// Collinear at the scale where the Stage A orientation filter is forced
        /// into Uncertain or exact-zero cancellation.
        /// </summary>
        internal static IEnumerable<IntersectCase> Adversarial(int count, int seed)
        {
            var rng = new Random(seed);
            int built = 0;
            while (built < count)
            {
                long dx = rng.NextInt64(1L << 25, 1L << 28);
                long dy = rng.NextInt64(1L << 25, 1L << 28);
                if (rng.Next(2) == 0) dy = -dy;
                if (ExtGcd(dx, dy, out long s, out long t) != 1) continue; // dx*s + dy*t = 1

                long ax = rng.NextInt64(-(1L << 20), 1L << 20);
                long ay = rng.NextInt64(-(1L << 20), 1L << 20);
                long bx = ax + dx, by = ay + dy;

                // Offset from B giving orient(A,B, B+offset) == target exactly,
                // pushed out by k*d so coordinates reach the overflow band.
                bool TryPoint(int target, out long px, out long py)
                {
                    long k = rng.NextInt64(2, 24) * (rng.Next(2) == 0 ? 1 : -1);
                    long ex = -t * target + k * dx;
                    long ey = s * target + k * dy;
                    px = bx + ex; py = by + ey;
                    return Math.Abs(px) < (1L << 52) && Math.Abs(py) < (1L << 52);
                }

                int tC = rng.Next(-2, 3);
                int tD = rng.Next(-2, 3);
                if (!TryPoint(tC, out long cx, out long cy)) continue;
                if (!TryPoint(tD, out long dpx, out long dpy)) continue;
                if (cx == dpx && cy == dpy) continue; // degenerate second segment

                yield return new IntersectCase(ax, ay, bx, by, cx, cy, dpx, dpy);
                built++;
            }
        }

        // ---- exact ground truth -----------------------------------------------------------------

        /// <summary>Exact sign of orient(p0, p1, q) over Z, via BigInteger.</summary>
        internal static int ExactOrient(long p0x, long p0y, long p1x, long p1y, long qx, long qy)
        {
            BigInteger v = (BigInteger)(p1x - p0x) * (qy - p0y)
                         - (BigInteger)(qx - p0x) * (p1y - p0y);
            return v.Sign;
        }

        /// <summary>
        /// Exact intersection classification of the two segments, mirroring the
        /// predicate's dispatch order: strict separation (None) takes priority,
        /// then a proper interior crossing (Point), otherwise Collinear (some
        /// endpoint is exactly on the other supporting line / overlap).
        /// </summary>
        internal static IntersectSign ExactClass(in IntersectCase c)
        {
            int o1 = ExactOrient(c.Ax, c.Ay, c.Bx, c.By, c.Cx, c.Cy); // C vs line AB
            int o2 = ExactOrient(c.Ax, c.Ay, c.Bx, c.By, c.Dx, c.Dy); // D vs line AB
            int o3 = ExactOrient(c.Cx, c.Cy, c.Dx, c.Dy, c.Ax, c.Ay); // A vs line CD
            int o4 = ExactOrient(c.Cx, c.Cy, c.Dx, c.Dy, c.Bx, c.By); // B vs line CD

            bool separated =
                (o1 > 0 && o2 > 0) || (o1 < 0 && o2 < 0) ||
                (o3 > 0 && o4 > 0) || (o3 < 0 && o4 < 0);
            if (separated) return IntersectSign.None;

            bool proper = (o1 * o2 < 0) && (o3 * o4 < 0);
            if (proper) return IntersectSign.Point;

            return IntersectSign.Collinear;
        }
    }
}
