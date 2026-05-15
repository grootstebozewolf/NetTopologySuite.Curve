// =============================================================================
// NetTopologySuite.Curve.Robust.Simplify.GreedyPerpSimplifierDifferentialTests
// -----------------------------------------------------------------------------
// Differential tests: run the same polyline through the C# port and through
// the Coq-extracted OCaml oracle binary, then assert bit-equal IEEE 754
// binary64 output point-by-point.
//
// Skipped by default.  Enable by pointing the env var NTS_ORACLE_BIN at the
// `oracle_bin` produced from NetTopologySuite.Proofs/oracle/:
//
//   In the proofs repo container:
//     make -f Makefile.gen          # writes oracle/extracted.ml
//     make -C oracle                # builds oracle/oracle_bin
//
//   Then locally:
//     export NTS_ORACLE_BIN=/path/to/NetTopologySuite.Proofs/oracle/oracle_bin
//     dotnet test --filter "FullyQualifiedName~Differential"
//
// On bit divergence:
//   - Most likely cause: the C# transliteration drifted from the Coq Fixpoint.
//   - Less likely: the C# host is on a runtime without RNE binary64 (very old
//     x87 32-bit OCaml, or a non-IEEE FPU), in which case run both on the
//     same modern 64-bit host.
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
    [Category("Differential")]
    public class GreedyPerpSimplifierDifferentialTests
    {
        private const string OraclePathEnvVar = "NTS_ORACLE_BIN";
        private string _oraclePath;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _oraclePath = Environment.GetEnvironmentVariable(OraclePathEnvVar);
            if (string.IsNullOrWhiteSpace(_oraclePath) || !File.Exists(_oraclePath))
            {
                Assert.Ignore(
                    "Differential tests skipped: set " + OraclePathEnvVar +
                    " to the path of the oracle binary. See NetTopologySuite.Proofs/oracle/ " +
                    "for build instructions.");
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
            var oracle = RunOracle(eps, pts);

            AssertBitEqual(csharp, oracle);
        }

        // Property-style fuzz: a handful of randomised polylines per run, with
        // bounded coordinate magnitudes that stay clear of overflow on the
        // squared-cross test.  NUnit's [Random] generator is deterministic per
        // seed so failures reproduce.
        [Test]
        public void Random_Polylines_BitEqual(
            [Random(2, 50, 10)] int n,
            [Random(0.0, 1.5, 3)] double eps)
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
            var oracle = RunOracle(eps, pts);

            AssertBitEqual(csharp, oracle);
        }

        private List<BPoint> RunOracle(double eps, IReadOnlyList<BPoint> pts)
        {
            var psi = new ProcessStartInfo
            {
                FileName = _oraclePath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using (var proc = Process.Start(psi))
            {
                if (proc == null)
                {
                    Assert.Fail("oracle process failed to start: " + _oraclePath);
                    return null;
                }

                // Feed input: eps then one "x y" line per point.
                using (var w = proc.StandardInput)
                {
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
                        Assert.Fail("oracle returned malformed line: '" + line + "'");
                    }
                    result.Add(new BPoint(
                        ParseOcamlFloat(parts[0]),
                        ParseOcamlFloat(parts[1])));
                }

                proc.WaitForExit();
                if (proc.ExitCode != 0)
                {
                    var err = proc.StandardError.ReadToEnd();
                    Assert.Fail("oracle exited with code " + proc.ExitCode + ": " + err);
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

        // Oracle emits "%h" hex floats (e.g. "0x1.8p+0", "0x0p+0", "nan",
        // "infinity", "neg_infinity").  netstandard2.0 has no native hex-float
        // parse, so we do it manually here.  Lossless for any finite float.
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
                "result lengths differ (C#=" + actual.Count + " oracle=" + expected.Count + ")");
            for (int i = 0; i < actual.Count; i++)
            {
                long ax = BitConverter.DoubleToInt64Bits(actual[i].X);
                long ay = BitConverter.DoubleToInt64Bits(actual[i].Y);
                long ex = BitConverter.DoubleToInt64Bits(expected[i].X);
                long ey = BitConverter.DoubleToInt64Bits(expected[i].Y);
                Assert.That(ax, Is.EqualTo(ex),
                    "point " + i + " X bits: C#=0x" + ax.ToString("X16") +
                    " oracle=0x" + ex.ToString("X16"));
                Assert.That(ay, Is.EqualTo(ey),
                    "point " + i + " Y bits: C#=0x" + ay.ToString("X16") +
                    " oracle=0x" + ey.ToString("X16"));
            }
        }
    }
}
