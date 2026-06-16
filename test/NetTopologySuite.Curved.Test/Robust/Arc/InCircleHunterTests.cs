// =============================================================================
// InCircleHunterTests
// -----------------------------------------------------------------------------
// Differential + counterexample hunts for the naive in-circle determinant sign
// (InCircle.Sign).  There is no Stage A filter on this predicate yet, so the
// hunts (a) pin its exactness on the well-separated regime where the lifted
// determinant fits the mantissa, (b) hunt the cocircular regime where the naive
// determinant provably loses the sign -- documenting the fragility a future
// filtered layer would close -- and (c) cross-validate the two exact ground
// truths (in-process BigInteger vs the Coq-extracted Z^2 INCIRCLE_EXACT oracle).
// =============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using NetTopologySuite.Robust.Arc;
using NetTopologySuite.Robust.Simplify;
using NUnit.Framework;

namespace NetTopologySuite.Test.Robust.Arc
{
    [TestFixture]
    public class InCircleHunterTests
    {
        // Exact sign of the lifted in-circle determinant over Z, via BigInteger.
        private static int ExactSign(long ax_, long ay_, long bx_, long by_, long cx_, long cy_, long px, long py)
        {
            BigInteger ax = ax_ - px, ay = ay_ - py, bx = bx_ - px, by = by_ - py, cx = cx_ - px, cy = cy_ - py;
            BigInteger na = ax * ax + ay * ay, nb = bx * bx + by * by, nc = cx * cx + cy * cy;
            BigInteger det = ax * (by * nc - cy * nb) - ay * (bx * nc - cx * nb) + na * (bx * cy - cx * by);
            return det.Sign;
        }

        private static int NaiveSign(IncircleSign s) => s switch
        { IncircleSign.Pos => 1, IncircleSign.Neg => -1, IncircleSign.Zero => 0, _ => 99 };

        private static int NaiveSign(long ax, long ay, long bx, long by, long cx, long cy, long px, long py)
            => NaiveSign(InCircle.Sign(new BPoint(ax, ay), new BPoint(bx, by), new BPoint(cx, cy), new BPoint(px, py)));

        // 12 lattice points on the circle x^2 + y^2 = 25 (radius 5).
        private static readonly (long, long)[] Circle5 =
        { (5,0),(0,5),(-5,0),(0,-5),(3,4),(4,3),(-3,4),(-4,3),(3,-4),(4,-3),(-3,-4),(-4,-3) };

        // -------------------------------------------------------------------------
        [Test]
        public void Sign_MatchesExact_OnWellSeparatedConfigs()
        {
            var rng = new Random(1106);
            int mismatches = 0;
            for (int i = 0; i < 100_000; i++)
            {
                long ax = rng.Next(-50, 51), ay = rng.Next(-50, 51), bx = rng.Next(-50, 51), by = rng.Next(-50, 51);
                long cx = rng.Next(-50, 51), cy = rng.Next(-50, 51), px = rng.Next(-50, 51), py = rng.Next(-50, 51);
                if (NaiveSign(ax, ay, bx, by, cx, cy, px, py) != ExactSign(ax, ay, bx, by, cx, cy, px, py))
                    mismatches++;
            }
            Assert.That(mismatches, Is.Zero,
                "Naive in-circle sign diverged from exact on a small-coordinate (mantissa-safe) configuration.");
        }

        // -------------------------------------------------------------------------
        // Hunter: in the cocircular regime at scale, the naive determinant loses
        // the sign.  This documents the predicate's fragility (no Stage A filter
        // yet); if a filtered layer lands and this reaches zero, replace the
        // characterization with the filter's soundness hunt.
        // -------------------------------------------------------------------------
        [Test]
        public void NaiveSign_HasCounterexamples_NearCocircular()
        {
            var rng = new Random(1106);
            int n = 0, flips = 0, strictFlips = 0, zeroMask = 0;
            foreach (var (ax, ay, bx, by, cx, cy, px, py) in Cocircular(500_000, rng))
            {
                int ex = ExactSign(ax, ay, bx, by, cx, cy, px, py);
                int nv = NaiveSign(ax, ay, bx, by, cx, cy, px, py);
                if (nv != ex) flips++;
                if ((nv == 1 && ex < 0) || (nv == -1 && ex > 0)) strictFlips++;
                if (nv == 0 && ex != 0) zeroMask++;
                n++;
            }
            TestContext.WriteLine($"cocircular cases={n:N0}  naive!=exact={flips:N0}  strict sign reversals={strictFlips:N0}  zero-masking={zeroMask:N0}");
            Assert.That(flips, Is.GreaterThan(0),
                "Hunter found no near-cocircular counterexample -- generator not stressing the determinant, " +
                "or the predicate became robust (update this characterization).");
        }

        // -------------------------------------------------------------------------
        [Test]
        public void ExactBigInteger_AgreesWith_Z2IncircleOracle()
        {
            string bin = Environment.GetEnvironmentVariable("ROCQ_REF_BIN");
            if (string.IsNullOrWhiteSpace(bin) || !File.Exists(bin))
                Assert.Ignore("ROCQ_REF_BIN is not set to an existing oracle binary; skipping Z^2 cross-check.");

            var rng = new Random(2106);
            var cases = new List<(long, long, long, long, long, long, long, long)>(60_000);
            foreach (var c in Cocircular(60_000, rng)) cases.Add(c);

            string[] tokens = RunIncircleExact(bin, cases);
            Assert.That(tokens.Length, Is.EqualTo(cases.Count));

            int disagree = 0;
            for (int i = 0; i < cases.Count; i++)
            {
                var (ax, ay, bx, by, cx, cy, px, py) = cases[i];
                int oracle = tokens[i].Trim().ToUpperInvariant() switch { "POS" => 1, "NEG" => -1, "ZERO" => 0, _ => 99 };
                if (oracle != ExactSign(ax, ay, bx, by, cx, cy, px, py)) disagree++;
            }
            Assert.That(disagree, Is.Zero,
                "Z^2 INCIRCLE_EXACT oracle disagreed with the in-process BigInteger oracle -- one of them is wrong.");
        }

        // ---- deterministic ------------------------------------------------------
        [Test]
        public void Sign_PointInsideUnitCircle_IsPos()
        {
            // CCW triangle on the unit-ish circle radius 5; origin is strictly inside.
            var s = InCircle.Sign(new BPoint(5, 0), new BPoint(0, 5), new BPoint(-5, 0), new BPoint(0, 0));
            Assert.That(s, Is.EqualTo(IncircleSign.Pos));
        }

        [Test]
        public void Sign_PointOutside_IsNeg()
        {
            var s = InCircle.Sign(new BPoint(5, 0), new BPoint(0, 5), new BPoint(-5, 0), new BPoint(100, 100));
            Assert.That(s, Is.EqualTo(IncircleSign.Neg));
        }

        [Test]
        public void Sign_PointExactlyOnCircle_IsZero()
        {
            var s = InCircle.Sign(new BPoint(5, 0), new BPoint(0, 5), new BPoint(-5, 0), new BPoint(0, -5));
            Assert.That(s, Is.EqualTo(IncircleSign.Zero));
        }

        // ---- generators / oracle plumbing ---------------------------------------
        private static IEnumerable<(long, long, long, long, long, long, long, long)> Cocircular(int count, Random rng)
        {
            int built = 0;
            while (built < count)
            {
                long s = 1L << rng.Next(4, 24);
                var pa = Circle5[rng.Next(12)]; var pb = Circle5[rng.Next(12)]; var pc = Circle5[rng.Next(12)]; var pp = Circle5[rng.Next(12)];
                long ax = pa.Item1 * s, ay = pa.Item2 * s, bx = pb.Item1 * s, by = pb.Item2 * s, cx = pc.Item1 * s, cy = pc.Item2 * s;
                long px = pp.Item1 * s + rng.Next(-2, 3), py = pp.Item2 * s + rng.Next(-2, 3);
                if ((ax == bx && ay == by) || (ax == cx && ay == cy) || (bx == cx && by == cy)) continue;
                yield return (ax, ay, bx, by, cx, cy, px, py);
                built++;
            }
        }

        private static string[] RunIncircleExact(string bin, IReadOnlyList<(long, long, long, long, long, long, long, long)> cases)
        {
            var psi = new ProcessStartInfo(bin)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var p = Process.Start(psi) ?? throw new InvalidOperationException("could not start oracle");
            var outputs = new List<string>(cases.Count);
            var reader = Task.Run(() => { string line; while ((line = p.StandardOutput.ReadLine()) != null) outputs.Add(line); });
            string err = string.Empty; var errReader = Task.Run(() => err = p.StandardError.ReadToEnd());

            var sb = new StringBuilder(cases.Count * 64);
            static string I(long v) => v.ToString(CultureInfo.InvariantCulture);
            foreach (var (ax, ay, bx, by, cx, cy, px, py) in cases)
                sb.Append("INCIRCLE_EXACT\n")
                  .Append(I(ax)).Append(' ').Append(I(ay)).Append('\n')
                  .Append(I(bx)).Append(' ').Append(I(by)).Append('\n')
                  .Append(I(cx)).Append(' ').Append(I(cy)).Append('\n')
                  .Append(I(px)).Append(' ').Append(I(py)).Append('\n');
            p.StandardInput.Write(sb.ToString());
            p.StandardInput.Close();
            reader.Wait(); errReader.Wait(); p.WaitForExit();
            if (p.ExitCode != 0) throw new InvalidOperationException($"oracle exited {p.ExitCode}: {err}");
            return outputs.ToArray();
        }
    }
}
