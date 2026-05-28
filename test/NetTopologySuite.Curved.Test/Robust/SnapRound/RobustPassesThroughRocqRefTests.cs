// =============================================================================
// NetTopologySuite.Curve.Robust.SnapRound.RobustPassesThroughRocqRefTests
// -----------------------------------------------------------------------------
// Differential tests against the RocqRefRunner -- the Coq-extracted reference
// binary built from NetTopologySuite.Proofs/oracle/.  Two new modes shipped
// in Proofs PR #23:
//
//   PASSES_THROUGH_FILTER   <-> b64_passes_through_hot_pixel
//   PASSES_THROUGH_HALFOPEN <-> b64_passes_through_hot_pixel_halfopen
//
// Both take three BPoints (P0, P1, C) on stdin (one mode line + three
// "<x> <y>" lines, hex-float spelling accepted) and emit "TRUE" or "FALSE"
// on stdout.  The oracle is persistent: a single process serves every
// fixture in this class.  [OneTimeSetUp] starts it, [OneTimeTearDown]
// closes stdin and waits for the OCaml `End_of_file` shutdown path.
//
// Skipped by default; activate by pointing `ROCQ_REF_BIN` at the
// PR-23-aware RocqRefRunner.
// =============================================================================

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using NetTopologySuite.Robust.Simplify;
using NetTopologySuite.Robust.SnapRound;
using NUnit.Framework;

namespace NetTopologySuite.Test.Robust.SnapRound
{
    [TestFixture]
    [Category("RocqRef")]
    public class RobustPassesThroughRocqRefTests
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
                    " to the path of a PR-23-aware RocqRefRunner binary " +
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
            _stdin  = _proc.StandardInput;
            _stdout = _proc.StandardOutput;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            // Closing stdin lets the OCaml dispatch loop hit `End_of_file`
            // on the mode-read and exit cleanly with code 0.
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
        // Transport.  Variadic over the point count so the same helper covers
        // INTERSECT (4 points), ORIENT (3 points), and PASSES_THROUGH (3 points)
        // if it ever moves to a shared base class.  Today it serves only
        // PASSES_THROUGH; the legacy three test classes keep their per-call
        // process spawn (a refactor for non-functional reasons is risk without
        // reward while they're working).
        // ---------------------------------------------------------------------
        private string RunRocqRef(string mode, string ctx, params BPoint[] points)
        {
            if (points.Length == 0)
            {
                throw new ArgumentException(
                    "RunRocqRef: " + mode + " (" + ctx + ") called with no points");
            }
            lock (_ioLock)
            {
                _stdin.WriteLine(mode);
                foreach (var p in points)
                {
                    _stdin.WriteLine(Fmt(p.X) + " " + Fmt(p.Y));
                }
                _stdin.Flush();
                string line = _stdout.ReadLine();
                if (line == null)
                {
                    Assert.Fail("RocqRefRunner returned no output for " +
                                mode + " (" + ctx + ")");
                }
                return line!.Trim();
            }
        }

        private bool RunPassesThroughFilter(BPoint p0, BPoint p1, BPoint c, string ctx) =>
            ParseBool(RunRocqRef("PASSES_THROUGH_FILTER", ctx, p0, p1, c), ctx);

        private bool RunPassesThroughHalfOpen(BPoint p0, BPoint p1, BPoint c, string ctx) =>
            ParseBool(RunRocqRef("PASSES_THROUGH_HALFOPEN", ctx, p0, p1, c), ctx);

        // ---------------------------------------------------------------------
        // Group 1: basic soundness -- C# port bit-equal to oracle on the
        // four canonical branches of the predicate.
        // ---------------------------------------------------------------------
        [TestCase(-1.0, -1.0,  1.0,  1.0,  0.0,  0.0,
            TestName = "Basic_Diagonal_ThroughCenter")]
        [TestCase( 5.0,  5.0,  6.0,  6.0,  0.0,  0.0,
            TestName = "Basic_Segment_FarFromPixel")]
        [TestCase( 0.0,  0.0,  1.0,  0.0,  0.0,  0.0,
            TestName = "Basic_Endpoint_AtCenter")]
        [TestCase(-0.1, -0.1,  0.1,  0.1,  0.0,  0.0,
            TestName = "Basic_BothEndpoints_Inside")]
        public void CSharp_BitEqual_To_RocqRef(
            double x0, double y0, double x1, double y1, double cx, double cy)
        {
            var p0 = new BPoint(x0, y0);
            var p1 = new BPoint(x1, y1);
            var c  = new BPoint(cx, cy);

            bool refFilter   = RunPassesThroughFilter(p0, p1, c, "basic-filter");
            bool csFilter    = RobustPassesThrough.PassesThroughFilter(p0, p1, c);
            bool refHalfOpen = RunPassesThroughHalfOpen(p0, p1, c, "basic-halfopen");
            bool csHalfOpen  = RobustPassesThrough.PassesThroughHalfOpen(p0, p1, c);

            Assert.That(csFilter,   Is.EqualTo(refFilter),
                "PassesThroughFilter disagrees with oracle");
            Assert.That(csHalfOpen, Is.EqualTo(refHalfOpen),
                "PassesThroughHalfOpen disagrees with oracle");
        }

        // ---------------------------------------------------------------------
        // Group 2: option-layer pin -- the formally-proved bracket
        //   b64_passes_through_hot_pixel_halfopen_implies_closed
        // (NetTopologySuite.Proofs PR #22):
        //
        //   HALFOPEN TRUE  =>  FILTER TRUE          (i.e. !(F=false && H=true))
        //
        // This is the snap-round analogue of OptionLayer_PointFiltered_AgreesWith*
        // in the intersect channel.
        // ---------------------------------------------------------------------
        [TestCase(-1.0, -1.0,  1.0,  1.0,  0.0,  0.0,
            TestName = "OptionLayer_HalfOpenImpliesFilter_Interior")]
        [TestCase( 5.0,  5.0,  6.0,  6.0,  0.0,  0.0,
            TestName = "OptionLayer_HalfOpenImpliesFilter_Outside")]
        [TestCase( 0.5,  0.0,  0.5,  1.0,  0.0,  0.0,
            TestName = "OptionLayer_HalfOpenImpliesFilter_Boundary")]
        public void OptionLayer_HalfOpenImpliesFilter(
            double x0, double y0, double x1, double y1, double cx, double cy)
        {
            var p0 = new BPoint(x0, y0);
            var p1 = new BPoint(x1, y1);
            var c  = new BPoint(cx, cy);

            bool refFilter   = RunPassesThroughFilter(p0, p1, c, "option-filter");
            bool refHalfOpen = RunPassesThroughHalfOpen(p0, p1, c, "option-halfopen");

            if (refHalfOpen)
            {
                Assert.That(refFilter, Is.True,
                    "option-layer pin violated: HALFOPEN=TRUE but FILTER=FALSE");
            }
        }

        // ---------------------------------------------------------------------
        // Group 3: pixel boundary divergence -- segments that lie exactly on
        // the closed pixel boundary.  The Hobby half-open pixel excludes the
        // upper x / upper y boundary (matching `in_hot_pixel`'s strict
        // `< xhi`); the closed filter accepts.  These cases are the load-
        // bearing distinction between the two predicates and must remain
        // observable end-to-end.
        //
        // Regression-locks runtime boundary behaviour.  Formal witness
        // (`b64_passes_through_hot_pixel_boundary_diverges`) is deferred
        // pending `binary_normalize_correct` bridge lemmas (Proofs PR #22 §8).
        // This test is empirical until that deferral closes -- hence the
        // `_Empirical` suffix on the test name.
        // ---------------------------------------------------------------------
        [TestCase( 0.5, -1.0,  0.5,  1.0,  0.0,  0.0,  true,  false,
            TestName = "PixelBoundary_ClosedVsHalfOpen_Diverge_Empirical_UpperX")]
        [TestCase(-1.0,  0.5,  1.0,  0.5,  0.0,  0.0,  true,  false,
            TestName = "PixelBoundary_ClosedVsHalfOpen_Diverge_Empirical_UpperY")]
        [TestCase(-0.5, -1.0, -0.5,  1.0,  0.0,  0.0,  true,  true,
            TestName = "PixelBoundary_ClosedVsHalfOpen_Diverge_Empirical_LowerX_BothAccept")]
        [TestCase(-0.25, -0.25,  0.25,  0.25,  0.0,  0.0,  true,  true,
            TestName = "PixelBoundary_ClosedVsHalfOpen_Diverge_Empirical_StrictInterior_Control")]
        public void PixelBoundary_ClosedVsHalfOpen_Diverge_Empirical(
            double x0, double y0, double x1, double y1, double cx, double cy,
            bool expectedFilter, bool expectedHalfOpen)
        {
            var p0 = new BPoint(x0, y0);
            var p1 = new BPoint(x1, y1);
            var c  = new BPoint(cx, cy);

            bool refFilter   = RunPassesThroughFilter(p0, p1, c, "boundary-filter");
            bool refHalfOpen = RunPassesThroughHalfOpen(p0, p1, c, "boundary-halfopen");
            bool csFilter    = RobustPassesThrough.PassesThroughFilter(p0, p1, c);
            bool csHalfOpen  = RobustPassesThrough.PassesThroughHalfOpen(p0, p1, c);

            Assert.That(refFilter,   Is.EqualTo(expectedFilter),
                "oracle FILTER mismatch with empirical expectation");
            Assert.That(refHalfOpen, Is.EqualTo(expectedHalfOpen),
                "oracle HALFOPEN mismatch with empirical expectation");
            Assert.That(csFilter,    Is.EqualTo(refFilter),
                "C# PassesThroughFilter disagrees with oracle on boundary");
            Assert.That(csHalfOpen,  Is.EqualTo(refHalfOpen),
                "C# PassesThroughHalfOpen disagrees with oracle on boundary");
        }

        // ---------------------------------------------------------------------
        // Group 4: adversarial -- categories matching the intersect channel's
        // adversarial corpus.  Each case exercises the C# port and the oracle
        // and asserts bit-equality.  No hand-rolled "expected" oracle output;
        // the contract is "C# agrees with oracle", not "both agree with my
        // intuition".
        // ---------------------------------------------------------------------
        [TestCase(-1E-300, -1E-300, 1E-300, 1E-300, 0.0, 0.0,
            TestName = "Adversarial_NearZeroPixel_TinyDiagonal")]
        [TestCase(-1E10, -1E10, 1E10, 1E10, 0.0, 0.0,
            TestName = "Adversarial_HugeMagnitude_DiagonalThroughOrigin")]
        [TestCase(1E15, 1E-15, -1E15, -1E-15, 0.0, 0.0,
            TestName = "Adversarial_MixedScale_DiagonalThroughOrigin")]
        [TestCase(5E-324, 0.0, -5E-324, 0.0, 0.0, 0.0,
            TestName = "Adversarial_Subnormal_DegenerateXAxis")]
        [TestCase(0.5, 0.5, -0.5, -0.5, 0.0, 0.0,
            TestName = "Adversarial_DiagonalCorners_BothBoundary")]
        [TestCase(-0.5, 0.5, 0.5, -0.5, 0.0, 0.0,
            TestName = "Adversarial_AntiDiagonalCorners_BothBoundary")]
        [TestCase(0.5, 0.5000000000000001, -0.5, -0.5, 0.0, 0.0,
            TestName = "Adversarial_NearDiagonal_OneUlpOff")]
        [TestCase(2.0, 2.0, -2.0, -2.0, 100.0, 100.0,
            TestName = "Adversarial_PixelFarFromOrigin_Misses")]
        public void Adversarial_BitEqual(
            double x0, double y0, double x1, double y1, double cx, double cy)
        {
            var p0 = new BPoint(x0, y0);
            var p1 = new BPoint(x1, y1);
            var c  = new BPoint(cx, cy);

            bool refFilter   = RunPassesThroughFilter(p0, p1, c, "adversarial-filter");
            bool csFilter    = RobustPassesThrough.PassesThroughFilter(p0, p1, c);
            bool refHalfOpen = RunPassesThroughHalfOpen(p0, p1, c, "adversarial-halfopen");
            bool csHalfOpen  = RobustPassesThrough.PassesThroughHalfOpen(p0, p1, c);

            Assert.That(csFilter,   Is.EqualTo(refFilter),
                "PassesThroughFilter disagrees with oracle on adversarial input");
            Assert.That(csHalfOpen, Is.EqualTo(refHalfOpen),
                "PassesThroughHalfOpen disagrees with oracle on adversarial input");
        }

        // ---------------------------------------------------------------------
        // Adversarial expansion (Phase 2 follow-up).  Four sub-categories stress-
        // testing the in_snap_region geometry and the Liang-Barsky t-interval at
        // its boundaries: near-vertical segments (NV), pixel edge / corner
        // traversal (ET), grid alignment + snap interactions (GA), and the
        // b64_coord_int_safe coordinate-scale boundary (CS).
        //
        // Every fixture is asserted as `C# port == oracle` -- the oracle is the
        // formal ground truth.  Captured (FILTER, HALFOPEN) values shown in each
        // TestCase comment were probed against oracle_bin
        // (NetTopologySuite.Proofs main @ 01d6381) before the asserts landed,
        // matching the discipline that the geometric intuition is a sanity
        // check, never the assertion source.
        // ---------------------------------------------------------------------

        // ----- Category 1: Near-vertical segments (NV-1 .. NV-3) -------------
        // 2.220446049250313e-16  ==  exactly 2^-52  (ULP at 1.0)
        // 1.0000000000000002     ==  exactly 1.0 + 2^-52  (next representable double after 1.0)
        [TestCase(0.0, -1.0, 2.220446049250313e-16, 1.0, 0.0, 0.0,
            TestName = "NV1_NearVertical_OneUlpLean_PassesThroughOriginPixel")]
            // observed: FILTER=TRUE, HALFOPEN=TRUE -- the 1-ULP eastward lean
            // doesn't budge the crossing off the central x-axis.
        [TestCase(1.0, -1.0, 1.0000000000000002, 1.0, 0.0, 0.0,
            TestName = "NV2_NearVertical_OneUlpLean_MissesOriginPixelByOne")]
            // observed: FILTER=FALSE, HALFOPEN=FALSE -- vertical near x=1, pixel
            // at origin; nothing the C# port could disagree with the oracle on.
        [TestCase(0.5, -1.0, 0.5, 1.0, 0.0, 0.0,
            TestName = "NV3_NearVertical_ExactlyOnUpperXBoundary")]
            // observed: FILTER=TRUE, HALFOPEN=FALSE -- the classic boundary
            // divergence.  Geometrically the same shape as
            // PixelBoundary_..._UpperX above; kept here as a category-completing
            // probe under the near-vertical heading.

        // ----- Category 2: Pixel edge traversal (ET-1 .. ET-6) ---------------
        [TestCase( 0.0, -1.0,  0.0,  1.0,  0.0,  0.0,
            TestName = "ET1_PixelEdge_VerticalCrossingThroughCentre")]
            // observed: FILTER=TRUE, HALFOPEN=TRUE.
        [TestCase(-1.0,  0.0,  1.0,  0.0,  0.0,  0.0,
            TestName = "ET2_PixelEdge_HorizontalCrossingThroughCentre")]
            // observed: FILTER=TRUE, HALFOPEN=TRUE.
        [TestCase( 0.0, -1.0,  1.0,  0.0,  0.0,  0.0,
            TestName = "ET3_PixelEdge_BottomRightCornerTouch")]
            // observed: FILTER=TRUE, HALFOPEN=FALSE -- the segment touches the
            // closed pixel only at the single point (0.5, -0.5); the half-open
            // midpoint witness xmid<xhi fails since xmid==xhi==0.5 at the
            // single-point t-interval.
        [TestCase(-1.0,  0.0,  0.0,  1.0,  0.0,  0.0,
            TestName = "ET4_PixelEdge_TopLeftCornerTouch")]
            // observed: FILTER=TRUE, HALFOPEN=FALSE -- mirror of ET-3 on the
            // (ymid<yhi) axis.
        [TestCase(-1.0, -1.0,  0.0,  0.0,  0.5,  0.5,
            TestName = "ET5_PixelEdge_BottomLeftCornerAsEndpoint")]
            // observed: FILTER=TRUE, HALFOPEN=TRUE -- the bottom-left corner
            // (xlo, ylo) is included in both the closed and the half-open
            // pixel ([xlo, xhi) x [ylo, yhi) has lo inclusive).
        [TestCase( 0.0,  0.0,  1.0,  1.0,  0.5,  0.5,
            TestName = "ET6_PixelEdge_DiagonalThroughBothOppositeCorners")]
            // observed: FILTER=TRUE, HALFOPEN=TRUE -- both endpoints sit on
            // opposite corners and the segment crosses the entire interior;
            // tmid==0.5 lands the midpoint witness at the pixel centre well
            // inside the half-open box.

        // ----- Category 3: Grid alignment (GA-1 .. GA-4) ---------------------
        [TestCase( 0.0,  0.0,  2.0,  0.0,  1.0,  0.0,
            TestName = "GA1_GridAligned_IntegerEndpoints_AlongPixelCentreRow")]
            // observed: FILTER=TRUE, HALFOPEN=TRUE -- endpoints already on the
            // integer grid, snap is a no-op.
        [TestCase( 0.3,  0.3,  0.7,  0.7,  0.0,  0.0,
            TestName = "GA2_OffGrid_BothEndpointsInsidePixel")]
            // observed: FILTER=TRUE, HALFOPEN=TRUE -- short diagonal that
            // stays clear of every pixel boundary.
        [TestCase( 0.0,  0.0,  1.3,  0.7,  1.0,  0.0,
            TestName = "GA3_SnapInducedDivergence_OneEndpointOffGrid")]
            // observed: FILTER=TRUE, HALFOPEN=FALSE.  Original segment passes
            // through the pixel at (1,0) cleanly.  snap((1.3, 0.7)) = (1, 1),
            // and the snapped segment (0,0)->(1,1) only touches pixel (1,0) at
            // its top-right corner (0.5, 0.5) -- the half-open ymid<yhi witness
            // fails on the snapped half of the conjunction.  This is the
            // load-bearing case for the half-open mode's snap-leg behaviour.
        [TestCase( 0.4,  0.4,  1.6,  1.6,  1.0,  1.0,
            TestName = "GA4_OffGrid_SymmetricSnapPreservesTraversal")]
            // observed: FILTER=TRUE, HALFOPEN=TRUE -- both endpoints snap to
            // (0,0) and (2,2) respectively, the snapped diagonal still crosses
            // pixel (1,1) through its interior.

        // ----- Category 4: Coordinate scale boundary (CS-1 .. CS-2) ----------
        [TestCase(1048576.0,    0.0, 1048577.0,    1.0, 1048576.0, 0.5,
            TestName = "CS1_CoordScale_TwoToTwenty_WithinSafeRange")]
            // 2^20 = 1048576.  Comfortably inside b64_coord_int_safe.
            // observed: FILTER=TRUE, HALFOPEN=TRUE.
        [TestCase(33554432.0,   0.0, 33554432.0,   1.0, 33554432.0, 0.5,
            TestName = "CS2_CoordScale_TwoToTwentyFive_AtCoordIntSafeBoundary")]
            // 2^25 = 33554432, the exact b64_coord_int_safe precondition
            // boundary.  Vertical segment AT the pixel centre.
            // observed: FILTER=TRUE, HALFOPEN=TRUE -- the predicate computes
            // cleanly at the precondition boundary.  No overflow.
        public void AdversarialExpansion_BitEqual(
            double x0, double y0, double x1, double y1, double cx, double cy)
        {
            var p0 = new BPoint(x0, y0);
            var p1 = new BPoint(x1, y1);
            var c  = new BPoint(cx, cy);

            bool refFilter   = RunPassesThroughFilter(p0, p1, c, "expansion-filter");
            bool csFilter    = RobustPassesThrough.PassesThroughFilter(p0, p1, c);
            bool refHalfOpen = RunPassesThroughHalfOpen(p0, p1, c, "expansion-halfopen");
            bool csHalfOpen  = RobustPassesThrough.PassesThroughHalfOpen(p0, p1, c);

            Assert.That(csFilter,   Is.EqualTo(refFilter),
                "PassesThroughFilter disagrees with oracle on adversarial-expansion input");
            Assert.That(csHalfOpen, Is.EqualTo(refHalfOpen),
                "PassesThroughHalfOpen disagrees with oracle on adversarial-expansion input");
        }

        // CS-3: outside the b64_coord_int_safe (2^25) precondition.  The
        // corpus's soundness lemmas don't apply here; the oracle's runtime
        // answer is informational only and may shift in future Proofs builds.
        // Kept as an [Explicit] regression hook -- runs only when the user
        // names it on the command line -- so a behaviour change at this
        // magnitude surfaces during a deliberate audit, not in a normal CI run.
        [Test, Explicit("Beyond b64_coord_int_safe (2^25). Oracle behaviour outside the formal precondition is informational only; this test does not pin a contract.")]
        public void CS3_CoordScale_TwoToTwentySix_BeyondPrecondition_Informational_BitEqual()
        {
            double scale = Math.Pow(2, 26);          // 67108864.0
            var p0 = new BPoint(scale, 0.0);
            var p1 = new BPoint(scale, 1.0);
            var c  = new BPoint(scale, 0.5);

            bool refFilter   = RunPassesThroughFilter(p0, p1, c, "cs3-filter");
            bool csFilter    = RobustPassesThrough.PassesThroughFilter(p0, p1, c);
            bool refHalfOpen = RunPassesThroughHalfOpen(p0, p1, c, "cs3-halfopen");
            bool csHalfOpen  = RobustPassesThrough.PassesThroughHalfOpen(p0, p1, c);

            // Bit-equality is still the contract -- the C# port mirrors the
            // OCaml driver line-by-line, so they should agree even outside the
            // precondition range.  observed (Proofs main @ 01d6381):
            //   FILTER=TRUE, HALFOPEN=TRUE
            Assert.That(csFilter,   Is.EqualTo(refFilter),
                "PassesThroughFilter disagrees with oracle at 2^26 (outside b64_coord_int_safe)");
            Assert.That(csHalfOpen, Is.EqualTo(refHalfOpen),
                "PassesThroughHalfOpen disagrees with oracle at 2^26 (outside b64_coord_int_safe)");
        }

        // ---------------------------------------------------------------------
        // Helpers.
        // ---------------------------------------------------------------------
        private static bool ParseBool(string s, string ctx)
        {
            if (s == "TRUE")  return true;
            if (s == "FALSE") return false;
            throw new FormatException(
                "Unexpected boolean reply from oracle (" + ctx + "): " + s);
        }

        // Hex-float-friendly formatter; matches the helper duplicated across the
        // other RocqRef test classes.
        private static string Fmt(double x)
        {
            if (double.IsNaN(x))              return "nan";
            if (double.IsPositiveInfinity(x)) return "infinity";
            if (double.IsNegativeInfinity(x)) return "neg_infinity";
            return x.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
