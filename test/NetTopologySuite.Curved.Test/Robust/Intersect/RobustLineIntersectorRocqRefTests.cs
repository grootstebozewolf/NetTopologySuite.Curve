// =============================================================================
// NetTopologySuite.Curve.Robust.Intersect.RobustLineIntersectorRocqRefTests
// -----------------------------------------------------------------------------
// Differential tests against the RocqRefRunner INTERSECT_FILTERED mode.  For
// each segment pair, run RobustLineIntersector.SignFiltered and the
// Coq-extracted reference; assert the 5-valued sign matches.  Skipped by
// default; activate by pointing `ROCQ_REF_BIN` at the RocqRefRunner binary.
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
        // Helpers.
        // -----------------------------------------------------------------

        private void AssertMatches(BPoint p0, BPoint p1, BPoint q0, BPoint q1)
        {
            var refSign = RunRocqRefIntersectFiltered(p0, p1, q0, q1);
            var csSign  = RobustLineIntersector.SignFiltered(p0, p1, q0, q1);
            Assert.That(csSign, Is.EqualTo(refSign),
                "intersect sign mismatch: C#=" + csSign + " RocqRef=" + refSign);
        }

        private IntersectSign RunRocqRefIntersectFiltered(
            BPoint p0, BPoint p1, BPoint q0, BPoint q1)
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
                    return IntersectSign.Nan;
                }

                using (var w = proc.StandardInput)
                {
                    w.WriteLine("INTERSECT_FILTERED");
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
                    Assert.Fail("RocqRefRunner exit code " + proc.ExitCode + ": " + err);
                }
                if (line == null)
                {
                    Assert.Fail("RocqRefRunner returned no output (intersect)");
                }

                string token = line.Trim();
                switch (token)
                {
                    case "NONE":      return IntersectSign.None;
                    case "POINT":     return IntersectSign.Point;
                    case "COLLINEAR": return IntersectSign.Collinear;
                    case "NAN":       return IntersectSign.Nan;
                    case "UNCERTAIN": return IntersectSign.Uncertain;
                    default:
                        Assert.Fail("Unknown intersect token: " + token);
                        return IntersectSign.Nan;
                }
            }
        }

        private static string Fmt(double x)
        {
            if (double.IsNaN(x))                return "nan";
            if (double.IsPositiveInfinity(x))   return "infinity";
            if (double.IsNegativeInfinity(x))   return "neg_infinity";
            return x.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
