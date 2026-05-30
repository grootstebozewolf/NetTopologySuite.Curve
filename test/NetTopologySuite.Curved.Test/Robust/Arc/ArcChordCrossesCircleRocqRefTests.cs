// =============================================================================
// NetTopologySuite.Curve.Robust.Arc.ArcChordCrossesCircleRocqRefTests
// -----------------------------------------------------------------------------
// Differential tests against the RocqRefRunner -- the Coq-extracted reference
// binary built from NetTopologySuite.Proofs/oracle/.  The mode shipped in
// the Phase-4 (Workstream B) artifact:
//
//   ARC_CHORD_CROSSES_CIRCLE <-> chord_crosses_arc_circle
//                                  (theories/ArcIntersect.v:129)
//     soundness: chord_crosses_arc_circle_implies_circle_intersection
//                  (theories/ArcIntersectIVT.v)
//
// Stdin: one mode line, then five "<x> <y>" lines (arc_start, arc_mid,
// arc_end, chord_P, chord_Q).  Stdout: "TRUE" or "FALSE".  Persistent
// oracle process owned by the fixture.
//
// Observed reply values for each TestCase comment captured by probe
// against oracle_bin built from NetTopologySuite.Proofs main @ 1ca583c5
// (Phase-3+4 artifact run 26678597803, artifact 7306659452).
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
    public class ArcChordCrossesCircleRocqRefTests
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
        // Group 1: unit-circle arc -- arc_start=(1,0), arc_mid=(0,1),
        // arc_end=(-1,0); CCW; circumscribed circle is the unit circle.
        // Each chord probes a distinct branch of the sufficient condition.
        // ---------------------------------------------------------------------
        [TestCase( 1, 0,  0, 1,  -1, 0,    0, 0,   2, 0,  true,
            TestName = "Unit_CCW_Chord_InsideToOutside_Crosses")]
            // observed: TRUE.  sp > 0 (P at centre), sq < 0 (Q at (2,0)
            // outside), product < 0 -> TRUE.
        [TestCase( 1, 0,  0, 1,  -1, 0,   -2, 0,   2, 0,  false,
            TestName = "Unit_CCW_Chord_OutsideToOutside_CrossesButSuffCondFails")]
            // observed: FALSE.  Both endpoints outside the circle but
            // chord geometrically crosses twice -- the sufficient-condition
            // form (sign product) cannot detect this.  Documents the
            // soundness-not-completeness contract.
        [TestCase( 1, 0,  0, 1,  -1, 0,   0.5, 0,  -0.5, 0,  false,
            TestName = "Unit_CCW_Chord_InsideToInside_DoesNotCross")]
            // observed: FALSE.  Both endpoints inside.
        [TestCase( 1, 0,  0, 1,  -1, 0,    2, 2,   3, 3,    false,
            TestName = "Unit_CCW_Chord_BothFarOutside_DoesNotCross")]
            // observed: FALSE.  Both endpoints same side.
        [TestCase( 1, 0,  0, 1,  -1, 0,    0, 0,   0, 2,    true,
            TestName = "Unit_CCW_Chord_InsideToOutsideVertical_Crosses")]
            // observed: TRUE.  Vertical chord through centre into outside.

        // Edge cases: endpoint exactly on the circle => sp or sq is zero
        // => product is zero, predicate is FALSE.
        [TestCase( 1, 0,  0, 1,  -1, 0,    1, 0,   2, 0,    false,
            TestName = "Unit_CCW_Chord_PEndpointOnCircle_FalseFromZeroProduct")]
            // observed: FALSE.  sp=0 (P=(1,0) coincides with arc_start),
            // 0 * sq = 0, predicate is FALSE.  Sufficient condition does
            // not fire even though the chord trivially touches the circle.
        [TestCase( 1, 0,  0, 1,  -1, 0,    0, 1,   0, 2,    false,
            TestName = "Unit_CCW_Chord_PEndpointOnCircle_AtArcMid_FalseFromZeroProduct")]
            // observed: FALSE.  Same as above with P=(0,1) coinciding with
            // arc_mid.
        public void UnitArc_CCW_BitEqual(
            double sx, double sy, double mx, double my, double ex, double ey,
            double px, double py, double qx, double qy, bool expected)
        {
            AssertBitEqual(
                new BPoint(sx, sy), new BPoint(mx, my), new BPoint(ex, ey),
                new BPoint(px, py), new BPoint(qx, qy),
                expected);
        }

        // ---------------------------------------------------------------------
        // Group 2: CW arc (arc_start / arc_end swapped).  inCircle_R flips
        // sign on CW orientations; sp*sq < 0 is invariant under both flipping
        // (negative * negative = positive) so the predicate is orientation-
        // independent -- a CCW-side chord and the equivalent CW-side chord
        // produce the same answer.
        // ---------------------------------------------------------------------
        [TestCase(-1, 0,  0, 1,   1, 0,    0, 0,   2, 0,   true,
            TestName = "Unit_CW_Chord_InsideToOutside_CrossesSameAsCCW")]
            // observed: TRUE.  Sign of both sp and sq flips vs the CCW
            // case but their product's sign is invariant.
        [TestCase(-1, 0,  0, 1,   1, 0,   -2, 0,   2, 0,  false,
            TestName = "Unit_CW_Chord_OutsideToOutside_FalseSameAsCCW")]
            // observed: FALSE.  Same as the CCW equivalent.
        public void UnitArc_CW_BitEqual(
            double sx, double sy, double mx, double my, double ex, double ey,
            double px, double py, double qx, double qy, bool expected)
        {
            AssertBitEqual(
                new BPoint(sx, sy), new BPoint(mx, my), new BPoint(ex, ey),
                new BPoint(px, py), new BPoint(qx, qy),
                expected);
        }

        // ---------------------------------------------------------------------
        // Group 3: floating-point adversarial.  These document the
        // sufficient-condition predicate's behaviour under overflow,
        // underflow, and NaN propagation -- inputs the formal IVT theorem
        // does not cover.  The C# port still mirrors the oracle exactly.
        // ---------------------------------------------------------------------
        [Test]
        public void Adversarial_HugeScale_OverflowsCollapseToFalse()
        {
            // observed: FALSE.  At scale 1e100 the squared norms exceed
            // 1e200; one cofactor multiply lifts to ~1e300, the next +1
            // overflows to infinity.  sp and sq both become +infinity
            // (same sign), product is +inf, < 0.0 is false.
            var arcStart = new BPoint(1e100, 0);
            var arcMid   = new BPoint(0, 1e100);
            var arcEnd   = new BPoint(-1e100, 0);
            var p        = new BPoint(0, 0);
            var q        = new BPoint(2e100, 0);

            AssertBitEqual(arcStart, arcMid, arcEnd, p, q, false);
        }

        [Test]
        public void Adversarial_SubnormalScale_UnderflowCollapsesToFalse()
        {
            // observed: FALSE.  At scale 1e-300 the squared norms underflow
            // to zero; cofactor collapses to zero on both sides; product is
            // 0, < 0.0 is false.
            var arcStart = new BPoint( 1e-300, 0);
            var arcMid   = new BPoint( 0, 1e-300);
            var arcEnd   = new BPoint(-1e-300, 0);
            var p        = new BPoint( 0, 0);
            var q        = new BPoint( 2e-300, 0);

            AssertBitEqual(arcStart, arcMid, arcEnd, p, q, false);
        }

        [Test]
        public void Adversarial_NaN_PropagatesToFalse(
            [Values(0, 1, 2, 3, 4)] int point,
            [Values(0, 1)]          int coord)
        {
            // observed: FALSE for every (point, coord) combination.  A NaN
            // in any input propagates through one of {sp, sq} to NaN; the
            // comparison `NaN < 0.0` is false by IEEE 754 semantics.
            var pts = new[]
            {
                new BPoint( 1, 0),  // arc_start
                new BPoint( 0, 1),  // arc_mid
                new BPoint(-1, 0),  // arc_end
                new BPoint( 0, 0),  // chord_P
                new BPoint( 2, 0),  // chord_Q
            };
            double nx = coord == 0 ? double.NaN : pts[point].X;
            double ny = coord == 1 ? double.NaN : pts[point].Y;
            pts[point] = new BPoint(nx, ny);

            AssertBitEqual(pts[0], pts[1], pts[2], pts[3], pts[4], false);
        }

        // ---------------------------------------------------------------------
        // Helpers.
        // ---------------------------------------------------------------------

        private void AssertBitEqual(
            BPoint arcStart, BPoint arcMid, BPoint arcEnd,
            BPoint chordP, BPoint chordQ, bool expected)
        {
            bool refResult = RunRocqRef(arcStart, arcMid, arcEnd, chordP, chordQ);
            bool csResult = ArcChordCrossesCircle.Evaluate(
                arcStart, arcMid, arcEnd, chordP, chordQ);

            Assert.That(refResult, Is.EqualTo(expected),
                "oracle disagrees with the expected value");
            Assert.That(csResult, Is.EqualTo(refResult),
                "C# ArcChordCrossesCircle disagrees with oracle");
        }

        private bool RunRocqRef(
            BPoint arcStart, BPoint arcMid, BPoint arcEnd,
            BPoint chordP, BPoint chordQ)
        {
            string line;
            lock (_ioLock)
            {
                _stdin.WriteLine("ARC_CHORD_CROSSES_CIRCLE");
                _stdin.WriteLine(Fmt(arcStart.X) + " " + Fmt(arcStart.Y));
                _stdin.WriteLine(Fmt(arcMid.X)   + " " + Fmt(arcMid.Y));
                _stdin.WriteLine(Fmt(arcEnd.X)   + " " + Fmt(arcEnd.Y));
                _stdin.WriteLine(Fmt(chordP.X)   + " " + Fmt(chordP.Y));
                _stdin.WriteLine(Fmt(chordQ.X)   + " " + Fmt(chordQ.Y));
                _stdin.Flush();
                line = _stdout.ReadLine();
            }
            if (line == null)
            {
                Assert.Fail("RocqRefRunner returned no output for ARC_CHORD_CROSSES_CIRCLE");
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
