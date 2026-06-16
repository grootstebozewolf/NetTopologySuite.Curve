// =============================================================================
// RobustLineIntersectorHunterTests
// -----------------------------------------------------------------------------
// Counterexample hunters for RobustLineIntersector.SignFiltered against an
// exact BigInteger four-orientation classifier (AdversarialIntersectCases).
//
// Properties hunted:
//
//   DIFFERENTIAL (easy regime) -- on small integer segment pairs the filtered
//     predicate must reproduce the exact classification exactly; this also
//     validates the in-process oracle itself.
//
//   SOUNDNESS (hard regime) -- the filter must never commit a wrong definite
//     intersection: SignFiltered == None implies the segments truly do not
//     share a point, and SignFiltered == Point implies a true interior
//     crossing.  On hard cases it is allowed to return Uncertain (or
//     Collinear, the Stage-A masking analogue), but never a false None/Point.
//
//   NON-VACUITY -- the same hard cases drive the filter into Uncertain
//     (uncertain > 0), and the unfiltered naive composition (RobustOrientation
//     .Sign, no Stage-A filter) disagrees with exact truth (naiveDisagree > 0),
//     proving the inputs are genuinely hard.
// =============================================================================

using NetTopologySuite.Robust.Intersect;
using NetTopologySuite.Robust.Orientation;
using NUnit.Framework;
using static NetTopologySuite.Test.Robust.Intersect.AdversarialIntersectCases;

namespace NetTopologySuite.Test.Robust.Intersect
{
    [TestFixture]
    public class RobustLineIntersectorHunterTests
    {
        private const int Seed = 1106;

        private static bool SameStrict(OrientSign a, OrientSign b) =>
            (a == OrientSign.Pos && b == OrientSign.Pos) ||
            (a == OrientSign.Neg && b == OrientSign.Neg);

        // The unfiltered predicate: the same dispatch as SignFiltered but on the
        // naive four-valued orientation sign (no Uncertain stage).
        private static IntersectSign NaiveClass(in IntersectCase c)
        {
            var pq0 = RobustOrientation.Sign(c.A, c.B, c.C);
            var pq1 = RobustOrientation.Sign(c.A, c.B, c.D);
            var qp0 = RobustOrientation.Sign(c.C, c.D, c.A);
            var qp1 = RobustOrientation.Sign(c.C, c.D, c.B);

            if (pq0 == OrientSign.Nan || pq1 == OrientSign.Nan ||
                qp0 == OrientSign.Nan || qp1 == OrientSign.Nan)
                return IntersectSign.Nan;
            if (SameStrict(pq0, pq1) || SameStrict(qp0, qp1))
                return IntersectSign.None;
            if (pq0 == OrientSign.Zero || pq1 == OrientSign.Zero ||
                qp0 == OrientSign.Zero || qp1 == OrientSign.Zero)
                return IntersectSign.Collinear;
            return IntersectSign.Point;
        }

        private static IntersectSign Filter(in IntersectCase c) =>
            RobustLineIntersector.SignFiltered(c.A, c.B, c.C, c.D);

        // -------------------------------------------------------------------------
        [Test]
        public void SignFiltered_MatchesExact_OnEasyIntegerPairs()
        {
            const int caseCount = 50_000;
            int mismatches = 0;
            IntersectCase first = default;
            foreach (var c in RandomSmall(caseCount, Seed))
            {
                var f = Filter(c);
                if (f != ExactClass(c))
                {
                    if (mismatches == 0) first = c;
                    mismatches++;
                }
            }
            Assert.That(mismatches, Is.Zero,
                $"Filtered predicate diverged from exact classification on an easy pair: {first}");
        }

        // -------------------------------------------------------------------------
        [Test]
        public void SignFiltered_NeverFalseNoneOrPoint_OnAdversarialPairs()
        {
            const int caseCount = 100_000;

            int falseNone = 0, falsePoint = 0, uncertain = 0, uncOverNaiveCommit = 0, naiveDisagree = 0;
            IntersectCase firstBad = default;

            foreach (var c in Adversarial(caseCount, Seed + 5))
            {
                var exact = ExactClass(c);
                var f = Filter(c);
                var naive = NaiveClass(c);

                if (f == IntersectSign.Uncertain) uncertain++;
                if (f == IntersectSign.None && exact != IntersectSign.None)
                {
                    if (falseNone + falsePoint == 0) firstBad = c;
                    falseNone++;
                }
                if (f == IntersectSign.Point && exact != IntersectSign.Point)
                {
                    if (falseNone + falsePoint == 0) firstBad = c;
                    falsePoint++;
                }

                // The filter doing real work: it declines (Uncertain) to certify a
                // result the unfiltered naive composition commits to (None/Point).
                if (f == IntersectSign.Uncertain &&
                    (naive == IntersectSign.None || naive == IntersectSign.Point))
                    uncOverNaiveCommit++;
                if (naive != exact) naiveDisagree++;
            }

            TestContext.WriteLine(
                $"cases={caseCount:N0}  false None={falseNone:N0}  false Point={falsePoint:N0}  " +
                $"filter Uncertain={uncertain:N0}  Uncertain-over-naive-commit={uncOverNaiveCommit:N0}  " +
                $"naive disagreements={naiveDisagree:N0}");

            Assert.Multiple(() =>
            {
                Assert.That(falseNone, Is.Zero, $"SignFiltered reported None on intersecting/touching segments: {firstBad}");
                Assert.That(falsePoint, Is.Zero, $"SignFiltered reported Point on non-crossing segments: {firstBad}");
                Assert.That(uncOverNaiveCommit, Is.GreaterThan(0),
                    "Filter never declined a commitment the naive composition made -- the hunt is not exercising the Stage-A band.");
                Assert.That(naiveDisagree, Is.GreaterThan(0),
                    "Generator was vacuous -- the unfiltered naive composition agreed with exact truth everywhere.");
            });
        }
    }
}
