// =============================================================================
// NetTopologySuite.Curve.Robust.Orientation.RobustOrientationRocqRefTests
// -----------------------------------------------------------------------------
// Differential tests against the RocqRefRunner ORIENT mode.  For each
// triangle, run both the C# `RobustOrientation.Orient2d` / `Sign` and the
// Coq-extracted reference binary, and assert (a) the four-valued sign
// matches and (b) the signed-area binary64 result is bit-equal.
//
// Skipped by default; activate by pointing `ROCQ_REF_BIN` at the
// RocqRefRunner built from NetTopologySuite.Proofs/oracle/.  Same env
// var the simplifier differential tests use, since the proofs-side
// driver now dispatches on a mode line (SIMPLIFY / ORIENT) over stdin.
// =============================================================================

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using NetTopologySuite.Robust.Orientation;
using NetTopologySuite.Robust.Simplify;
using NUnit.Framework;

namespace NetTopologySuite.Test.Robust.Orientation
{
    [TestFixture]
    [Category("RocqRef")]
    public class RobustOrientationRocqRefTests
    {
        private const string RocqRefPathEnvVar = "ROCQ_REF_BIN";
        private string _rocqRefPath;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _rocqRefPath = Environment.GetEnvironmentVariable(RocqRefPathEnvVar);
            if (string.IsNullOrWhiteSpace(_rocqRefPath) || !File.Exists(_rocqRefPath))
            {
                Assert.Ignore(
                    "RocqRef differential tests skipped: set " + RocqRefPathEnvVar +
                    " to the path of the RocqRefRunner binary.  See " +
                    "NetTopologySuite.Proofs/oracle/ for build instructions.");
            }
        }

        // -----------------------------------------------------------------
        // Deterministic triangle fixtures covering the four sign branches.
        // -----------------------------------------------------------------

        [TestCase(0, 0, 1, 0, 0, 1, TestName = "CCW unit triangle")]
        [TestCase(0, 0, 0, 1, 1, 0, TestName = "CW unit triangle")]
        [TestCase(0, 0, 1, 0, 2, 0, TestName = "Collinear horizontal")]
        [TestCase(0, 0, 1, 1, 2, 2, TestName = "Collinear diagonal")]
        [TestCase(3, 4, 3, 4, 7, 9, TestName = "Degenerate base (A == A)")]
        [TestCase(3, 4, 7, 9, 3, 4, TestName = "Q coincides with P0")]
        [TestCase(3, 4, 7, 9, 7, 9, TestName = "Q coincides with P1")]
        [TestCase(0, 0, 1e10, 0, 0, 1e-10, TestName = "Mixed-scale CCW")]
        [TestCase(-1e100, -1e100, 1e100, 1e100, 0, 0, TestName = "Huge collinear")]
        public void CSharp_BitEqual_To_RocqRef(
            double x0, double y0, double x1, double y1, double x2, double y2)
        {
            var p0 = new BPoint(x0, y0);
            var p1 = new BPoint(x1, y1);
            var q  = new BPoint(x2, y2);

            AssertMatches(p0, p1, q);
        }

        // -----------------------------------------------------------------
        // Property-style fuzz: random triangles uniform in [-5, 5]^2.
        // -----------------------------------------------------------------
        [Test]
        public void Random_Triangles_BitEqual([Random(0, int.MaxValue, 80)] int seed)
        {
            var rng = new Random(seed);
            var p0 = new BPoint(rng.NextDouble() * 10 - 5, rng.NextDouble() * 10 - 5);
            var p1 = new BPoint(rng.NextDouble() * 10 - 5, rng.NextDouble() * 10 - 5);
            var q  = new BPoint(rng.NextDouble() * 10 - 5, rng.NextDouble() * 10 - 5);
            AssertMatches(p0, p1, q);
        }

        // -----------------------------------------------------------------
        // Adversarial: near-collinear configurations.  Where the naive
        // binary64 orient2d is most likely to round to the wrong sign --
        // the C# and RocqRef will agree on whatever sign their identical
        // arithmetic produces, but a divergence here would flag a real
        // implementation mismatch.
        // -----------------------------------------------------------------
        [Test]
        public void Adversarial_NearCollinear_BitEqual(
            [Values(0.0, 1e-300, 1e-100, 1e-15, 1e-12, 1e-9, 1e-3)] double dy)
        {
            var p0 = new BPoint(0.0, 0.0);
            var p1 = new BPoint(1.0, 0.0);
            var q  = new BPoint(0.5, dy);
            AssertMatches(p0, p1, q);
        }

        // -----------------------------------------------------------------
        // Adversarial: NaN coordinates at every position / coordinate.
        // -----------------------------------------------------------------
        [Test]
        public void Adversarial_NaN_BitEqual(
            [Values(0, 1, 2)] int nanPosition,
            [Values(0, 1)]    int nanCoord)
        {
            var pts = new[]
            {
                new BPoint(0, 0),
                new BPoint(1, 0),
                new BPoint(0, 1),
            };
            double nx = nanCoord == 0 ? double.NaN : pts[nanPosition].X;
            double ny = nanCoord == 1 ? double.NaN : pts[nanPosition].Y;
            pts[nanPosition] = new BPoint(nx, ny);
            AssertMatches(pts[0], pts[1], pts[2]);
        }

        // -----------------------------------------------------------------
        // Adversarial: huge magnitudes that push intermediate products
        // close to (or past) binary64 overflow.  Whatever the implementation
        // does -- return a real sign, propagate to Nan via inf-minus-inf,
        // or fall through to Uncertain because the filter bound itself
        // overflowed -- C# and RocqRef must agree on it.
        // -----------------------------------------------------------------
        [Test]
        public void Adversarial_HugeMagnitude_BitEqual(
            [Values(1e100, 1e150, 1e200, 1e250, 1e300)] double mag)
        {
            var p0 = new BPoint(-mag, -mag);
            var p1 = new BPoint( mag, -mag);
            var q  = new BPoint(  0,   mag);
            AssertMatches(p0, p1, q);
        }

        // Mixed scales: large outer coords, tiny perturbation -- a regime
        // where Shewchuk's filter typically declines and Uncertain is the
        // right answer.  C# and RocqRef must agree on which inputs land
        // in Pos / Neg / Uncertain.
        [Test]
        public void Adversarial_MixedScale_BitEqual(
            [Values(1e6, 1e10, 1e15)]         double mag,
            [Values(1e-30, 1e-15, 1e-9, 1.0)] double dy)
        {
            var p0 = new BPoint(0,    0);
            var p1 = new BPoint(mag,  0);
            var q  = new BPoint(mag, dy);
            AssertMatches(p0, p1, q);
        }

        // -----------------------------------------------------------------
        // Adversarial: integer-valued coordinates in the regime covered by
        // the Coq theorem `b64_orient_sign_filtered_sound_small_int` in
        // `theories-flocq/Orient_b64_exact.v` -- |coord| <= 2^25,
        // integer-valued.  In this regime every operation in `b64_orient2d`
        // is bit-exact: every intermediate stays within binary64's 53-bit
        // integer-exactness window, so the rounded value equals the exact
        // mathematical cross product on the nose, and the Stage A
        // filter's sign decision is sound relative to the mathematical
        // `cross_R_BP`.  These tests exercise edge cases through the
        // differential harness; a divergence between C# and RocqRef on
        // inputs the Coq proof shows are fully determined would flag an
        // implementation drift.
        //
        // The `det=1 detsum=2^51` cases reproduce the near-boundary
        // construction from `docs/soundness-strategy.md`: with operands
        // P0=(-2^25,-2^25), P1=(0,1), Q=(-1,0), the products t1=2^50 and
        // t2=2^50-1 give det=1 over a detsum near binary64's
        // integer-exactness ceiling -- the tightest non-zero outcome the
        // regime allows.
        // -----------------------------------------------------------------
        [TestCase(-33554432.0, -33554432.0,  33554432.0,  33554432.0,         0.0,  33554432.0,
            TestName = "IntegerRegime: full-range CCW")]
        [TestCase(-33554432.0, -33554432.0,  33554432.0,  33554432.0,  33554432.0,         0.0,
            TestName = "IntegerRegime: full-range CW")]
        [TestCase(-33554432.0, -33554432.0,  33554432.0,  33554432.0,         0.0,         0.0,
            TestName = "IntegerRegime: full-range collinear")]
        [TestCase(-33554432.0, -33554432.0,         0.0,         1.0,        -1.0,         0.0,
            TestName = "IntegerRegime: det=1, detsum near 2^51")]
        [TestCase(-33554432.0, -33554432.0,         0.0,        -1.0,        -1.0,         0.0,
            TestName = "IntegerRegime: det=-1, detsum near 2^51")]
        [TestCase( 33554432.0,  33554432.0,  33554432.0,  33554432.0,         7.0,        11.0,
            TestName = "IntegerRegime: degenerate base at boundary")]
        [TestCase(         0.0,         0.0,  33554432.0,  33554432.0,         0.0,         0.0,
            TestName = "IntegerRegime: Q=P0 at boundary")]
        [TestCase(         0.0,         0.0,  33554432.0,  33554432.0,  33554432.0,  33554432.0,
            TestName = "IntegerRegime: Q=P1 at boundary")]
        public void Adversarial_IntegerRegime_BoundaryCorners_BitEqual(
            double x0, double y0, double x1, double y1, double x2, double y2)
        {
            AssertMatches(new BPoint(x0, y0), new BPoint(x1, y1), new BPoint(x2, y2));
        }

        // Three exactly-collinear integer points across six scales.  Det
        // is mathematically zero; `Sign` and `SignFiltered` should both
        // return Zero, and the binary64 area should be bit-zero.  At
        // scale = 2^24, `2 * scale = 2^25` still lies in the regime.
        [Test]
        public void Adversarial_IntegerRegime_CollinearAtScale_BitEqual(
            [Values(1, 7, 100, 1000, 1000000, 16777216)] int scale)
        {
            AssertMatches(
                new BPoint(0, 0),
                new BPoint(scale, scale),
                new BPoint(2 * scale, 2 * scale));
        }

        // Random integer triangles uniformly in [-2^25, 2^25]^2.  Every
        // sample is in the proved-sound regime, so the C# implementation
        // must bit-match the Coq-extracted reference on all 80 samples.
        [Test]
        public void Adversarial_IntegerRegime_RandomInteger_BitEqual(
            [Random(0, int.MaxValue, 80)] int seed)
        {
            var rng = new Random(seed);
            const int K = 33554432; // 2^25
            AssertMatches(
                new BPoint(rng.Next(-K, K + 1), rng.Next(-K, K + 1)),
                new BPoint(rng.Next(-K, K + 1), rng.Next(-K, K + 1)),
                new BPoint(rng.Next(-K, K + 1), rng.Next(-K, K + 1)));
        }

        // -----------------------------------------------------------------
        // Helpers.
        // -----------------------------------------------------------------

        private void AssertMatches(BPoint p0, BPoint p1, BPoint q)
        {
            // Naive ORIENT mode -- 4-valued sign + signed area.
            var (sign, area) = RunRocqRefOrient(p0, p1, q);
            var csSign = RobustOrientation.Sign(p0, p1, q);
            var csArea = RobustOrientation.Orient2d(p0, p1, q);

            Assert.That(csSign, Is.EqualTo(sign),
                "naive sign mismatch: C#=" + csSign + " RocqRef=" + sign);

            long csBits = BitConverter.DoubleToInt64Bits(csArea);
            long refBits = BitConverter.DoubleToInt64Bits(area);
            Assert.That(csBits, Is.EqualTo(refBits),
                "orient2d bits: C#=0x" + csBits.ToString("X16") +
                " RocqRef=0x" + refBits.ToString("X16"));

            // ORIENT_FILTERED mode -- 5-valued sign + signed area.
            // The signed-area output should bit-equal the naive mode's
            // (same underlying b64_orient2d formula); the sign differs
            // only when the Stage A filter declines.
            var (signR, areaR) = RunRocqRefOrientFiltered(p0, p1, q);
            var csSignR = RobustOrientation.SignFiltered(p0, p1, q);

            Assert.That(csSignR, Is.EqualTo(signR),
                "filtered sign mismatch: C#=" + csSignR + " RocqRef=" + signR);

            long csBitsR = BitConverter.DoubleToInt64Bits(
                RobustOrientation.Orient2d(p0, p1, q));
            long refBitsR = BitConverter.DoubleToInt64Bits(areaR);
            Assert.That(csBitsR, Is.EqualTo(refBitsR),
                "orient2d bits (filtered mode) differ");
        }

        private (OrientSignRobust sign, double area) RunRocqRefOrientFiltered(
            BPoint p0, BPoint p1, BPoint q)
        {
            var psi = new ProcessStartInfo
            {
                FileName = _rocqRefPath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using (var proc = Process.Start(psi))
            {
                if (proc == null)
                {
                    Assert.Fail("RocqRefRunner failed to start: " + _rocqRefPath);
                    return (OrientSignRobust.Nan, double.NaN);
                }

                using (var w = proc.StandardInput)
                {
                    w.WriteLine("ORIENT_FILTERED");
                    w.WriteLine(Fmt(p0.X) + " " + Fmt(p0.Y));
                    w.WriteLine(Fmt(p1.X) + " " + Fmt(p1.Y));
                    w.WriteLine(Fmt(q.X)  + " " + Fmt(q.Y));
                }

                string line = proc.StandardOutput.ReadLine();
                proc.WaitForExit();
                if (proc.ExitCode != 0)
                {
                    string err = proc.StandardError.ReadToEnd();
                    Assert.Fail("RocqRefRunner exit " + proc.ExitCode + ": " + err);
                }
                if (line == null)
                {
                    Assert.Fail("RocqRefRunner returned no output (filtered)");
                }
                var parts = line.Trim().Split(' ');
                if (parts.Length != 2)
                {
                    Assert.Fail("malformed filtered orient line: '" + line + "'");
                }

                OrientSignRobust s;
                switch (parts[0])
                {
                    case "POS":       s = OrientSignRobust.Pos; break;
                    case "NEG":       s = OrientSignRobust.Neg; break;
                    case "ZERO":      s = OrientSignRobust.Zero; break;
                    case "NAN":       s = OrientSignRobust.Nan; break;
                    case "UNCERTAIN": s = OrientSignRobust.Uncertain; break;
                    default:
                        Assert.Fail("Unknown filtered sign token: " + parts[0]);
                        s = OrientSignRobust.Nan;
                        break;
                }
                double area = ParseOcamlFloat(parts[1]);
                return (s, area);
            }
        }

        private (OrientSign sign, double area) RunRocqRefOrient(
            BPoint p0, BPoint p1, BPoint q)
        {
            var psi = new ProcessStartInfo
            {
                FileName = _rocqRefPath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using (var proc = Process.Start(psi))
            {
                if (proc == null)
                {
                    Assert.Fail("RocqRefRunner process failed to start: " + _rocqRefPath);
                    return (OrientSign.Nan, double.NaN);
                }

                using (var w = proc.StandardInput)
                {
                    w.WriteLine("ORIENT");
                    w.WriteLine(Fmt(p0.X) + " " + Fmt(p0.Y));
                    w.WriteLine(Fmt(p1.X) + " " + Fmt(p1.Y));
                    w.WriteLine(Fmt(q.X)  + " " + Fmt(q.Y));
                }

                string line = proc.StandardOutput.ReadLine();
                proc.WaitForExit();
                if (proc.ExitCode != 0)
                {
                    string err = proc.StandardError.ReadToEnd();
                    Assert.Fail("RocqRefRunner exit code " + proc.ExitCode + ": " + err);
                }
                if (line == null)
                {
                    Assert.Fail("RocqRefRunner returned no output");
                }

                var parts = line.Trim().Split(' ');
                if (parts.Length != 2)
                {
                    Assert.Fail("RocqRefRunner returned malformed line: '" + line + "'");
                }

                OrientSign sign;
                switch (parts[0])
                {
                    case "POS":  sign = OrientSign.Pos;  break;
                    case "NEG":  sign = OrientSign.Neg;  break;
                    case "ZERO": sign = OrientSign.Zero; break;
                    case "NAN":  sign = OrientSign.Nan;  break;
                    default:
                        Assert.Fail("Unknown sign token: " + parts[0]);
                        sign = OrientSign.Nan;
                        break;
                }
                double area = ParseOcamlFloat(parts[1]);
                return (sign, area);
            }
        }

        private static string Fmt(double x)
        {
            if (double.IsNaN(x)) return "nan";
            if (double.IsPositiveInfinity(x)) return "infinity";
            if (double.IsNegativeInfinity(x)) return "neg_infinity";
            return x.ToString("R", CultureInfo.InvariantCulture);
        }

        private static double ParseOcamlFloat(string s)
        {
            if (s == "nan" || s == "Nan" || s == "NaN") return double.NaN;
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
