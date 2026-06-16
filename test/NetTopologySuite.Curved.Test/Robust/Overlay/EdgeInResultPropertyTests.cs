// =============================================================================
// EdgeInResultPropertyTests
// -----------------------------------------------------------------------------
// EdgeInResult.Evaluate is a total pure-boolean function over the 16-cell
// domain (BooleanOp x in_left x in_right).  These tests exhaustively pin every
// cell against an independent boolean specification, then cross-check the
// algebraic identities the four ops are supposed to satisfy.  (The RocqRef
// differential against the extracted EDGE_IN_RESULT oracle lives in
// EdgeInResultRocqRefTests; this file is the oracle-free completeness check.)
// =============================================================================

using System;
using NetTopologySuite.Robust.Overlay;
using NUnit.Framework;

namespace NetTopologySuite.Test.Robust.Overlay
{
    [TestFixture]
    public class EdgeInResultPropertyTests
    {
        private static readonly BooleanOp[] Ops =
            { BooleanOp.Union, BooleanOp.Intersection, BooleanOp.Difference, BooleanOp.SymDiff };

        private static bool Spec(BooleanOp op, bool l, bool r) => op switch
        {
            BooleanOp.Union        => l || r,
            BooleanOp.Intersection => l && r,
            BooleanOp.Difference   => l && !r,
            BooleanOp.SymDiff      => l != r,
            _ => throw new ArgumentOutOfRangeException(nameof(op)),
        };

        [Test]
        public void Evaluate_MatchesSpec_OnAll16Cells()
        {
            foreach (var op in Ops)
                foreach (bool l in new[] { false, true })
                    foreach (bool r in new[] { false, true })
                        Assert.That(EdgeInResult.Evaluate(op, new EdgeLabel(l, r)), Is.EqualTo(Spec(op, l, r)),
                            $"op={op} l={l} r={r}");
        }

        [Test]
        public void Union_Intersection_SymDiff_AreCommutative_InLeftRight()
        {
            foreach (bool l in new[] { false, true })
                foreach (bool r in new[] { false, true })
                {
                    foreach (var op in new[] { BooleanOp.Union, BooleanOp.Intersection, BooleanOp.SymDiff })
                        Assert.That(EdgeInResult.Evaluate(op, new EdgeLabel(l, r)),
                                    Is.EqualTo(EdgeInResult.Evaluate(op, new EdgeLabel(r, l))), $"op={op} l={l} r={r}");
                }
        }

        [Test]
        public void Difference_IsAsymmetric_AtLeastOnce()
        {
            // Difference is not commutative: (T,F) and (F,T) must differ.
            Assert.That(EdgeInResult.Evaluate(BooleanOp.Difference, new EdgeLabel(true, false)),
                        Is.Not.EqualTo(EdgeInResult.Evaluate(BooleanOp.Difference, new EdgeLabel(false, true))));
        }

        [Test]
        public void SymDiff_Equals_Union_AndNot_Intersection()
        {
            foreach (bool l in new[] { false, true })
                foreach (bool r in new[] { false, true })
                {
                    bool u = EdgeInResult.Evaluate(BooleanOp.Union, new EdgeLabel(l, r));
                    bool i = EdgeInResult.Evaluate(BooleanOp.Intersection, new EdgeLabel(l, r));
                    bool x = EdgeInResult.Evaluate(BooleanOp.SymDiff, new EdgeLabel(l, r));
                    Assert.That(x, Is.EqualTo(u && !i), $"l={l} r={r}");
                }
        }

        [Test]
        public void Union_Equals_DeMorgan_NotBothAbsent()
        {
            foreach (bool l in new[] { false, true })
                foreach (bool r in new[] { false, true })
                    Assert.That(EdgeInResult.Evaluate(BooleanOp.Union, new EdgeLabel(l, r)),
                                Is.EqualTo(!(!l && !r)), $"l={l} r={r}");
        }

        [Test]
        public void UnknownOp_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => EdgeInResult.Evaluate((BooleanOp)999, new EdgeLabel(true, true)));
        }
    }
}
