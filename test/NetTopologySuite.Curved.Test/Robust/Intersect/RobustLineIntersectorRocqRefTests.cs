// =============================================================================
// NetTopologySuite.Curve.Robust.Intersect.RobustLineIntersectorRocqRefTests
// -----------------------------------------------------------------------------
// Differential tests against the RocqRefRunner.  For each segment pair:
//
//   1. INTERSECT_FILTERED              -> compare the 5-valued sign with
//                                         RobustLineIntersector.SignFiltered.
//   2. INTERSECT_POINT_XY (Phase 1)    -> only when the sign is POINT, fetch
//                                         the rounded intersection coords
//                                         from the runner (XY <x> <y> -- a
//                                         totals function, always defined)
//                                         and bit-compare with the C# port.
//
// Non-POINT cases skip step 2 entirely (one spawn instead of two), since the
// sign equality already certifies "no intersection point" on both sides.
//
// The option layer (INTERSECT_POINT_FILTERED -> NONE / POINT x y) is anchored
// separately in OptionLayer_PointFiltered_AgreesWithSignAndXy; this confines
// option-layer round-tripping to a small targeted fixture rather than every
// fuzz case.
//
// Skipped by default; activate by pointing `ROCQ_REF_BIN` at the
// RocqRefRunner binary.
// =============================================================================

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using NetTopologySuite.Robust.Intersect;
using NetTopologySuite.Robust.Simplify;
using NUnit.Framework;

namespace NetTopologySuite.Test.Robust.Intersect
{
    [TestFixture]
    [Category("RocqRef")]
    public class RobustLineIntersectorRocqRefTests
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
                    " to the path of the RocqRefRunner binary.");
            }
        }

        // -----------------------------------------------------------------
        // Deterministic fixtures covering each result branch.
        // -----------------------------------------------------------------
        [TestCase(0, 0, 2, 0, 1, -1, 1, 1, TestName = "Proper crossing")]
        [TestCase(0, 0, 1, 0, 0, 1, 1, 1, TestName = "Disjoint parallel")]
        [TestCase(0, 0, 1, 0, 2, 0, 3, 0, TestName = "Collinear disjoint")]
        [TestCase(0, 0, 2, 0, 1, 0, 3, 0, TestName = "Collinear overlapping")]
        [TestCase(0, 0, 1, 0, 1, 0, 2, 0, TestName = "Collinear shared endpoint")]
        [TestCase(0, 0, 1, 0, 0.5, 0, 0.5, 1, TestName = "T-junction endpoint on segment")]
        [TestCase(0, 0, 1, 1, 0, 1, 1, 0, TestName = "Diagonals cross")]
        public void CSharp_BitEqual_To_RocqRef(
            double x0, double y0, double x1, double y1,
            double xq0, double yq0, double xq1, double yq1)
        {
            AssertMatches(
                new BPoint(x0, y0), new BPoint(x1, y1),
                new BPoint(xq0, yq0), new BPoint(xq1, yq1));
        }

        // -----------------------------------------------------------------
        // Random property-style fuzz in [-5, 5]^2.
        // -----------------------------------------------------------------
        [Test]
        public void Random_Segments_BitEqual([Random(0, int.MaxValue, 80)] int seed)
        {
            var rng = new Random(seed);
            AssertMatches(
                new BPoint(rng.NextDouble() * 10 - 5, rng.NextDouble() * 10 - 5),
                new BPoint(rng.NextDouble() * 10 - 5, rng.NextDouble() * 10 - 5),
                new BPoint(rng.NextDouble() * 10 - 5, rng.NextDouble() * 10 - 5),
                new BPoint(rng.NextDouble() * 10 - 5, rng.NextDouble() * 10 - 5));
        }

        // -----------------------------------------------------------------
        // Adversarial: NaN in any coord of any position.
        // -----------------------------------------------------------------
        [Test]
        public void Adversarial_NaN_BitEqual(
            [Values(0, 1, 2, 3)] int pos,
            [Values(0, 1)]       int coord)
        {
            var pts = new[]
            {
                new BPoint(0, 0),
                new BPoint(1, 0),
                new BPoint(0, 1),
                new BPoint(1, 1),
            };
            double nx = coord == 0 ? double.NaN : pts[pos].X;
            double ny = coord == 1 ? double.NaN : pts[pos].Y;
            pts[pos] = new BPoint(nx, ny);
            AssertMatches(pts[0], pts[1], pts[2], pts[3]);
        }

        // -----------------------------------------------------------------
        // Adversarial: huge magnitudes where intermediate products may
        // overflow.  Two diagonals crossing at the origin.
        // -----------------------------------------------------------------
        [Test]
        public void Adversarial_HugeMagnitude_BitEqual(
            [Values(1e100, 1e150, 1e200, 1e250, 1e300)] double mag)
        {
            AssertMatches(
                new BPoint(-mag, -mag), new BPoint( mag,  mag),
                new BPoint(-mag,  mag), new BPoint( mag, -mag));
        }

        // -----------------------------------------------------------------
        // Adversarial: integer regime |coord| <= 2^25 -- the regime where
        // the Coq theorem b64_intersect_sign_filtered_none_sound_small_int
        // proves IntersectNone is sound w.r.t. the R-side intersection
        // predicate.  A divergence between C# and RocqRef here would flag an
        // implementation drift on inputs the Coq proof shows are fully
        // determined.
        // -----------------------------------------------------------------
        [TestCase(0, 0, 10, 10, 0, 10, 10, 0,
            TestName = "IntegerRegime: two diagonals cross at (5,5)")]
        [TestCase(0, 0, 10, 0, 5, 5, 5, -5,
            TestName = "IntegerRegime: T crossing at midpoint")]
        [TestCase(0, 0, 10, 0, 20, 0, 30, 0,
            TestName = "IntegerRegime: collinear disjoint")]
        [TestCase(0, 0, 10, 0, 5, 0, 15, 0,
            TestName = "IntegerRegime: collinear overlapping")]
        [TestCase(-33554432, -33554432,  33554432,  33554432,
                  -33554432,  33554432,  33554432, -33554432,
            TestName = "IntegerRegime: full-range crossing diagonals")]
        [TestCase(-33554432, -33554432,  33554432,  33554432,
                  -33554432, -33554432,  33554432,  33554432,
            TestName = "IntegerRegime: full-range identical segment (collinear)")]
        [TestCase(0, 0, 33554432, 0, 0, 1, 33554432, 1,
            TestName = "IntegerRegime: parallel at boundary, disjoint")]
        public void Adversarial_IntegerRegime_BitEqual(
            double x0, double y0, double x1, double y1,
            double xq0, double yq0, double xq1, double yq1)
        {
            AssertMatches(
                new BPoint(x0, y0), new BPoint(x1, y1),
                new BPoint(xq0, yq0), new BPoint(xq1, yq1));
        }

        // Random integer fuzz uniform in [-2^25, 2^25]^2.  Every sample is in
        // the proved-sound regime; C# must bit-match RocqRef on all 80.
        [Test]
        public void Adversarial_IntegerRegime_RandomInteger_BitEqual(
            [Random(0, int.MaxValue, 80)] int seed)
        {
            var rng = new Random(seed);
            const int K = 33554432; // 2^25
            AssertMatches(
                new BPoint(rng.Next(-K, K + 1), rng.Next(-K, K + 1)),
                new BPoint(rng.Next(-K, K + 1), rng.Next(-K, K + 1)),
                new BPoint(rng.Next(-K, K + 1), rng.Next(-K, K + 1)),
                new BPoint(rng.Next(-K, K + 1), rng.Next(-K, K + 1)));
        }

        // -----------------------------------------------------------------
        // Adversarial: intersection near the origin, where cancellation in
        // the Cramer numerator P0.x + s * (P1.x - P0.x) loses precision and
        // any drift between the C# port and the Coq reference would show
        // up first.
        // -----------------------------------------------------------------
        [TestCase(1e-300, TestName = "NearZero: 1e-300 subnormal regime")]
        [TestCase(1e-200, TestName = "NearZero: 1e-200")]
        [TestCase(1e-100, TestName = "NearZero: 1e-100")]
        [TestCase(1e-50,  TestName = "NearZero: 1e-50")]
        [TestCase(1e-15,  TestName = "NearZero: 1e-15 around ULP at 1.0")]
        [TestCase(1e-9,   TestName = "NearZero: 1e-9")]
        public void Adversarial_NearZeroIntersection_BitEqual(double eps)
        {
            AssertMatches(
                new BPoint(-eps, -eps), new BPoint( eps,  eps),
                new BPoint(-eps,  eps), new BPoint( eps, -eps));
        }

        // -----------------------------------------------------------------
        // Adversarial: one segment vastly larger than the other.  The
        // Coq-side b64 arithmetic is associativity-sensitive; if the C#
        // port reorders the s = qp0 / den or the P0 + s*(P1-P0) chain,
        // these mixed-scale crossings catch it.
        // -----------------------------------------------------------------
        [TestCase(1e10,  1e-10, TestName = "MixedScale: 1e10 x 1e-10")]
        [TestCase(1e15,  1e-5,  TestName = "MixedScale: 1e15 x 1e-5")]
        [TestCase(1e20,  1.0,   TestName = "MixedScale: 1e20 x 1.0")]
        [TestCase(1e5,   1e-15, TestName = "MixedScale: 1e5 x 1e-15 (s ~ 0)")]
        public void Adversarial_MixedScale_BitEqual(double big, double small)
        {
            // A horizontal segment of half-length `big` crossed by a vertical
            // segment of half-length `small` centred on the same origin.  s
            // (the Cramer parameter on the big segment) is exactly 0.5; the
            // intersection point is (0, 0).
            AssertMatches(
                new BPoint(-big, 0), new BPoint(big, 0),
                new BPoint(0, -small), new BPoint(0, small));
        }

        // -----------------------------------------------------------------
        // Adversarial: an endpoint of one segment lies exactly on the
        // interior of the other (T-junction with the witness coincident
        // with a vertex).  Tests that the Cramer ratio s rounds to exactly
        // 0 or 1 where the geometry demands it.
        // -----------------------------------------------------------------
        [TestCase(0, 0, 10, 0,  5, 0,  5,  5, TestName = "EndpointIncidence: Q0 mid-P, T up")]
        [TestCase(0, 0, 10, 0,  5, 5,  5,  0, TestName = "EndpointIncidence: Q1 mid-P, T down")]
        [TestCase(0, 0, 10, 0,  0, 0,  0,  5, TestName = "EndpointIncidence: Q0=P0 at vertex")]
        [TestCase(0, 0, 10, 0, 10, 0, 10,  5, TestName = "EndpointIncidence: Q0=P1 at vertex")]
        [TestCase(0, 0,  4, 4,  2, 2,  6,  2, TestName = "EndpointIncidence: cross at Q0 mid-P")]
        public void Adversarial_EndpointIncidence_BitEqual(
            double x0, double y0, double x1, double y1,
            double xq0, double yq0, double xq1, double yq1)
        {
            AssertMatches(
                new BPoint(x0, y0), new BPoint(x1, y1),
                new BPoint(xq0, yq0), new BPoint(xq1, yq1));
        }

        // -----------------------------------------------------------------
        // Adversarial: subnormal-magnitude coordinates.  Below DBL_MIN
        // (~2.2e-308) the IEEE 754 representation loses the implicit
        // leading bit and arithmetic operates with reduced precision.
        // Any drift in how the C# port handles subnormals will surface
        // here.
        // -----------------------------------------------------------------
        [Test]
        public void Adversarial_Subnormal_BitEqual(
            [Values(double.Epsilon, 1e-310, 1e-320)] double tiny)
        {
            AssertMatches(
                new BPoint(-tiny, 0), new BPoint(tiny, 0),
                new BPoint(0, -tiny), new BPoint(0, tiny));
        }

        // -----------------------------------------------------------------
        // Option layer: INTERSECT_POINT_FILTERED rounds the runner's totals
        // into an option.  Verify directly for representative cases that
        // (i) it emits NONE for every non-POINT sign branch, and (ii) when
        // it emits POINT, the coords match INTERSECT_POINT_XY (and the C#
        // option-layer IntersectionPoint).
        // -----------------------------------------------------------------
        [TestCase(0, 0, 2, 0, 1, -1, 1, 1, TestName = "OptionLayer: proper crossing -> POINT")]
        [TestCase(0, 0, 1, 0, 0, 1, 1, 1, TestName = "OptionLayer: disjoint -> NONE")]
        [TestCase(0, 0, 1, 0, 2, 0, 3, 0, TestName = "OptionLayer: collinear disjoint -> NONE")]
        [TestCase(0, 0, 2, 0, 1, 0, 3, 0, TestName = "OptionLayer: collinear overlap -> NONE")]
        [TestCase(double.NaN, 0, 1, 0, 0, 1, 1, 1, TestName = "OptionLayer: NaN input -> NONE")]
        public void OptionLayer_PointFiltered_AgreesWithSignAndXy(
            double x0, double y0, double x1, double y1,
            double xq0, double yq0, double xq1, double yq1)
        {
            var p0 = new BPoint(x0, y0);
            var p1 = new BPoint(x1, y1);
            var q0 = new BPoint(xq0, yq0);
            var q1 = new BPoint(xq1, yq1);

            var refSign     = RunRocqRefIntersectFiltered(p0, p1, q0, q1);
            var refFiltered = RunRocqRefIntersectPointFiltered(p0, p1, q0, q1);

            if (refSign == IntersectSign.Point)
            {
                Assert.That(refFiltered.hasPoint, Is.True,
                    "option layer: INTERSECT_FILTERED=POINT but INTERSECT_POINT_FILTERED=NONE");

                var (refX, refY) = RunRocqRefIntersectPointXy(p0, p1, q0, q1);
                Assert.That(BitConverter.DoubleToInt64Bits(refFiltered.x),
                            Is.EqualTo(BitConverter.DoubleToInt64Bits(refX)),
                            "option-layer X bits diverge from totals");
                Assert.That(BitConverter.DoubleToInt64Bits(refFiltered.y),
                            Is.EqualTo(BitConverter.DoubleToInt64Bits(refY)),
                            "option-layer Y bits diverge from totals");
            }
            else
            {
                Assert.That(refFiltered.hasPoint, Is.False,
                    "option layer: INTERSECT_FILTERED=" + refSign +
                    " but INTERSECT_POINT_FILTERED=POINT");
            }
        }

        // -----------------------------------------------------------------
        // Helpers.
        // -----------------------------------------------------------------

        private void AssertMatches(BPoint p0, BPoint p1, BPoint q0, BPoint q1)
        {
            var refSign = RunRocqRefIntersectFiltered(p0, p1, q0, q1);
            var csSign  = RobustLineIntersector.SignFiltered(p0, p1, q0, q1);
            Assert.That(csSign, Is.EqualTo(refSign),
                "intersect sign mismatch: C#=" + csSign + " RocqRef=" + refSign);

            var csPoint = RobustLineIntersector.IntersectionPoint(p0, p1, q0, q1);

            if (refSign == IntersectSign.Point)
            {
                // Use INTERSECT_POINT_XY -- the Phase 1 totals mode -- since
                // we've already determined the sign is POINT and the coords
                // are geometrically meaningful.  Saves the option-layer
                // round-trip (validated separately in
                // OptionLayer_PointFiltered_AgreesWithSignAndXy).
                var (refX, refY) = RunRocqRefIntersectPointXy(p0, p1, q0, q1);

                Assert.That(csPoint, Is.Not.Null,
                    "intersection point: C#=null while RocqRef sign=POINT");
                long csXBits  = BitConverter.DoubleToInt64Bits(csPoint!.Value.X);
                long refXBits = BitConverter.DoubleToInt64Bits(refX);
                long csYBits  = BitConverter.DoubleToInt64Bits(csPoint.Value.Y);
                long refYBits = BitConverter.DoubleToInt64Bits(refY);
                Assert.That(csXBits, Is.EqualTo(refXBits),
                    "intersection X bits: C#=0x" + csXBits.ToString("X16") +
                    " RocqRef=0x" + refXBits.ToString("X16"));
                Assert.That(csYBits, Is.EqualTo(refYBits),
                    "intersection Y bits: C#=0x" + csYBits.ToString("X16") +
                    " RocqRef=0x" + refYBits.ToString("X16"));
            }
            else
            {
                Assert.That(csPoint, Is.Null,
                    "intersection point: C# returned a point while RocqRef sign=" + refSign);
            }
        }

        private IntersectSign RunRocqRefIntersectFiltered(
            BPoint p0, BPoint p1, BPoint q0, BPoint q1)
        {
            string line = RunRocqRefSingleLine(
                "INTERSECT_FILTERED", p0, p1, q0, q1, "intersect");

            switch (line)
            {
                case "NONE":      return IntersectSign.None;
                case "POINT":     return IntersectSign.Point;
                case "COLLINEAR": return IntersectSign.Collinear;
                case "NAN":       return IntersectSign.Nan;
                case "UNCERTAIN": return IntersectSign.Uncertain;
                default:
                    Assert.Fail("Unknown intersect token: " + line);
                    return IntersectSign.Nan;
            }
        }

        // Option-layer: NONE or POINT <x> <y>.  May report no intersection.
        private (bool hasPoint, double x, double y) RunRocqRefIntersectPointFiltered(
            BPoint p0, BPoint p1, BPoint q0, BPoint q1)
        {
            string line = RunRocqRefSingleLine(
                "INTERSECT_POINT_FILTERED", p0, p1, q0, q1, "intersect point");

            if (line == "NONE")
            {
                return (false, double.NaN, double.NaN);
            }
            var parts = line.Split(' ');
            if (parts.Length != 3 || parts[0] != "POINT")
            {
                Assert.Fail("malformed intersect-point line: '" + line + "'");
            }
            double x = ParseOcamlFloat(parts[1]);
            double y = ParseOcamlFloat(parts[2]);
            return (true, x, y);
        }

        // Totals: XY <x> <y> unconditionally.  Callers are responsible for
        // first calling INTERSECT_FILTERED to determine whether the coords
        // are geometrically meaningful.
        private (double x, double y) RunRocqRefIntersectPointXy(
            BPoint p0, BPoint p1, BPoint q0, BPoint q1)
        {
            string line = RunRocqRefSingleLine(
                "INTERSECT_POINT_XY", p0, p1, q0, q1, "intersect point xy");

            var parts = line.Split(' ');
            if (parts.Length != 3 || parts[0] != "XY")
            {
                Assert.Fail("malformed intersect-point-xy line: '" + line + "'");
            }
            double x = ParseOcamlFloat(parts[1]);
            double y = ParseOcamlFloat(parts[2]);
            return (x, y);
        }

        // Shared transport: spawn the runner, send `<mode>\n<4 points>`,
        // return the single trimmed stdout line.  All three runner modes
        // exposed by Phase 1 follow this protocol exactly.
        private string RunRocqRefSingleLine(
            string mode, BPoint p0, BPoint p1, BPoint q0, BPoint q1, string ctx)
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
                    return string.Empty;
                }

                using (var w = proc.StandardInput)
                {
                    w.WriteLine(mode);
                    w.WriteLine(Fmt(p0.X) + " " + Fmt(p0.Y));
                    w.WriteLine(Fmt(p1.X) + " " + Fmt(p1.Y));
                    w.WriteLine(Fmt(q0.X) + " " + Fmt(q0.Y));
                    w.WriteLine(Fmt(q1.X) + " " + Fmt(q1.Y));
                }

                string line = proc.StandardOutput.ReadLine();
                proc.WaitForExit();
                if (proc.ExitCode != 0)
                {
                    string err = proc.StandardError.ReadToEnd();
                    Assert.Fail("RocqRefRunner exit code " + proc.ExitCode +
                                " (" + ctx + "): " + err);
                }
                if (line == null)
                {
                    Assert.Fail("RocqRefRunner returned no output (" + ctx + ")");
                }
                return line!.Trim();
            }
        }

        private static string Fmt(double x)
        {
            if (double.IsNaN(x))                return "nan";
            if (double.IsPositiveInfinity(x))   return "infinity";
            if (double.IsNegativeInfinity(x))   return "neg_infinity";
            return x.ToString("R", CultureInfo.InvariantCulture);
        }

        // Parse OCaml's "%h" hex-float output back into a double.  Mirrors
        // the helper in RobustOrientationRocqRefTests; duplicated here to
        // keep this fixture self-contained.
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
