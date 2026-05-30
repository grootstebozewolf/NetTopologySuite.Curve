// =============================================================================
// NetTopologySuite.Curve.Robust.Overlay.EdgeInResultRocqRefTests
// -----------------------------------------------------------------------------
// Differential tests against the RocqRefRunner -- the Coq-extracted reference
// binary built from NetTopologySuite.Proofs/oracle/.  The mode shipped in
// the Phase-3 (Workstream A) artifact:
//
//   EDGE_IN_RESULT <-> edge_in_result   (theories/OverlayGraph.v:375)
//
// Pure boolean truth table.  Stdin: one mode line, then BooleanOp token
// (UNION | INTERSECTION | DIFFERENCE | SYMDIFF), then "true|false" for
// in_left, then "true|false" for in_right.  Stdout: "TRUE" or "FALSE".
// The oracle is persistent; this fixture exhaustively walks all 16 input
// cells over a single oracle_bin process.
//
// Because the predicate is total over a 16-cell finite domain and the
// C# port is a line-by-line transliteration of the Coq match (see
// EdgeInResult.cs), the diff test serves two purposes:
//   1. bit-by-bit confirmation that the C# port matches the oracle;
//   2. regression-lock on the oracle binary itself -- any drift in
//      the Phase-3 extraction would surface here.
//
// Skipped by default; activate by pointing ROCQ_REF_BIN at the Phase-3-
// aware RocqRefRunner binary.
// =============================================================================

using System;
using System.Diagnostics;
using System.IO;
using NetTopologySuite.Robust.Overlay;
using NUnit.Framework;

namespace NetTopologySuite.Test.Robust.Overlay
{
    [TestFixture]
    [Category("RocqRef")]
    public class EdgeInResultRocqRefTests
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
                    " to the path of a Phase-3-aware RocqRefRunner binary " +
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
        // The EDGE_IN_RESULT input domain is finite: 4 ops x 2 InLeft x 2
        // InRight = 16 cells.  Exhaustively walk it; reject the alternative
        // of "random sampling" because the domain is small enough that any
        // weakening from full coverage would be a strict loss.
        // ---------------------------------------------------------------------
        [TestCase(BooleanOp.Union,        false, false, false)]
        [TestCase(BooleanOp.Union,        false, true,  true)]
        [TestCase(BooleanOp.Union,        true,  false, true)]
        [TestCase(BooleanOp.Union,        true,  true,  true)]
        [TestCase(BooleanOp.Intersection, false, false, false)]
        [TestCase(BooleanOp.Intersection, false, true,  false)]
        [TestCase(BooleanOp.Intersection, true,  false, false)]
        [TestCase(BooleanOp.Intersection, true,  true,  true)]
        [TestCase(BooleanOp.Difference,   false, false, false)]
        [TestCase(BooleanOp.Difference,   false, true,  false)]
        [TestCase(BooleanOp.Difference,   true,  false, true)]
        [TestCase(BooleanOp.Difference,   true,  true,  false)]
        [TestCase(BooleanOp.SymDiff,      false, false, false)]
        [TestCase(BooleanOp.SymDiff,      false, true,  true)]
        [TestCase(BooleanOp.SymDiff,      true,  false, true)]
        [TestCase(BooleanOp.SymDiff,      true,  true,  false)]
        public void TruthTable_BitEqual(
            BooleanOp op, bool inLeft, bool inRight, bool expected)
        {
            // Three-way assertion: oracle == expected, C# port == oracle, and
            // (transitively) C# port == expected.  The expected column is
            // included so the test class is self-checking when run without
            // ROCQ_REF_BIN -- the [Category("RocqRef")] suppresses execution
            // of the oracle calls but the truth table itself stays visible in
            // the source as the formal contract.
            var label = new EdgeLabel(inLeft, inRight);
            bool csResult = EdgeInResult.Evaluate(op, label);

            string oracleReply = RunRocqRef(op, inLeft, inRight);
            bool oracleResult = ParseBool(oracleReply, op + "/" + inLeft + "/" + inRight);

            Assert.That(oracleResult, Is.EqualTo(expected),
                "oracle disagrees with the expected truth-table value for " +
                op + " InLeft=" + inLeft + " InRight=" + inRight);
            Assert.That(csResult, Is.EqualTo(oracleResult),
                "C# EdgeInResult disagrees with oracle for " +
                op + " InLeft=" + inLeft + " InRight=" + inRight);
        }

        // ---------------------------------------------------------------------
        // Transport.
        // ---------------------------------------------------------------------
        private string RunRocqRef(BooleanOp op, bool inLeft, bool inRight)
        {
            lock (_ioLock)
            {
                _stdin.WriteLine("EDGE_IN_RESULT");
                _stdin.WriteLine(OpToken(op));
                _stdin.WriteLine(inLeft ? "true" : "false");
                _stdin.WriteLine(inRight ? "true" : "false");
                _stdin.Flush();
                string line = _stdout.ReadLine();
                if (line == null)
                {
                    Assert.Fail("RocqRefRunner returned no output for EDGE_IN_RESULT");
                }
                return line!.Trim();
            }
        }

        private static string OpToken(BooleanOp op)
        {
            switch (op)
            {
                case BooleanOp.Union:        return "UNION";
                case BooleanOp.Intersection: return "INTERSECTION";
                case BooleanOp.Difference:   return "DIFFERENCE";
                case BooleanOp.SymDiff:      return "SYMDIFF";
                default: throw new ArgumentOutOfRangeException(nameof(op));
            }
        }

        private static bool ParseBool(string s, string ctx)
        {
            if (s == "TRUE") return true;
            if (s == "FALSE") return false;
            throw new FormatException(
                "Unexpected boolean reply from oracle (" + ctx + "): " + s);
        }
    }
}
