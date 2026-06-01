using NUnit.Framework;
using static NetTopologySuite.Test.Robust.AdversarialOrientationCases;

namespace NetTopologySuite.Test.Robust
{
    /// <summary>
    /// A counterexample hunter for the double-double (DD) orientation predicate in the NTS mirror
    /// of JTS — <see cref="NetTopologySuite.Algorithm.CGAlgorithmsDD"/> — checked against an exact
    /// BigInteger oracle (see <see cref="AdversarialOrientationCases.ExactOrientation"/>).
    /// <para/>
    /// Mirrors the JTS-side <c>OrientationDDRobustnessTest</c>. The hunt is deliberately
    /// non-vacuous: on the same adversarial near-collinear cases the naive double predicate is
    /// <i>expected</i> to flip many times (proving the inputs are genuinely hard), while DD is
    /// expected to never flip. It therefore doubles as a regression guard: any weakening of
    /// <c>CGAlgorithmsDD</c> back toward the naive computation would trip the DD-failure assertion.
    /// <para/>
    /// Relevant to locationtech/jts#1106: rather than restricting the input domain, it quantifies
    /// empirically why DD is the robust choice. Strong empirical evidence, not a proof — DD is
    /// ~106-bit, not adaptive-exact. (The companion <see cref="OrientationDDExactOracleHuntTest"/>
    /// re-runs the same hunt against the proof-extracted Z² <c>ORIENT_EXACT</c> oracle.)
    /// </summary>
    [TestFixture]
    public class OrientationDDRobustnessTest
    {
        private const int HunterCaseCount = 100_000;
        private const int Seed = 1106; // deterministic; named after the JTS issue.

        [Test]
        public void Dd_HasNoCounterexamples_AgainstExactSign()
        {
            int ddFailures = 0, naiveFailures = 0;
            OrientCase firstDdFail = default;

            foreach (var c in NearCollinear(HunterCaseCount, Seed))
            {
                int exact = ExactOrientation(c.P1x, c.P1y, c.P2x, c.P2y, c.Qx, c.Qy);
                // The BigInteger oracle must reproduce the determinant the case was built to have.
                Assert.That(exact, Is.EqualTo(c.ExactSign), "Exact oracle disagreed with construction.");

                if (DdOrientation(c.P1x, c.P1y, c.P2x, c.P2y, c.Qx, c.Qy) != exact)
                {
                    if (ddFailures == 0) firstDdFail = c;
                    ddFailures++;
                }
                if (NaiveOrientation(c.P1x, c.P1y, c.P2x, c.P2y, c.Qx, c.Qy) != exact) naiveFailures++;
            }

            TestContext.WriteLine(
                $"cases={HunterCaseCount:N0}  naive flips={naiveFailures:N0}  DD flips={ddFailures:N0}");

            Assert.Multiple(() =>
            {
                Assert.That(ddFailures, Is.Zero,
                    $"DD orientation disagreed with the exact sign; first counterexample: {firstDdFail}");
                Assert.That(naiveFailures, Is.GreaterThan(0),
                    "Generator was vacuous — the naive predicate found no failures.");
            });
        }

        [Test]
        public void Dd_ResolvesMinimalDeterminant_AtProductOverflowScale()
        {
            int checks = 0, naiveFlips = 0;

            foreach (var c in MinimalDeterminant(10_000, Seed + 1))
            {
                int exact = ExactOrientation(c.P1x, c.P1y, c.P2x, c.P2y, c.Qx, c.Qy);
                Assert.That(exact, Is.EqualTo(c.ExactSign), "Exact oracle disagreed with construction.");
                Assert.That(DdOrientation(c.P1x, c.P1y, c.P2x, c.P2y, c.Qx, c.Qy), Is.EqualTo(exact),
                    $"DD flipped on a minimal-determinant case det={c.ExactSign}: {c}");
                if (NaiveOrientation(c.P1x, c.P1y, c.P2x, c.P2y, c.Qx, c.Qy) != exact) naiveFlips++;
                checks++;
            }

            TestContext.WriteLine($"minimal-determinant cases verified: {checks:N0}  (naive flips: {naiveFlips:N0})");
            Assert.That(naiveFlips, Is.GreaterThan(0),
                "Even at the overflow scale the naive predicate never flipped — construction too easy.");
        }

        /// <summary>
        /// The near-collinear band from the RocqRef oracle study: as q approaches the line y = x,
        /// the naive predicate commits to a sign throughout the non-certifiable band, whereas DD
        /// resolves it correctly (matching the exact sign) once it is nonzero.
        /// </summary>
        [TestCase(0.0, 0)]
        [TestCase(1e-17, 0)]   // below the ulp at 0.5 -> stored qy == 0.5 -> collinear
        [TestCase(5e-17, 0)]
        [TestCase(1e-16, 1)]   // first representable step above 0.5
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
