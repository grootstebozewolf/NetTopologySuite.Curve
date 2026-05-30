// =============================================================================
// NetTopologySuite.Curve.Robust.Arc.InCircleRocqRefTests
// -----------------------------------------------------------------------------
// Differential tests against the RocqRefRunner -- the Coq-extracted reference
// binary built from NetTopologySuite.Proofs/oracle/.  The mode shipped in
// the Phase-4 (Workstream B) artifact:
//
//   INCIRCLE_SIGN <-> inCircle_R   (theories/ArcOrient.v:88)
//                     hand-rolled OCaml mirror in oracle/driver.ml
//
// Stdin: one mode line, then four "<x> <y>" lines for A, B, C, P.
// Stdout: "<sign> <det_hex>" where sign in {POS, NEG, ZERO, NAN}.
// The oracle is persistent (a single oracle_bin process serves the whole
// fixture); a [OneTimeSetUp] starts it and [OneTimeTearDown] closes stdin
// to let the OCaml End_of_file path exit cleanly.
//
// Every fixture asserts BOTH the sign equality AND the bit-exact det value
// against the oracle.  The C# port is a line-by-line transliteration of
// the OCaml hand-rolled mirror (see InCircle.cs), so bit-equality is the
// formal contract -- not merely sign-equality.
//
// "Observed" values in each TestCase comment were captured by direct
// probe against oracle_bin built from NetTopologySuite.Proofs main
// @ 1ca583c5 (the Phase-3+4 artifact run 26678597803, artifact 7306659452)
// before the asserts landed.
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
    public class InCircleRocqRefTests
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
        // Group 1: canonical CCW triangle (1,0), (0,1), (-1,0) circumscribed
        // by the unit circle.  Each P probe lives in a distinct branch of
        // the predicate.
        // ---------------------------------------------------------------------
        [TestCase( 1, 0,  0, 1,  -1, 0,   0,    0,    IncircleSign.Pos,
            TestName = "CCW_UnitTri_P_AtCenter_Inside")]
            // observed: POS 0x1p+1 = 2.0
        [TestCase( 1, 0,  0, 1,  -1, 0,   2,    2,    IncircleSign.Neg,
            TestName = "CCW_UnitTri_P_FarOutside")]
            // observed: NEG -0x1.cp+3 = -14.0
        [TestCase( 1, 0,  0, 1,  -1, 0,   0,   -1,    IncircleSign.Zero,
            TestName = "CCW_UnitTri_P_OnCircle_BottomVertexPosition")]
            // observed: ZERO 0x0p+0
        [TestCase( 1, 0,  0, 1,  -1, 0,   1,    0,    IncircleSign.Zero,
            TestName = "CCW_UnitTri_P_OnCircle_CoincidesWithA")]
            // observed: ZERO 0x0p+0.  Degenerate: P coincides with A; the
            // determinant has a zero row, so the formal answer is exactly 0.
        [TestCase( 1, 0,  0, 1,  -1, 0,   0.5,  0,    IncircleSign.Pos,
            TestName = "CCW_UnitTri_P_JustInside_HalfRadius")]
            // observed: POS 0x1.8p+0 = 1.5
        [TestCase( 1, 0,  0, 1,  -1, 0,   1.01, 0,    IncircleSign.Neg,
            TestName = "CCW_UnitTri_P_JustOutside_OneHundredthOver")]
            // observed: NEG -0x1.495182a9930c2p-5 ~= -0.0402
        public void CCW_UnitTriangle_BitEqual(
            double ax, double ay, double bx, double by, double cx, double cy,
            double px, double py, IncircleSign expectedSign)
        {
            AssertBitEqual(
                new BPoint(ax, ay), new BPoint(bx, by),
                new BPoint(cx, cy), new BPoint(px, py),
                expectedSign);
        }

        // ---------------------------------------------------------------------
        // Group 2: CW triangle -- the sign of inCircle_R flips relative to
        // the geometric "inside / outside" reading.  Same vertex positions
        // as the CCW group but A and C swapped.
        // ---------------------------------------------------------------------
        [TestCase(-1, 0,  0, 1,   1, 0,   0,    0,    IncircleSign.Neg,
            TestName = "CW_UnitTri_P_AtCenter_SignFlipped")]
            // observed: NEG -0x1p+1 = -2.0
        [TestCase(-1, 0,  0, 1,   1, 0,   2,    2,    IncircleSign.Pos,
            TestName = "CW_UnitTri_P_FarOutside_SignFlipped")]
            // observed: POS 0x1.cp+3 = 14.0
        public void CW_UnitTriangle_BitEqual(
            double ax, double ay, double bx, double by, double cx, double cy,
            double px, double py, IncircleSign expectedSign)
        {
            AssertBitEqual(
                new BPoint(ax, ay), new BPoint(bx, by),
                new BPoint(cx, cy), new BPoint(px, py),
                expectedSign);
        }

        // ---------------------------------------------------------------------
        // Group 3: degenerate (A, B, C) collinear.  The geometric "inCircle"
        // reading is undefined (no circumscribed circle), but the determinant
        // formula remains total -- it computes a different geometric quantity
        // (proportional to a signed area when ABC are colinear).  Bit-equal
        // contract still holds; geometric meaning is informational only.
        // ---------------------------------------------------------------------
        [TestCase(0, 0,  1, 0,  2, 0,   0,    1,    IncircleSign.Pos,
            TestName = "Collinear_ABC_OnXAxis_P_AboveLine")]
            // observed: POS 0x1p+1 = 2.0.  Not "inside a circle"; the formula
            // returns a finite non-zero value because P is off the degenerate
            // ABC line.  Documents the predicate's behaviour outside its
            // intended domain.
        public void Collinear_ABC_BitEqual(
            double ax, double ay, double bx, double by, double cx, double cy,
            double px, double py, IncircleSign expectedSign)
        {
            AssertBitEqual(
                new BPoint(ax, ay), new BPoint(bx, by),
                new BPoint(cx, cy), new BPoint(px, py),
                expectedSign);
        }

        // ---------------------------------------------------------------------
        // Group 4: adversarial -- NaN, huge magnitudes, subnormals.
        // ---------------------------------------------------------------------
        [Test]
        public void Adversarial_Nan_InAnyCoord_PropagatesToNan(
            [Values(0, 1, 2, 3)] int point,
            [Values(0, 1)]       int coord)
        {
            var pts = new[]
            {
                new BPoint( 1, 0),
                new BPoint( 0, 1),
                new BPoint(-1, 0),
                new BPoint( 0, 0),
            };
            double nx = coord == 0 ? double.NaN : pts[point].X;
            double ny = coord == 1 ? double.NaN : pts[point].Y;
            pts[point] = new BPoint(nx, ny);

            var (refSign, refDetBits) = RunRocqRef(pts[0], pts[1], pts[2], pts[3]);
            var csSign = InCircle.Sign(pts[0], pts[1], pts[2], pts[3]);
            double csDet = InCircle.Determinant(pts[0], pts[1], pts[2], pts[3]);

            Assert.That(refSign, Is.EqualTo(IncircleSign.Nan),
                "oracle should return NAN with a NaN input");
            Assert.That(csSign, Is.EqualTo(refSign),
                "C# InCircle.Sign disagrees with oracle on NaN input");
            // For NaN we don't compare det bits because there are many NaN
            // bit-patterns; the IsNaN classification is the contract.
            Assert.That(double.IsNaN(csDet), Is.True,
                "C# InCircle.Determinant should be NaN when oracle returns NAN");
            // The oracle outputs hex-NaN ("nan" or "-nan"); refDetBits's NaN-
            // ness is what matters, not the specific payload.
            Assert.That(double.IsNaN(BitConverter.Int64BitsToDouble(refDetBits)),
                        Is.True,
                "oracle det column should also be NaN");
        }

        [Test]
        public void Adversarial_HugeMagnitude_OverflowsToInfinityWithSign()
        {
            // observed: A=(1e100,0) B=(0,1e100) C=(-1e100,0) P=(0,0)
            //   -> POS infinity (overflows but sign preserved).
            var a = new BPoint( 1e100, 0);
            var b = new BPoint( 0, 1e100);
            var c = new BPoint(-1e100, 0);
            var p = new BPoint( 0, 0);

            var (refSign, refDetBits) = RunRocqRef(a, b, c, p);
            var csSign = InCircle.Sign(a, b, c, p);
            double csDet = InCircle.Determinant(a, b, c, p);

            Assert.That(csSign, Is.EqualTo(refSign),
                "C# InCircle.Sign disagrees with oracle on huge-magnitude input");
            Assert.That(BitConverter.DoubleToInt64Bits(csDet), Is.EqualTo(refDetBits),
                "C# InCircle.Determinant bits diverge from oracle on huge-magnitude input");
        }

        [Test]
        public void Adversarial_HugeMagnitude_InfMinusInfCancelsToNan()
        {
            // observed: A=(1e100,0) B=(0,1e100) C=(-1e100,0) P=(2e100,2e100)
            //   -> NAN -nan  (an inf - inf cancellation inside the cofactor).
            var a = new BPoint( 1e100, 0);
            var b = new BPoint( 0, 1e100);
            var c = new BPoint(-1e100, 0);
            var p = new BPoint( 2e100, 2e100);

            var (refSign, _) = RunRocqRef(a, b, c, p);
            var csSign = InCircle.Sign(a, b, c, p);
            double csDet = InCircle.Determinant(a, b, c, p);

            Assert.That(refSign, Is.EqualTo(IncircleSign.Nan),
                "oracle should NAN when an intermediate cancels inf - inf");
            Assert.That(csSign, Is.EqualTo(refSign),
                "C# InCircle.Sign disagrees with oracle on inf-inf cancellation");
            Assert.That(double.IsNaN(csDet), Is.True,
                "C# Determinant should be NaN on inf-inf cancellation");
        }

        [Test]
        public void Adversarial_Subnormal_UnderflowsToZero()
        {
            // observed: A=(1e-300,0) B=(0,1e-300) C=(-1e-300,0) P=(0,0)
            //   -> ZERO 0x0p+0  (the squared norms underflow to zero, the
            //   det collapses to exact zero).  The bit contract still holds:
            //   C# port must mirror the same underflow behaviour.
            var a = new BPoint( 1e-300, 0);
            var b = new BPoint( 0, 1e-300);
            var c = new BPoint(-1e-300, 0);
            var p = new BPoint( 0, 0);

            AssertBitEqual(a, b, c, p, IncircleSign.Zero);
        }

        // ---------------------------------------------------------------------
        // Helpers.
        // ---------------------------------------------------------------------

        private void AssertBitEqual(
            BPoint a, BPoint b, BPoint c, BPoint p, IncircleSign expectedSign)
        {
            var (refSign, refDetBits) = RunRocqRef(a, b, c, p);
            var csSign = InCircle.Sign(a, b, c, p);
            double csDet = InCircle.Determinant(a, b, c, p);
            long csBits = BitConverter.DoubleToInt64Bits(csDet);

            Assert.That(refSign, Is.EqualTo(expectedSign),
                "oracle sign disagrees with the expected reference value");
            Assert.That(csSign, Is.EqualTo(refSign),
                "C# InCircle.Sign disagrees with oracle");
            Assert.That(csBits, Is.EqualTo(refDetBits),
                "C# InCircle.Determinant bits diverge from oracle: " +
                "C#=0x" + csBits.ToString("X16") + " oracle=0x" + refDetBits.ToString("X16"));
        }

        private (IncircleSign sign, long detBits) RunRocqRef(
            BPoint a, BPoint b, BPoint c, BPoint p)
        {
            string line;
            lock (_ioLock)
            {
                _stdin.WriteLine("INCIRCLE_SIGN");
                _stdin.WriteLine(Fmt(a.X) + " " + Fmt(a.Y));
                _stdin.WriteLine(Fmt(b.X) + " " + Fmt(b.Y));
                _stdin.WriteLine(Fmt(c.X) + " " + Fmt(c.Y));
                _stdin.WriteLine(Fmt(p.X) + " " + Fmt(p.Y));
                _stdin.Flush();
                line = _stdout.ReadLine();
            }
            if (line == null)
            {
                Assert.Fail("RocqRefRunner returned no output for INCIRCLE_SIGN");
            }
            var parts = line!.Trim().Split(' ');
            if (parts.Length != 2)
            {
                Assert.Fail("malformed INCIRCLE_SIGN reply: '" + line + "'");
            }
            IncircleSign sign;
            switch (parts[0])
            {
                case "POS":  sign = IncircleSign.Pos;  break;
                case "NEG":  sign = IncircleSign.Neg;  break;
                case "ZERO": sign = IncircleSign.Zero; break;
                case "NAN":  sign = IncircleSign.Nan;  break;
                default:
                    Assert.Fail("unknown INCIRCLE_SIGN sign token: " + parts[0]);
                    return (IncircleSign.Nan, 0L);
            }
            double det = ParseOcamlFloat(parts[1]);
            return (sign, BitConverter.DoubleToInt64Bits(det));
        }

        // Hex-float-friendly formatter; matches the helper in the other
        // RocqRef test classes.
        private static string Fmt(double x)
        {
            if (double.IsNaN(x))              return "nan";
            if (double.IsPositiveInfinity(x)) return "infinity";
            if (double.IsNegativeInfinity(x)) return "neg_infinity";
            return x.ToString("R", CultureInfo.InvariantCulture);
        }

        // OCaml "%h" hex-float parser; mirrors the helper in
        // RobustLineIntersectorRocqRefTests.  Duplicated to keep this
        // fixture self-contained (matching the per-class convention used
        // by the existing Phase 1+2 RocqRef tests).
        private static double ParseOcamlFloat(string s)
        {
            if (s == "nan" || s == "-nan" || s == "Nan" || s == "NaN") return double.NaN;
            if (s == "inf" || s == "infinity" || s == "Infinity") return double.PositiveInfinity;
            if (s == "-inf" || s == "neg_infinity" || s == "-Infinity") return double.NegativeInfinity;

            int i = 0;
            bool neg = false;
            if (s[0] == '-') { neg = true; i = 1; }
            else if (s[0] == '+') { i = 1; }

            if (i + 1 >= s.Length || s[i] != '0' || s[i + 1] != 'x')
            {
                return double.Parse(s, CultureInfo.InvariantCulture);
            }
            i += 2;
            int pIdx = s.IndexOf('p', i);
            if (pIdx < 0)
            {
                throw new FormatException("Bad hex float: " + s);
            }
            string mantissa = s.Substring(i, pIdx - i);
            int exponent = int.Parse(s.Substring(pIdx + 1), CultureInfo.InvariantCulture);
            int dotIdx = mantissa.IndexOf('.');
            string ipart = dotIdx >= 0 ? mantissa.Substring(0, dotIdx) : mantissa;
            string fpart = dotIdx >= 0 ? mantissa.Substring(dotIdx + 1) : "";
            double m = 0.0;
            if (ipart.Length > 0)
            {
                m = (double)Convert.ToInt64(ipart, 16);
            }
            if (fpart.Length > 0)
            {
                long fval = Convert.ToInt64(fpart, 16);
                m += fval / Math.Pow(16, fpart.Length);
            }
            m *= Math.Pow(2, exponent);
            return neg ? -m : m;
        }
    }
}
