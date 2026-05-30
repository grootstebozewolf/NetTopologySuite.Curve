// =============================================================================
// NetTopologySuite.Curve.Robust.Arc.ArcPassesThroughHotPixelRocqRefTests
// -----------------------------------------------------------------------------
// Differential tests against the RocqRefRunner -- the Coq-extracted reference
// binary built from NetTopologySuite.Proofs/oracle/.  The mode shipped in
// the Phase-4 (Workstream B) artifact:
//
//   ARC_PASSES_THROUGH_PIXEL <-> arc_passes_through_hot_pixel
//                                  (theories/ArcHotPixel.v:95)
//
// Stdin: one mode line, then four "<x> <y>" lines (arc_start, arc_mid,
// arc_end, pixel_center), then one "<scale>" line.  Stdout: "TRUE" or
// "FALSE".  Persistent oracle process owned by the fixture.
//
// Observed reply values in each TestCase comment captured by probe against
// oracle_bin built from NetTopologySuite.Proofs main @ 1ca583c5 (Phase-3+4
// artifact run 26678597803, artifact 7306659452).
//
// Skipped by default; activate by pointing ROCQ_REF_BIN at the Phase-4-
// aware RocqRefRunner binary.
// =============================================================================

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using NetTopologySuite.Robust.Arc;
using NetTopologySuite.Robust.Simplify;
using NUnit.Framework;

namespace NetTopologySuite.Test.Robust.Arc
{
    [TestFixture]
    [Category("RocqRef")]
    public class ArcPassesThroughHotPixelRocqRefTests
    {
        private const string RocqRefPathEnvVar = "ROCQ_REF_BIN";

        private Process _proc;
        private StreamWriter _stdin;
        private StreamReader _stdout;
        private readonly object _ioLock = new object();

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            string rocqRefPath = Environment.GetEnvironmentVariable(RocqRefPathEnvVar);
            if (string.IsNullOrWhiteSpace(rocqRefPath) || !File.Exists(rocqRefPath))
            {
                Assert.Ignore(
                    "RocqRef differential tests skipped: set " + RocqRefPathEnvVar +
                    " to the path of a Phase-4-aware RocqRefRunner binary " +
                    "(NetTopologySuite.Proofs/oracle/oracle_bin).");
            }

            var psi = new ProcessStartInfo
            {
                FileName = rocqRefPath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            _proc = Process.Start(psi);
            if (_proc == null)
            {
                Assert.Fail("RocqRefRunner process failed to start: " + rocqRefPath);
            }
            _stdin = _proc.StandardInput;
            _stdout = _proc.StandardOutput;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (_proc != null && !_proc.HasExited)
            {
                try { _stdin?.Close(); } catch { /* ignore */ }
                if (!_proc.WaitForExit(2000))
                {
                    try { _proc.Kill(); } catch { /* ignore */ }
                }
            }
            _proc?.Dispose();
        }

        // ---------------------------------------------------------------------
        // Group 1: each fixture probes a distinct branch of the six-way
        // disjunction.  The unit-circle arc is (1,0)-(0,1)-(-1,0) -- a CCW
        // upper-semicircle that lies entirely in the y >= 0 half-plane.
        // ---------------------------------------------------------------------
        [TestCase( 1, 0,  0, 1,  -1, 0,    1, 0,   1.0,  true,
            TestName = "Unit_Pixel_AtArcStartEndpoint_CapturedByEndpointTest")]
            // observed: TRUE.  arc_start = (1,0); pixel center (1,0) s=1
            // -> bounds [0.5, 1.5) x [-0.5, 0.5).  1 in [0.5, 1.5) and 0
            // in [-0.5, 0.5).  in_hot_pixel_halfopen(arc_start, ...) fires.
        [TestCase( 1, 0,  0, 1,  -1, 0,    0, 0,   1.0,  false,
            TestName = "Unit_Pixel_AtOrigin_ArcAbovePixel_NoCircleEdgeCrossing")]
            // observed: FALSE.  Arc lies in y >= 0 half-plane; pixel at
            // origin spans [-0.5, 0.5) x [-0.5, 0.5).  Arc endpoints (1,0)
            // and (-1,0) are outside the pixel (1 not in [-0.5, 0.5)).  All
            // four pixel edges have both endpoints strictly outside the
            // unit circle (corner distance = sqrt(0.5) ~ 0.707), so each
            // sign-product is positive and predicate is FALSE.  Documents
            // that "arc above pixel" is correctly classified, even though
            // the arc's circle DOES cross the pixel interior at y=+0.707.
        [TestCase( 1, 0,  0, 1,  -1, 0,    10, 10,  1.0,  false,
            TestName = "Unit_Pixel_FarFromArc_NoFire")]
            // observed: FALSE.
        [TestCase( 1, 0,  0, 1,  -1, 0,    0.5, 0.5,  0.5,  true,
            TestName = "Unit_Pixel_OnArcBody_CapturedByEdgeCrossing")]
            // observed: TRUE.  Pixel center (0.5, 0.5), s=0.5 -> bounds
            // [0.25, 0.75) x [0.25, 0.75); all four corners inside the
            // unit circle EXCEPT top-right (0.75, 0.75) at distance
            // sqrt(1.125) ~ 1.061 > 1.  The (br, tr) edge from (0.75, 0.25)
            // [inside] to (0.75, 0.75) [outside] has opposite InCircle
            // signs, sign-product < 0, predicate fires TRUE.
        [TestCase( 1, 0,  0, 1,  -1, 0,    5, 5,  0.001,  false,
            TestName = "Unit_Pixel_TinyAndFar_NoFire")]
            // observed: FALSE.
        [TestCase( 1, 0,  0, 1,  -1, 0,   -1, 0,   1.0,  true,
            TestName = "Unit_Pixel_AtArcEndEndpoint_CapturedByEndpointTest")]
            // observed: TRUE.  Symmetric to the arc_start case.
        public void UnitArc_BitEqual(
            double sx, double sy, double mx, double my, double ex, double ey,
            double cx, double cy, double scale, bool expected)
        {
            AssertBitEqual(
                new BPoint(sx, sy), new BPoint(mx, my), new BPoint(ex, ey),
                new BPoint(cx, cy), scale,
                expected);
        }

        // ---------------------------------------------------------------------
        // Group 2: half-open pixel boundary semantics.  The convention
        // (bottom + left CLOSED, top + right OPEN) is inherited from Phase
        // 2's in_hot_pixel_halfopen; this group probes that the Phase 4
        // arc-aware variant respects the same `<` upper bound.
        // ---------------------------------------------------------------------
        [TestCase( 1, 0,  0, 1,  -1, 0,   1.5, 0.5,  1.0,   true,
            TestName = "Boundary_ArcStart_OnPixelBottomLeftCorner_Closed")]
            // observed: TRUE.  Pixel center (1.5, 0.5), s=1 -> bounds
            // [1.0, 2.0) x [0.0, 1.0).  arc_start=(1, 0): x=1 in [1.0, 2.0)
            // (lo closed), y=0 in [0.0, 1.0) (lo closed).  TRUE.
        [TestCase( 1, 0,  0, 1,  -1, 0,   0.5, -0.5, 1.0,  false,
            TestName = "Boundary_ArcStart_OnPixelTopRightCorner_Open")]
            // observed: FALSE.  Pixel center (0.5, -0.5), s=1 -> bounds
            // [0.0, 1.0) x [-1.0, 0.0).  arc_start=(1, 0): x=1 NOT in
            // [0.0, 1.0) (hi open), so endpoint test fails.  Edge crossings
            // also do not fire (see test code for the geometric reason).
        public void HalfOpenBoundary_BitEqual(
            double sx, double sy, double mx, double my, double ex, double ey,
            double cx, double cy, double scale, bool expected)
        {
            AssertBitEqual(
                new BPoint(sx, sy), new BPoint(mx, my), new BPoint(ex, ey),
                new BPoint(cx, cy), scale,
                expected);
        }

        // ---------------------------------------------------------------------
        // Group 3: the "false positive" case -- the arc's CIRCLE crosses a
        // pixel edge, but the arc itself (a proper subarc of the circle)
        // does not.  Predicate returns TRUE because it operates on the full
        // circle, not on the angular extent of the arc.  Documents this
        // load-bearing soundness-not-completeness behaviour so that future
        // maintainers don't try to "fix" the predicate by adding angular
        // filtering -- that would break the bit-equality contract with the
        // Coq IVT theorem.
        // ---------------------------------------------------------------------
        [TestCase( 1, 0,  0, 1,  -1, 0,    0, -0.5,  1.0,  true,
            TestName = "FalsePositive_PixelBelowArc_CircleStillCrossesPixelEdge")]
            // observed: TRUE.  Arc body lies in y >= 0; pixel at (0, -0.5)
            // s=1 spans [-0.5, 0.5) x [-1.0, 0.0) -- entirely below the
            // x-axis, with no point of the upper-semicircle arc inside.
            // BUT the right edge of the pixel from (0.5, -1) to (0.5, 0)
            // has bottom-right outside and top-right inside the unit
            // circle, so sp * sq < 0 fires the chord-crosses-circle test
            // on that edge.  Predicate returns TRUE even though the arc
            // does NOT geometrically pass through the pixel.  Mirrors the
            // exact Coq semantics: the predicate is about the full circle,
            // not the angular slice the arc occupies.
        public void FalsePositive_CircleNotArc_BitEqual(
            double sx, double sy, double mx, double my, double ex, double ey,
            double cx, double cy, double scale, bool expected)
        {
            AssertBitEqual(
                new BPoint(sx, sy), new BPoint(mx, my), new BPoint(ex, ey),
                new BPoint(cx, cy), scale,
                expected);
        }

        // ---------------------------------------------------------------------
        // Group 4: degenerate / adversarial scale values.
        // ---------------------------------------------------------------------
        [Test]
        public void Adversarial_ScaleZero_EmptyPixel_NoFire()
        {
            // observed: FALSE.  scale=0 -> r=0 -> pixel bounds [cx, cx)
            // which is the empty half-open interval for every point.  All
            // four corners collapse to a single point (cx, cy), so the
            // four chord-crossings have sp == sq (same input), product
            // sp^2 >= 0, predicate FALSE per edge.  Endpoint tests fail
            // because every `< hi` is `< cx` which is false when x = cx.
            var arcStart = new BPoint( 1, 0);
            var arcMid   = new BPoint( 0, 1);
            var arcEnd   = new BPoint(-1, 0);
            var center   = new BPoint( 1, 0);

            AssertBitEqual(arcStart, arcMid, arcEnd, center, 0.0, false);
        }

        [Test]
        public void Adversarial_NegativeScale_InvertedPixel_CanStillFireViaEdges()
        {
            // observed: TRUE.  scale=-1 -> r=-0.5 -> pixel bounds
            // [cx+0.5, cx-0.5) which is empty for endpoint tests.  But the
            // four "corners" are still well-defined points
            //   bl=(cx+0.5, cy+0.5), br=(cx-0.5, cy+0.5),
            //   tr=(cx-0.5, cy-0.5), tl=(cx+0.5, cy-0.5)
            // For arc (1,0)-(0,1)-(-1,0) and center=(1,0):
            //   bl=(1.5, 0.5), br=(0.5, 0.5), tr=(0.5, -0.5), tl=(1.5, -0.5)
            //   (0.5, 0.5) lies inside the unit circle (distance sqrt(0.5)
            //   ~ 0.707 < 1); (1.5, 0.5) lies outside.  Sign product on
            //   the (bl, br) edge < 0, predicate fires TRUE.  Demonstrates
            //   that the predicate's edge logic is independent of pixel-box
            //   degeneracy.
            var arcStart = new BPoint( 1, 0);
            var arcMid   = new BPoint( 0, 1);
            var arcEnd   = new BPoint(-1, 0);
            var center   = new BPoint( 1, 0);

            AssertBitEqual(arcStart, arcMid, arcEnd, center, -1.0, true);
        }

        [Test]
        public void Adversarial_NanScale_AllComparisonsFailToFalse()
        {
            // observed: FALSE.  scale=NaN -> r=NaN -> every corner has NaN
            // coordinates -> every InCircle.Determinant returns NaN ->
            // every NaN < 0 is false -> every edge crossing FALSE.
            // Endpoint tests: (cx - r) and (cx + r) are NaN -> every >=
            // and < comparison vs NaN is false -> endpoint tests FALSE.
            // Six-way disjunction returns FALSE.
            var arcStart = new BPoint( 1, 0);
            var arcMid   = new BPoint( 0, 1);
            var arcEnd   = new BPoint(-1, 0);
            var center   = new BPoint( 0, 0);

            AssertBitEqual(arcStart, arcMid, arcEnd, center, double.NaN, false);
        }

        [Test]
        public void Adversarial_HugeScale_PixelContainsArc_FireFromEndpointTest()
        {
            // observed: TRUE.  scale=100 -> r=50 -> pixel at origin spans
            // [-50, 50) x [-50, 50).  Both arc endpoints lie inside.
            // in_hot_pixel_halfopen(arc_start, ...) fires.
            var arcStart = new BPoint( 1, 0);
            var arcMid   = new BPoint( 0, 1);
            var arcEnd   = new BPoint(-1, 0);
            var center   = new BPoint( 0, 0);

            AssertBitEqual(arcStart, arcMid, arcEnd, center, 100.0, true);
        }

        // ---------------------------------------------------------------------
        // Helpers.
        // ---------------------------------------------------------------------

        private void AssertBitEqual(
            BPoint arcStart, BPoint arcMid, BPoint arcEnd,
            BPoint center, double scale, bool expected)
        {
            bool refResult = RunRocqRef(arcStart, arcMid, arcEnd, center, scale);
            bool csResult = ArcPassesThroughHotPixel.Evaluate(
                arcStart, arcMid, arcEnd, center, scale);

            Assert.That(refResult, Is.EqualTo(expected),
                "oracle disagrees with the expected reference value");
            Assert.That(csResult, Is.EqualTo(refResult),
                "C# ArcPassesThroughHotPixel disagrees with oracle");
        }

        private bool RunRocqRef(
            BPoint arcStart, BPoint arcMid, BPoint arcEnd,
            BPoint center, double scale)
        {
            string line;
            lock (_ioLock)
            {
                _stdin.WriteLine("ARC_PASSES_THROUGH_PIXEL");
                _stdin.WriteLine(Fmt(arcStart.X) + " " + Fmt(arcStart.Y));
                _stdin.WriteLine(Fmt(arcMid.X)   + " " + Fmt(arcMid.Y));
                _stdin.WriteLine(Fmt(arcEnd.X)   + " " + Fmt(arcEnd.Y));
                _stdin.WriteLine(Fmt(center.X)   + " " + Fmt(center.Y));
                _stdin.WriteLine(Fmt(scale));
                _stdin.Flush();
                line = _stdout.ReadLine();
            }
            if (line == null)
            {
                Assert.Fail("RocqRefRunner returned no output for ARC_PASSES_THROUGH_PIXEL");
            }
            return ParseBool(line!.Trim());
        }

        private static bool ParseBool(string s)
        {
            if (s == "TRUE") return true;
            if (s == "FALSE") return false;
            throw new FormatException("Unexpected boolean reply: " + s);
        }

        private static string Fmt(double x)
        {
            if (double.IsNaN(x))              return "nan";
            if (double.IsPositiveInfinity(x)) return "infinity";
            if (double.IsNegativeInfinity(x)) return "neg_infinity";
            return x.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
