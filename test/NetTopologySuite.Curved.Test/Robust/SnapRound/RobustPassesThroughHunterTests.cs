// =============================================================================
// RobustPassesThroughHunterTests
// -----------------------------------------------------------------------------
// Adversarial property hunts for the hot-pixel passes-through predicates
// (RobustPassesThrough.PassesThroughFilter / PassesThroughHalfOpen).  Both are
// Liang-Barsky tests on the raw segment AND on the half-to-even-snapped
// segment against the unit pixel [cx-0.5,cx+0.5] x [cy-0.5,cy+0.5].
//
// Cases are drawn on a dyadic grid (multiples of 1/4) around an integer pixel
// centre -- the regime that maximises grazing of the pixel edges, where the
// closed/half-open distinction and the snap rounding actually bite.
//
// Properties hunted (each over ~1M adversarial cases):
//
//   1. CONTAINMENT INVARIANT -- the documented load-bearing relationship:
//      PassesThroughHalfOpen TRUE  ==>  PassesThroughFilter TRUE
//      (equivalently Filter FALSE ==> HalfOpen FALSE).  Non-vacuous: the
//      closed-but-not-open boundary divergence (Filter && !HalfOpen) must occur.
//
//   2. OPEN-INTERIOR COMPLETENESS -- if both endpoints are strictly inside the
//      open pixel, HalfOpen must be TRUE.
//
//   3. SEPARATION SOUNDNESS -- if the segment lies strictly on one side of a
//      pixel slab (its bounding box misses the closed pixel), Filter is FALSE.
//
// Plus deterministic edge cases pinning the half-open top/right-edge exclusion.
// =============================================================================

using System;
using NetTopologySuite.Robust.SnapRound;
using NetTopologySuite.Robust.Simplify;
using NUnit.Framework;

namespace NetTopologySuite.Test.Robust.SnapRound
{
    [TestFixture]
    public class RobustPassesThroughHunterTests
    {
        private static readonly double[] Grid =
            { -1.5, -1.25, -1.0, -0.75, -0.5, -0.25, 0.0, 0.25, 0.5, 0.75, 1.0, 1.25, 1.5 };

        private const int CaseCount = 1_000_000;

        private static bool Filter(BPoint p0, BPoint p1, BPoint c) =>
            RobustPassesThrough.PassesThroughFilter(p0, p1, c);

        private static bool HalfOpen(BPoint p0, BPoint p1, BPoint c) =>
            RobustPassesThrough.PassesThroughHalfOpen(p0, p1, c);

        // -------------------------------------------------------------------------
        [Test]
        public void HalfOpen_Implies_Filter_WithBoundaryDivergence()
        {
            var rng = new Random(1106);
            int halfOpenNotFilter = 0, divergence = 0;
            (BPoint, BPoint, BPoint) firstBad = default;

            for (int i = 0; i < CaseCount; i++)
            {
                double cx = rng.Next(-3, 4), cy = rng.Next(-3, 4);
                var c = new BPoint(cx, cy);
                var p0 = new BPoint(cx + Pick(rng), cy + Pick(rng));
                var p1 = new BPoint(cx + Pick(rng), cy + Pick(rng));

                bool f = Filter(p0, p1, c), h = HalfOpen(p0, p1, c);
                if (h && !f) { if (halfOpenNotFilter == 0) firstBad = (p0, p1, c); halfOpenNotFilter++; }
                if (f && !h) divergence++;
            }

            TestContext.WriteLine($"cases={CaseCount:N0}  HalfOpen&&!Filter={halfOpenNotFilter:N0}  Filter&&!HalfOpen={divergence:N0}");
            Assert.Multiple(() =>
            {
                Assert.That(halfOpenNotFilter, Is.Zero,
                    $"HalfOpen reported pass-through but Filter did not (Filter must over-approximate): {firstBad}");
                Assert.That(divergence, Is.GreaterThan(0),
                    "No closed-vs-half-open divergence found -- the hunt is not grazing the pixel boundary.");
            });
        }

        // -------------------------------------------------------------------------
        [Test]
        public void BothEndpointsStrictlyInsideOpenPixel_ImpliesHalfOpen()
        {
            var rng = new Random(2106);
            int inside = 0, violations = 0;
            (BPoint, BPoint, BPoint) firstBad = default;

            for (int i = 0; i < CaseCount; i++)
            {
                double cx = rng.Next(-3, 4), cy = rng.Next(-3, 4);
                var c = new BPoint(cx, cy);
                var p0 = new BPoint(cx + Pick(rng), cy + Pick(rng));
                var p1 = new BPoint(cx + Pick(rng), cy + Pick(rng));

                if (InsideOpen(p0, c) && InsideOpen(p1, c))
                {
                    inside++;
                    if (!HalfOpen(p0, p1, c)) { if (violations == 0) firstBad = (p0, p1, c); violations++; }
                }
            }

            TestContext.WriteLine($"inside-open cases={inside:N0}  HalfOpen violations={violations:N0}");
            Assert.Multiple(() =>
            {
                Assert.That(violations, Is.Zero, $"Segment fully inside the open pixel was not reported HalfOpen: {firstBad}");
                Assert.That(inside, Is.GreaterThan(0), "No inside-open cases sampled.");
            });
        }

        // -------------------------------------------------------------------------
        [Test]
        public void StrictlySeparatedSegment_ImpliesNotFilter()
        {
            var rng = new Random(3106);
            int separated = 0, violations = 0;
            (BPoint, BPoint, BPoint) firstBad = default;

            for (int i = 0; i < CaseCount; i++)
            {
                double cx = rng.Next(-3, 4), cy = rng.Next(-3, 4);
                var c = new BPoint(cx, cy);
                var p0 = new BPoint(cx + Pick(rng), cy + Pick(rng));
                var p1 = new BPoint(cx + Pick(rng), cy + Pick(rng));

                bool sepX = (p0.X > cx + 0.5 && p1.X > cx + 0.5) || (p0.X < cx - 0.5 && p1.X < cx - 0.5);
                bool sepY = (p0.Y > cy + 0.5 && p1.Y > cy + 0.5) || (p0.Y < cy - 0.5 && p1.Y < cy - 0.5);
                if (sepX || sepY)
                {
                    separated++;
                    if (Filter(p0, p1, c)) { if (violations == 0) firstBad = (p0, p1, c); violations++; }
                }
            }

            TestContext.WriteLine($"separated cases={separated:N0}  Filter violations={violations:N0}");
            Assert.Multiple(() =>
            {
                Assert.That(violations, Is.Zero, $"Strictly separated segment was reported as passing through: {firstBad}");
                Assert.That(separated, Is.GreaterThan(0), "No separated cases sampled.");
            });
        }

        // ---- deterministic edge cases -------------------------------------------
        [Test]
        public void TopEdge_ClosedTouches_HalfOpenExcludes()
        {
            // Horizontal segment along y = cy + 0.5 through the pixel at origin.
            var p0 = new BPoint(-1.0, 0.5);
            var p1 = new BPoint(1.0, 0.5);
            var c = new BPoint(0.0, 0.0);
            Assert.Multiple(() =>
            {
                Assert.That(Filter(p0, p1, c), Is.True, "closed pixel includes the top edge");
                Assert.That(HalfOpen(p0, p1, c), Is.False, "half-open pixel excludes the top edge");
            });
        }

        [Test]
        public void RightEdge_ClosedTouches_HalfOpenExcludes()
        {
            // Vertical segment along x = cx + 0.5 through the pixel at origin.
            var p0 = new BPoint(0.5, -1.0);
            var p1 = new BPoint(0.5, 1.0);
            var c = new BPoint(0.0, 0.0);
            Assert.Multiple(() =>
            {
                Assert.That(Filter(p0, p1, c), Is.True, "closed pixel includes the right edge");
                Assert.That(HalfOpen(p0, p1, c), Is.False, "half-open pixel excludes the right edge");
            });
        }

        [Test]
        public void SegmentThroughCentre_BothTrue()
        {
            var p0 = new BPoint(-1.0, -1.0);
            var p1 = new BPoint(1.0, 1.0);
            var c = new BPoint(0.0, 0.0);
            Assert.Multiple(() =>
            {
                Assert.That(Filter(p0, p1, c), Is.True);
                Assert.That(HalfOpen(p0, p1, c), Is.True);
            });
        }

        [Test]
        public void SegmentClearlyOutside_BothFalse()
        {
            var p0 = new BPoint(3.0, 3.0);
            var p1 = new BPoint(5.0, 4.0);
            var c = new BPoint(0.0, 0.0);
            Assert.Multiple(() =>
            {
                Assert.That(Filter(p0, p1, c), Is.False);
                Assert.That(HalfOpen(p0, p1, c), Is.False);
            });
        }

        private static double Pick(Random rng) => Grid[rng.Next(Grid.Length)];

        private static bool InsideOpen(BPoint p, BPoint c) =>
            p.X > c.X - 0.5 && p.X < c.X + 0.5 && p.Y > c.Y - 0.5 && p.Y < c.Y + 0.5;
    }
}
