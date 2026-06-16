// =============================================================================
// RobustOrientationHunterTests
// -----------------------------------------------------------------------------
// Counterexample hunters for the orientation layer.  Two independent ground
// truths:
//
//   * an in-process exact BigInteger oracle (always runs), and
//   * the proof-extracted Z^2 exact-integer oracle ORIENT_EXACT shipped in the
//     RocqRefRunner binary (gated on ROCQ_REF_BIN).
//
// What is actually guaranteed by the Shewchuk Stage A filter
// (RobustOrientation.SignFiltered), and therefore what we hunt:
//
//   SOUNDNESS  -- a *strict* result is never wrong: SignFiltered returns Pos
//                 only when the true determinant is > 0, and Neg only when it
//                 is < 0.  It is allowed to return Uncertain (within the error
//                 bound) or Zero (the naive cross product cancelled to exactly
//                 +0.0) on a hard case, but it must NEVER commit the opposite
//                 strict sign.  This is the property the type exists to protect.
//
//   Zero is NOT a claim of true collinearity -- on the overflow-scale
//   Diophantine cases below the naive products cancel to exactly 0 while the
//   true determinant is +/-1, so SignFiltered returns Zero on a non-collinear
//   triple.  Resolving that masking is Stage B/C/D work; here we only
//   *characterise* it (zeroMask count), we do not treat it as a failure.
//
//   NON-VACUITY -- the filter must be shown to do real work: on these cases it
//                 downgrades naive non-zero commitments to Uncertain
//                 (uncertain > 0), and the unfiltered naive predicate disagrees
//                 with exact truth on a large fraction (naiveDisagree > 0).
// =============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NetTopologySuite.Robust.Orientation;
using NUnit.Framework;
using static NetTopologySuite.Test.Robust.Orientation.AdversarialOrientationCases;

namespace NetTopologySuite.Test.Robust.Orientation
{
    [TestFixture]
    public class RobustOrientationHunterTests
    {
        private const int Seed = 1106; // deterministic; named after locationtech/jts#1106.

        private static int NaiveSignInt(OrientSign s) => s switch
        {
            OrientSign.Pos => 1,
            OrientSign.Neg => -1,
            OrientSign.Zero => 0,
            _ => 2, // Nan -- impossible on the all-finite integer cases below.
        };

        // A strict commitment that contradicts the exact strict sign.
        private static bool StrictFlip(OrientSignRobust filtered, int exact) =>
            (filtered == OrientSignRobust.Pos && exact <= 0) ||
            (filtered == OrientSignRobust.Neg && exact >= 0);

        // -------------------------------------------------------------------------
        // Hunt 1: soundness against the in-process BigInteger oracle, near-collinear.
        // -------------------------------------------------------------------------
        [Test]
        public void SignFiltered_NeverFlipsStrictSign_AgainstExactBigInteger()
        {
            const int caseCount = 100_000;

            int strictFlips = 0, uncertain = 0, zeroMask = 0, naiveDisagree = 0;
            OrientCase firstFlip = default;

            foreach (var c in NearCollinear(caseCount, Seed))
            {
                int exact = ExactOrientation(c.P1x, c.P1y, c.P2x, c.P2y, c.Qx, c.Qy);
                Assert.That(exact, Is.EqualTo(c.ExactSign), "BigInteger oracle disagreed with construction.");

                var filtered = RobustOrientation.SignFiltered(c.P0, c.P1, c.Q);
                if (filtered == OrientSignRobust.Uncertain) uncertain++;
                if (filtered == OrientSignRobust.Zero && exact != 0) zeroMask++;
                if (StrictFlip(filtered, exact)) { if (strictFlips == 0) firstFlip = c; strictFlips++; }

                if (NaiveSignInt(RobustOrientation.Sign(c.P0, c.P1, c.Q)) != exact) naiveDisagree++;
            }

            TestContext.WriteLine(
                $"cases={caseCount:N0}  STRICT flips={strictFlips:N0}  filter downgrades (Uncertain)={uncertain:N0}  " +
                $"Stage-A zero-masking={zeroMask:N0}  naive disagreements={naiveDisagree:N0}");

            Assert.Multiple(() =>
            {
                Assert.That(strictFlips, Is.Zero,
                    $"SignFiltered committed the opposite strict sign; first counterexample: {firstFlip}");
                Assert.That(uncertain, Is.GreaterThan(0),
                    "Filter never downgraded a naive non-zero result -- the hunt is not exercising the error bound.");
                Assert.That(naiveDisagree, Is.GreaterThan(0),
                    "Generator was vacuous -- the unfiltered naive predicate agreed with exact truth everywhere.");
            });
        }

        // -------------------------------------------------------------------------
        // Hunt 2: same soundness on minimal-determinant cases (|det| in {0,1}).
        // -------------------------------------------------------------------------
        [Test]
        public void SignFiltered_NeverFlipsStrictSign_OnMinimalDeterminant()
        {
            const int caseCount = 20_000;

            int strictFlips = 0, naiveDisagree = 0;
            foreach (var c in MinimalDeterminant(caseCount, Seed + 1))
            {
                int exact = ExactOrientation(c.P1x, c.P1y, c.P2x, c.P2y, c.Qx, c.Qy);
                if (StrictFlip(RobustOrientation.SignFiltered(c.P0, c.P1, c.Q), exact)) strictFlips++;
                if (NaiveSignInt(RobustOrientation.Sign(c.P0, c.P1, c.Q)) != exact) naiveDisagree++;
            }

            Assert.Multiple(() =>
            {
                Assert.That(strictFlips, Is.Zero, "SignFiltered flipped a strict sign on a minimal-determinant case.");
                Assert.That(naiveDisagree, Is.GreaterThan(0),
                    "Even at overflow scale the naive predicate agreed everywhere -- construction too easy.");
            });
        }

        // -------------------------------------------------------------------------
        // Hunt 3: cross-validate the extracted Z^2 oracle against BigInteger, then
        // re-run the soundness hunt against that independent ground truth.
        // -------------------------------------------------------------------------
        [Test]
        public void SignFiltered_NeverFlipsStrictSign_AgainstZ2Oracle()
        {
            string bin = Environment.GetEnvironmentVariable("ROCQ_REF_BIN");
            if (string.IsNullOrWhiteSpace(bin) || !File.Exists(bin))
                Assert.Ignore("ROCQ_REF_BIN is not set to an existing oracle binary; skipping Z^2 differential.");

            const int caseCount = 50_000;
            var cases = new List<OrientCase>(caseCount);
            foreach (var c in NearCollinear(caseCount, Seed + 2))
                cases.Add(c);

            string[] oracleSigns = RunOrientExact(bin, cases);
            Assert.That(oracleSigns.Length, Is.EqualTo(cases.Count),
                $"oracle returned {oracleSigns.Length} lines for {cases.Count} cases.");

            int oracleVsBigInt = 0, strictFlips = 0, uncertain = 0;
            for (int i = 0; i < cases.Count; i++)
            {
                var c = cases[i];
                int oracle = ParseSign(oracleSigns[i]);
                if (oracle != ExactOrientation(c.P1x, c.P1y, c.P2x, c.P2y, c.Qx, c.Qy)) oracleVsBigInt++;

                var filtered = RobustOrientation.SignFiltered(c.P0, c.P1, c.Q);
                if (filtered == OrientSignRobust.Uncertain) uncertain++;
                if (StrictFlip(filtered, oracle)) strictFlips++;
            }

            Assert.Multiple(() =>
            {
                Assert.That(oracleVsBigInt, Is.Zero,
                    "Extracted Z^2 oracle disagreed with the in-process BigInteger oracle -- one of them is wrong.");
                Assert.That(strictFlips, Is.Zero, "SignFiltered committed the opposite strict sign vs the Z^2 oracle.");
                Assert.That(uncertain, Is.GreaterThan(0), "Filter never downgraded against the Z^2 oracle cases.");
            });
        }

        // ---- oracle plumbing -------------------------------------------------------------------

        private static int ParseSign(string token) => token.Trim().ToUpperInvariant() switch
        {
            "POS" => 1,
            "NEG" => -1,
            "ZERO" => 0,
            var other => throw new FormatException($"unexpected ORIENT_EXACT token: '{other}'"),
        };

        private static string[] RunOrientExact(string bin, IReadOnlyList<OrientCase> cases)
        {
            var psi = new ProcessStartInfo(bin)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var p = Process.Start(psi) ?? throw new InvalidOperationException($"could not start oracle: {bin}");

            var outputs = new List<string>(cases.Count);
            var reader = Task.Run(() =>
            {
                string line;
                while ((line = p.StandardOutput.ReadLine()) != null)
                    outputs.Add(line);
            });
            string stderr = string.Empty;
            var errReader = Task.Run(() => stderr = p.StandardError.ReadToEnd());

            var sb = new StringBuilder(cases.Count * 48);
            static string I(double v) => ((long)v).ToString(CultureInfo.InvariantCulture);
            foreach (var c in cases)
            {
                sb.Append("ORIENT_EXACT\n")
                  .Append(I(c.P1x)).Append(' ').Append(I(c.P1y)).Append('\n')
                  .Append(I(c.P2x)).Append(' ').Append(I(c.P2y)).Append('\n')
                  .Append(I(c.Qx)).Append(' ').Append(I(c.Qy)).Append('\n');
            }
            p.StandardInput.Write(sb.ToString());
            p.StandardInput.Close();

            reader.Wait();
            errReader.Wait();
            p.WaitForExit();
            if (p.ExitCode != 0)
                throw new InvalidOperationException($"oracle exited with code {p.ExitCode}: {stderr}");

            return outputs.ToArray();
        }
    }
}
