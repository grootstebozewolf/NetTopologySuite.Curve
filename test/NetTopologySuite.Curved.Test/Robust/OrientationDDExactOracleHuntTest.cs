using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using static NetTopologySuite.Test.Robust.AdversarialOrientationCases;

namespace NetTopologySuite.Test.Robust
{
    /// <summary>
    /// Differential DD hunt against the proof-extracted <b>Z² exact-integer orientation oracle</b>
    /// (<c>ORIENT_EXACT</c>) shipped in the RocqRefRunner binary from NetTopologySuite.Proofs.
    /// <para/>
    /// This is the "alternative route" to robust soundness: instead of a binary64 forward-error
    /// filter, the oracle evaluates the orientation determinant in exact integer (GMP) arithmetic
    /// over Z², so its sign is ground truth by construction. We stream the same adversarial
    /// near-collinear cases used by <see cref="OrientationDDRobustnessTest"/> through the oracle and
    /// check NTS <c>CGAlgorithmsDD</c> against it — an <i>independent</i> ground truth from the
    /// in-process BigInteger oracle, so the two cross-validate.
    /// <para/>
    /// Gated on the <c>ROCQ_REF_BIN</c> environment variable (path to the oracle binary); the test
    /// is Ignored when it is not set, matching the rest of the RocqRef differential suite. Fetch the
    /// binary with <c>scripts/fetch-oracle.sh</c> and
    /// <c>export ROCQ_REF_BIN=$(scripts/fetch-oracle.sh)</c>.
    /// </summary>
    [TestFixture]
    public class OrientationDDExactOracleHuntTest
    {
        private const int HunterCaseCount = 50_000;
        private const int Seed = 1106;

        [Test]
        public void Dd_HasNoCounterexamples_AgainstZ2ExactOracle()
        {
            string bin = Environment.GetEnvironmentVariable("ROCQ_REF_BIN");
            if (string.IsNullOrWhiteSpace(bin) || !File.Exists(bin))
                Assert.Ignore("ROCQ_REF_BIN is not set to an existing oracle binary; skipping Z² differential. " +
                              "Run: export ROCQ_REF_BIN=$(scripts/fetch-oracle.sh)");

            var cases = new List<OrientCase>(HunterCaseCount);
            foreach (var c in NearCollinear(HunterCaseCount, Seed))
                cases.Add(c);

            string[] oracleSigns = RunOrientExact(bin, cases);
            Assert.That(oracleSigns.Length, Is.EqualTo(cases.Count),
                $"oracle returned {oracleSigns.Length} lines for {cases.Count} cases.");

            int ddFailures = 0, naiveFailures = 0, oracleVsBigInt = 0;
            OrientCase firstDdFail = default;

            for (int i = 0; i < cases.Count; i++)
            {
                var c = cases[i];
                int oracle = ParseSign(oracleSigns[i]);

                // Cross-validate the extracted Z² oracle against the construction / BigInteger oracle.
                if (oracle != c.ExactSign) oracleVsBigInt++;

                if (DdOrientation(c.P1x, c.P1y, c.P2x, c.P2y, c.Qx, c.Qy) != oracle)
                {
                    if (ddFailures == 0) firstDdFail = c;
                    ddFailures++;
                }
                if (NaiveOrientation(c.P1x, c.P1y, c.P2x, c.P2y, c.Qx, c.Qy) != oracle) naiveFailures++;
            }

            TestContext.WriteLine(
                $"cases={cases.Count:N0}  naive flips={naiveFailures:N0}  DD flips={ddFailures:N0}  " +
                $"oracle-vs-BigInteger disagreements={oracleVsBigInt:N0}");

            Assert.Multiple(() =>
            {
                Assert.That(oracleVsBigInt, Is.Zero,
                    "The extracted Z² oracle disagreed with the in-process BigInteger oracle — one of them is wrong.");
                Assert.That(ddFailures, Is.Zero,
                    $"DD orientation disagreed with the Z² oracle; first counterexample: {firstDdFail}");
                Assert.That(naiveFailures, Is.GreaterThan(0),
                    "Generator was vacuous — the naive predicate found no failures against the Z² oracle.");
            });
        }

        /// <summary>
        /// Streams every case to the oracle in <c>ORIENT_EXACT</c> mode and returns one sign token
        /// per case. Reads stdout on a background task while writing stdin to avoid pipe deadlock.
        /// </summary>
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

            if (!reader.Wait(TimeSpan.FromMinutes(2)) || !p.WaitForExit(120_000))
            {
                try { p.Kill(true); } catch { /* ignore */ }
                Assert.Fail("oracle did not complete within the timeout.");
            }
            errReader.Wait(TimeSpan.FromSeconds(5));
            if (p.ExitCode != 0)
                Assert.Fail($"oracle exited with code {p.ExitCode}. stderr: {stderr}");

            return outputs.ToArray();
        }

        private static int ParseSign(string line)
        {
            // ORIENT_EXACT emits a bare POS / NEG / ZERO token.
            ReadOnlySpan<char> s = line.AsSpan().Trim();
            int sp = s.IndexOf(' ');
            if (sp >= 0) s = s[..sp];
            if (s.Equals("POS", StringComparison.OrdinalIgnoreCase)) return 1;
            if (s.Equals("NEG", StringComparison.OrdinalIgnoreCase)) return -1;
            if (s.Equals("ZERO", StringComparison.OrdinalIgnoreCase)) return 0;
            throw new FormatException($"unexpected oracle sign token: '{line}'");
        }
    }
}
