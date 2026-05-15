// =============================================================================
// NetTopologySuite.Curve.Robust.Simplify.GreedyPerpSimplifierRocqRefTests
// -----------------------------------------------------------------------------
// Differential tests against the **RocqRefRunner** -- the Coq-extracted
// reference binary built from NetTopologySuite.Proofs/oracle/.  Run the same
// polyline through the C# port and through RocqRefRunner, then assert
// bit-equal IEEE 754 binary64 output point-by-point.
//
// Skipped by default.  Activate by pointing the env var ROCQ_REF_BIN at the
// compiled RocqRefRunner:
//
//   In the proofs repo container:
//     make -f Makefile.gen          # writes oracle/extracted.ml
//     make -C oracle                # builds oracle/oracle_bin (= RocqRefRunner)
//
//   Then locally:
//     export ROCQ_REF_BIN=/path/to/NetTopologySuite.Proofs/oracle/oracle_bin
//     dotnet test --filter "FullyQualifiedName~RocqRef"
//
// On bit divergence:
//   - Most likely cause: the C# implementation drifted from the Coq spec.
//   - Less likely: the C# host is on a runtime without RNE binary64 (very
//     old x87 32-bit OCaml, or a non-IEEE FPU), in which case run both on
//     the same modern 64-bit host.
//   - Least likely: a real bug in the Coq spec.  Treat that finding as gold.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NetTopologySuite.Robust.Simplify;
using NUnit.Framework;

namespace NetTopologySuite.Test.Robust.Simplify
{
    [TestFixture]
    [Category("RocqRef")]
    public class GreedyPerpSimplifierRocqRefTests
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

        // Deterministic fixtures matching the existing structural-lemma tests
        // and the smoke tests run on the oracle side.
        [TestCase(0.5, new double[] { 0, 0, 1, 0, 2, 0, 3, 0, 4, 0 },
            TestName = "Collinear 5-point line, eps=0.5")]
        [TestCase(0.5, new double[] { 0, 0, 1, 0, 1, 1 },
            TestName = "L-shape, eps=0.5")]
        [TestCase(0.0, new double[] { 0, 0, 1e-300, 1e-300, 2e-300, 2e-300 },
            TestName = "Tiny subnormal scale, eps=0")]
        [TestCase(0.5, new double[] { 0, 0, 100000, 0.001, 200000, 0 },
            TestName = "Huge dx + tiny dy (perpendicular regime)")]
        [TestCase(1e-15, new double[] { 0, 0, 1, 1e-15, 2, 0 },
            TestName = "Near-collinear within machine eps")]
        public void CSharp_BitEqual_To_Oracle(double eps, double[] flatCoords)
        {
            var pts = FlatToBPoints(flatCoords);

            var csharp = GreedyPerpSimplifier.Simplify(eps, pts);
            var rocqRef = RunRocqRef(eps, pts);

            AssertBitEqual(csharp, rocqRef);
        }

        // Property-style fuzz over uniformly-distributed polylines in
        // [-5, 5]^2.  NUnit's [Random] generator is deterministic per seed
        // so any failure reproduces.  150 cases (15 n x 10 eps).
        [Test]
        public void Random_Polylines_BitEqual(
            [Random(2, 80, 15)] int n,
            [Random(0.0, 2.0, 10)] double eps)
        {
            var rng = TestContext.CurrentContext.Random;
            var pts = new List<BPoint>(n);
            for (int i = 0; i < n; i++)
            {
                pts.Add(new BPoint(
                    rng.NextDouble() * 10.0 - 5.0,
                    rng.NextDouble() * 10.0 - 5.0));
            }

            var csharp = GreedyPerpSimplifier.Simplify(eps, pts);
            var rocqRef = RunRocqRef(eps, pts);

            AssertBitEqual(csharp, rocqRef);
        }

        // -----------------------------------------------------------------
        // Adversarial families.  These poke at known failure modes for
        // floating-point geometric predicates and the perpendicular-
        // distance heuristic specifically.
        // -----------------------------------------------------------------

        // Tight clusters: many points within machine epsilon of each other,
        // which stresses the dist_sq -> 0 branch where rhs = eps^2 * 0 = 0
        // and even an arbitrarily small lhs satisfies `lhs <= 0` only when
        // lhs is also 0.  The C# port and RocqRef must agree on what "small"
        // means bit-exactly.
        [Test]
        public void Adversarial_TightCluster_BitEqual(
            [Random(3, 20, 6)] int n,
            [Values(1e-300, 1e-100, 1e-12, 1e-6)] double scale)
        {
            var rng = TestContext.CurrentContext.Random;
            var pts = new List<BPoint>(n);
            for (int i = 0; i < n; i++)
            {
                pts.Add(new BPoint(
                    rng.NextDouble() * scale,
                    rng.NextDouble() * scale));
            }

            var csharp = GreedyPerpSimplifier.Simplify(0.5, pts);
            var rocqRef = RunRocqRef(0.5, pts);

            AssertBitEqual(csharp, rocqRef);
        }

        // Repeated points: the predicate `dist_sq kept r = 0` makes the
        // right-hand side zero too, so the test reduces to lhs <= 0.  Two
        // implementations can diverge here if one of them special-cases
        // duplicate points.
        [Test]
        public void Adversarial_RepeatedPoints_BitEqual(
            [Values(2, 3, 5, 10)] int repeats,
            [Random(0.0, 1.0, 3)] double eps)
        {
            var pts = new List<BPoint>();
            for (int i = 0; i < repeats; i++)
            {
                pts.Add(new BPoint(1.0, 2.0));
            }
            pts.Add(new BPoint(3.0, 4.0));

            var csharp = GreedyPerpSimplifier.Simplify(eps, pts);
            var rocqRef = RunRocqRef(eps, pts);

            AssertBitEqual(csharp, rocqRef);
        }

        // Near-collinear with controlled deviation.  The middle point is
        // perturbed off the line `(0,0) -> (1,0)` by a small dy.  We sweep
        // dy across machine-epsilon scales to find any boundary where
        // C# and RocqRef disagree about whether the point should be dropped.
        [Test]
        public void Adversarial_NearCollinear_BitEqual(
            [Values(0.0, 1e-300, 1e-100, 1e-15, 1e-12, 1e-9, 1e-6, 1e-3, 1e-1)] double dy,
            [Values(1e-10, 1e-6, 1e-3, 0.5, 1.0)] double eps)
        {
            var pts = new List<BPoint>
            {
                new BPoint(0.0, 0.0),
                new BPoint(0.5, dy),
                new BPoint(1.0, 0.0),
            };

            var csharp = GreedyPerpSimplifier.Simplify(eps, pts);
            var rocqRef = RunRocqRef(eps, pts);

            AssertBitEqual(csharp, rocqRef);
        }

        // NaN inputs.  `b64_le` returns false on either operand being NaN,
        // so the simplifier should fall through to "keep" on any test that
        // involves a NaN coordinate -- never dropping a point uncertainly.
        [Test]
        public void Adversarial_NaN_BitEqual(
            [Values(0, 1, 2)] int nanPosition)
        {
            var pts = new List<BPoint>
            {
                new BPoint(0.0, 0.0),
                new BPoint(1.0, 0.0),
                new BPoint(2.0, 0.0),
            };
            pts[nanPosition] = new BPoint(double.NaN, pts[nanPosition].Y);

            var csharp = GreedyPerpSimplifier.Simplify(0.5, pts);
            var rocqRef = RunRocqRef(0.5, pts);

            AssertBitEqual(csharp, rocqRef);
        }

        // Mixed signs + huge magnitude excursions.  Tests the eps^2 *
        // dist_sq composition when dist_sq is large enough that eps^2 *
        // dist_sq can overflow; the simplifier should still behave
        // consistently between the two implementations.
        [Test]
        public void Adversarial_HugeMagnitude_BitEqual(
            [Values(1e150, 1e200, 1e300)] double mag,
            [Values(0.0, 0.1, 1.0)] double eps)
        {
            var pts = new List<BPoint>
            {
                new BPoint(-mag, -mag),
                new BPoint( 0.0,  0.0),
                new BPoint( mag,  mag),
            };

            var csharp = GreedyPerpSimplifier.Simplify(eps, pts);
            var rocqRef = RunRocqRef(eps, pts);

            AssertBitEqual(csharp, rocqRef);
        }

        private List<BPoint> RunRocqRef(double eps, IReadOnlyList<BPoint> pts)
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
                    return null;
                }

                // Feed input: eps then one "x y" line per point.
                using (var w = proc.StandardInput)
                {
                    // RocqRefRunner now dispatches on a mode line on stdin
                    // (added when the ORIENT mode was wired up alongside the
                    // simplifier in the proofs repo).  Prefix every query.
                    w.WriteLine("SIMPLIFY");
                    w.WriteLine(FormatRoundtrip(eps));
                    foreach (var p in pts)
                    {
                        w.WriteLine(FormatRoundtrip(p.X) + " " + FormatRoundtrip(p.Y));
                    }
                }

                // Parse output: one "<hex-x> <hex-y>" line per result point.
                var result = new List<BPoint>();
                string line;
                while ((line = proc.StandardOutput.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length == 0) continue;
                    var parts = line.Split(' ');
                    if (parts.Length != 2)
                    {
                        Assert.Fail("RocqRefRunner returned malformed line: '" + line + "'");
                    }
                    result.Add(new BPoint(
                        ParseOcamlFloat(parts[0]),
                        ParseOcamlFloat(parts[1])));
                }

                proc.WaitForExit();
                if (proc.ExitCode != 0)
                {
                    var err = proc.StandardError.ReadToEnd();
                    Assert.Fail("RocqRefRunner exited with code " + proc.ExitCode + ": " + err);
                }
                return result;
            }
        }

        // Decimal "R" format on .NET round-trips IEEE 754 binary64 losslessly,
        // and OCaml's `float_of_string` accepts the same shape.  We use this
        // (not hex) on the C# -> OCaml direction because it's simpler and
        // doesn't need a custom formatter on this side.
        private static string FormatRoundtrip(double x)
        {
            if (double.IsNaN(x)) return "nan";
            if (double.IsPositiveInfinity(x)) return "infinity";
            if (double.IsNegativeInfinity(x)) return "neg_infinity";
            return x.ToString("R", CultureInfo.InvariantCulture);
        }

        // RocqRefRunner emits "%h" hex floats (e.g. "0x1.8p+0", "0x0p+0",
        // "nan", "infinity", "neg_infinity").  netstandard2.0 has no native
        // hex-float parse, so we do it manually here.  Lossless for any
        // finite float.
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
                // Plain decimal -- shouldn't really happen with %h, but
                // tolerated for robustness.
                return double.Parse(s, CultureInfo.InvariantCulture);
            }
            i += 2;
            int pIdx = s.IndexOf('p', i);
            if (pIdx < 0)
            {
                throw new FormatException("Bad hex float (no 'p' exponent): " + s);
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

        private static List<BPoint> FlatToBPoints(double[] flat)
        {
            var pts = new List<BPoint>(flat.Length / 2);
            for (int i = 0; i < flat.Length; i += 2)
            {
                pts.Add(new BPoint(flat[i], flat[i + 1]));
            }
            return pts;
        }

        private static void AssertBitEqual(List<BPoint> actual, List<BPoint> expected)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count),
                "result lengths differ (C#=" + actual.Count +
                " RocqRef=" + expected.Count + ")");
            for (int i = 0; i < actual.Count; i++)
            {
                long ax = BitConverter.DoubleToInt64Bits(actual[i].X);
                long ay = BitConverter.DoubleToInt64Bits(actual[i].Y);
                long ex = BitConverter.DoubleToInt64Bits(expected[i].X);
                long ey = BitConverter.DoubleToInt64Bits(expected[i].Y);
                Assert.That(ax, Is.EqualTo(ex),
                    "point " + i + " X bits: C#=0x" + ax.ToString("X16") +
                    " RocqRef=0x" + ex.ToString("X16"));
                Assert.That(ay, Is.EqualTo(ey),
                    "point " + i + " Y bits: C#=0x" + ay.ToString("X16") +
                    " RocqRef=0x" + ey.ToString("X16"));
            }
        }
    }
}
