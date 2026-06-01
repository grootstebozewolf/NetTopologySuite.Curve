using System;
using System.Numerics;
using NetTopologySuite.Algorithm;
using NUnit.Framework;

namespace NetTopologySuite.Test.Robust
{
    /// <summary>
    /// A counterexample hunter for the double-double (DD) orientation predicate in the NTS
    /// mirror of JTS — <see cref="CGAlgorithmsDD.OrientationIndex(double,double,double,double,double,double)"/>.
    /// <para/>
    /// This mirrors the JTS-side <c>OrientationDDRobustnessTest</c>: it searches adversarial,
    /// near-collinear configurations whose orientation determinant is computed from products
    /// that overflow the 53-bit double mantissa, and compares three predicates against the
    /// <i>exact</i> sign of the real-number determinant (computed with <see cref="BigInteger"/>):
    /// <list type="bullet">
    /// <item><description><b>naive</b> — the plain <c>double</c> cross product;</description></item>
    /// <item><description><b>DD</b> — <see cref="CGAlgorithmsDD"/> (~106-bit double-double);</description></item>
    /// <item><description><b>exact</b> — the ground-truth sign.</description></item>
    /// </list>
    /// The hunter is deliberately non-vacuous: on the same generated cases the naive predicate
    /// is <i>expected</i> to flip many times, which proves the generator produces genuinely hard
    /// inputs — while DD is expected to never flip. The test therefore doubles as a regression
    /// guard: if a future change weakened <see cref="CGAlgorithmsDD"/> back toward the naive
    /// computation, the DD-failure assertion would fire.
    /// <para/>
    /// Relevant to locationtech/jts#1106 ("Point-Line Orientation robustness issues"): rather
    /// than restricting the input domain, it quantifies empirically why DD is the robust choice.
    /// This is strong empirical evidence, not a proof — DD is ~106-bit, not adaptive-exact.
    /// </summary>
    [TestFixture]
    public class OrientationDDRobustnessTest
    {
        // ~106-bit DD vs. up-to-~2^60 products: comfortably enough cases to surface naive flips,
        // small enough to keep the fixture fast in CI.
        private const int HunterCaseCount = 100_000;
        private const int Seed = 1106; // deterministic; named after the JTS issue.

        /// <summary>
        /// Exact sign of the orientation determinant of three points with <c>double</c>
        /// coordinates, evaluated over the reals (no rounding) via <see cref="BigInteger"/>.
        /// Uses the same determinant arrangement as <see cref="CGAlgorithmsDD"/>:
        /// <c>(p2-p1) x (q-p2)</c>, which is algebraically identical to the textbook form.
        /// </summary>
        internal static int ExactOrientation(
            double p1x, double p1y, double p2x, double p2y, double qx, double qy)
        {
            Span<double> ords = stackalloc double[6] { p1x, p1y, p2x, p2y, qx, qy };
            var mant = new BigInteger[6];
            int minExp = int.MaxValue;
            var exp = new int[6];
            for (int i = 0; i < 6; i++)
            {
                (mant[i], exp[i]) = Decompose(ords[i]);
                if (exp[i] < minExp) minExp = exp[i];
            }

            // Scale every ordinate to an exact integer m * 2^(exp-minExp); the shared 2^minExp
            // factor cancels in the determinant and never affects the sign.
            BigInteger N(int i) => mant[i] << (exp[i] - minExp);

            BigInteger dx1 = N(2) - N(0);
            BigInteger dy1 = N(3) - N(1);
            BigInteger dx2 = N(4) - N(2);
            BigInteger dy2 = N(5) - N(3);

            BigInteger det = dx1 * dy2 - dy1 * dx2;
            return det.Sign;
        }

        /// <summary>Decompose a finite double into an exact <c>mantissa * 2^exp</c> pair.</summary>
        private static (BigInteger mant, int exp) Decompose(double d)
        {
            long bits = BitConverter.DoubleToInt64Bits(d);
            int rawExp = (int)((bits >> 52) & 0x7FF);
            long rawMant = bits & 0xF_FFFF_FFFF_FFFFL;

            BigInteger mant;
            int exp;
            if (rawExp == 0)
            {
                // subnormal (or zero): no implicit leading bit
                mant = rawMant;
                exp = -1074;
            }
            else
            {
                mant = rawMant | 0x10_0000_0000_0000L; // restore implicit 1.xxx bit
                exp = rawExp - 1075;                   // bias 1023 + 52 fraction bits
            }
            if (bits < 0) mant = -mant;
            return (mant, exp);
        }

        /// <summary>The fragile predicate DD replaced: a plain <c>double</c> cross product.</summary>
        private static int NaiveOrientation(
            double p1x, double p1y, double p2x, double p2y, double qx, double qy)
        {
            double dx1 = p2x - p1x, dy1 = p2y - p1y;
            double dx2 = qx - p2x, dy2 = qy - p2y;
            return Math.Sign(dx1 * dy2 - dy1 * dx2);
        }

        private static int DdOrientation(
            double p1x, double p1y, double p2x, double p2y, double qx, double qy)
            => CGAlgorithmsDD.OrientationIndex(p1x, p1y, p2x, p2y, qx, qy);

        /// <summary>Extended Euclid: returns g = gcd(a,b) and x,y with a*x + b*y = g.</summary>
        private static long ExtGcd(long a, long b, out long x, out long y)
        {
            if (b == 0) { x = 1; y = 0; return a; }
            long g = ExtGcd(b, a % b, out long x1, out long y1);
            x = y1;
            y = x1 - (a / b) * y1;
            return g;
        }

        /// <summary>
        /// The core hunt: across <see cref="HunterCaseCount"/> adversarial cases, DD must match the
        /// exact sign on every one (zero counterexamples), while the naive predicate is expected to
        /// flip on many — proving the search is real.
        /// <para/>
        /// Each case is engineered to be genuinely near-collinear in the orientation sense: a small
        /// integer determinant (target in {-2..2}) formed from <i>large</i> individual products
        /// (&gt; 2^53). A point merely close to a long segment is NOT near-collinear — its
        /// determinant is base*height, which stays large. So q is solved from the Diophantine
        /// equation dx*ey - dy*ex = target (with gcd(dx,dy)=1), then shifted along the direction so
        /// the products dx*ey and dy*ex individually exceed the 53-bit mantissa and the naive
        /// subtraction loses the sign.
        /// </summary>
        [Test]
        public void Dd_HasNoCounterexamples_AgainstExactSign()
        {
            var rng = new Random(Seed);

            int ddFailures = 0;
            int naiveFailures = 0;
            int productOverflow = 0;   // cases where a product exceeds 2^53 (naive can't be trusted)
            int built = 0;
            (double, double, double, double, double, double) firstDdFail = default;

            while (built < HunterCaseCount)
            {
                // Coprime direction near 2^27 so products land in 2^54..2^58.
                long dx = rng.NextInt64(1L << 25, 1L << 28);
                long dy = rng.NextInt64(1L << 25, 1L << 28);
                if (rng.Next(2) == 0) dy = -dy;
                long g = ExtGcd(dx, dy, out long s, out long t);
                if (g != 1) continue;                 // need gcd 1 to hit |det| == 1 exactly

                long p1x = rng.NextInt64(-(1L << 20), 1L << 20);
                long p1y = rng.NextInt64(-(1L << 20), 1L << 20);
                long p2x = p1x + dx, p2y = p1y + dy;

                int target = rng.Next(-2, 3);          // exact determinant in {-2,-1,0,1,2}

                // dx*ey - dy*ex = target, base solution from dx*s + dy*t = 1:
                long ey = s * target;
                long ex = -t * target;
                // Shift by k along (dx,dy) to inflate the products without changing the determinant.
                long k = rng.NextInt64(2, 16) * (rng.Next(2) == 0 ? 1 : -1);
                ey += k * dy;
                ex += k * dx;

                long qx = p2x + ex, qy = p2y + ey;
                if (Math.Abs(qx) >= (1L << 52) || Math.Abs(qy) >= (1L << 52)) continue; // keep exact as double

                double ax = p1x, ay = p1y, bx = p2x, by = p2y, cx = qx, cy = qy;

                int exact = ExactOrientation(ax, ay, bx, by, cx, cy);
                int dd = DdOrientation(ax, ay, bx, by, cx, cy);
                int naive = NaiveOrientation(ax, ay, bx, by, cx, cy);

                // Sanity: the exact oracle must reproduce the constructed determinant's sign.
                Assert.That(exact, Is.EqualTo(Math.Sign(target)), "Exact oracle disagreed with construction.");

                if (Math.Abs((double)dx * ey) > 9.007e15 || Math.Abs((double)dy * ex) > 9.007e15)
                    productOverflow++;

                if (dd != exact)
                {
                    if (ddFailures == 0) firstDdFail = (ax, ay, bx, by, cx, cy);
                    ddFailures++;
                }
                if (naive != exact) naiveFailures++;
                built++;
            }

            TestContext.WriteLine(
                $"cases={HunterCaseCount:N0}  product-overflow={productOverflow:N0}  " +
                $"naive flips={naiveFailures:N0}  DD flips={ddFailures:N0}");

            Assert.Multiple(() =>
            {
                Assert.That(ddFailures, Is.Zero,
                    $"DD orientation disagreed with the exact sign; first counterexample: {firstDdFail}");
                Assert.That(naiveFailures, Is.GreaterThan(0),
                    "Generator was vacuous — the naive predicate found no failures, so the hunt " +
                    "did not exercise the hard near-collinear regime.");
            });
        }

        /// <summary>
        /// A targeted minimal-determinant construction at the product-overflow scale: choose a
        /// direction near 2^30 and a query point that makes the true determinant exactly +/-1 or 0,
        /// the smallest nonzero values. DD must still report the correct sign on every one.
        /// </summary>
        [Test]
        public void Dd_ResolvesMinimalDeterminant_AtProductOverflowScale()
        {
            var rng = new Random(Seed + 1);
            int checks = 0;
            int naiveFlips = 0;

            while (checks < 10_000)
            {
                // Coprime direction near 2^27 so the products dx*ey, dy*ex reach 2^54..2^58.
                long dx = rng.NextInt64(1L << 25, 1L << 28);
                long dy = rng.NextInt64(1L << 25, 1L << 28);
                if (rng.Next(2) == 0) dy = -dy;
                if (ExtGcd(dx, dy, out long s, out long t) != 1) continue;

                long p1x = rng.NextInt64(-(1L << 20), 1L << 20);
                long p1y = rng.NextInt64(-(1L << 20), 1L << 20);
                long p2x = p1x + dx, p2y = p1y + dy;

                int target = rng.Next(-1, 2);          // smallest determinants: -1, 0, +1
                long ey = s * target;
                long ex = -t * target;
                long k = rng.NextInt64(2, 16) * (rng.Next(2) == 0 ? 1 : -1);
                ey += k * dy;
                ex += k * dx;

                long qx = p2x + ex, qy = p2y + ey;
                if (Math.Abs(qx) >= (1L << 52) || Math.Abs(qy) >= (1L << 52)) continue;

                double ax = p1x, ay = p1y, bx = p2x, by = p2y, cx = qx, cy = qy;
                int exact = ExactOrientation(ax, ay, bx, by, cx, cy);
                int dd = DdOrientation(ax, ay, bx, by, cx, cy);

                Assert.That(exact, Is.EqualTo(Math.Sign(target)),
                    "Exact oracle disagreed with the constructed determinant.");
                Assert.That(dd, Is.EqualTo(exact),
                    $"DD flipped on a minimal-determinant case det={target}: " +
                    $"({ax},{ay})({bx},{by})({cx},{cy})");

                if (NaiveOrientation(ax, ay, bx, by, cx, cy) != exact) naiveFlips++;
                checks++;
            }

            TestContext.WriteLine($"minimal-determinant cases verified: {checks:N0}  (naive flips: {naiveFlips:N0})");
            Assert.That(naiveFlips, Is.GreaterThan(0),
                "Even at the overflow scale the naive predicate never flipped — construction too easy.");
        }

        /// <summary>
        /// The near-collinear band from the RocqRef oracle study: as q approaches the line
        /// y = x, the naive predicate commits to a sign throughout the non-certifiable band,
        /// whereas DD resolves it correctly (matching the exact sign) once it is nonzero.
        /// </summary>
        [TestCase(0.0, 0)]
        [TestCase(1e-17, 0)]   // below the double ulp at 0.5 -> stored qy == 0.5 -> collinear
        [TestCase(5e-17, 0)]
        [TestCase(1e-16, 1)]   // first representable step above 0.5 -> truly above the line
        [TestCase(2e-16, 1)]
        [TestCase(5e-16, 1)]
        [TestCase(1e-15, 1)]
        [TestCase(1e-14, 1)]
        public void Dd_MatchesExactSign_OnNearCollinearBand(double delta, int expectedSign)
        {
            double qy = 0.5 + delta;
            int exact = ExactOrientation(0.0, 0.0, 1.0, 1.0, 0.5, qy);
            int dd = DdOrientation(0.0, 0.0, 1.0, 1.0, 0.5, qy);

            Assert.That(exact, Is.EqualTo(expectedSign), "Exact oracle sign mismatch.");
            Assert.That(dd, Is.EqualTo(exact), "DD disagreed with the exact sign on the near-collinear band.");
        }
    }
}
