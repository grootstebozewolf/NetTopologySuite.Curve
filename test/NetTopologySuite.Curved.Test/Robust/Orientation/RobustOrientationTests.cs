// =============================================================================
// NetTopologySuite.Curve.Robust.Orientation.RobustOrientationTests
// -----------------------------------------------------------------------------
// Mirrors the Qed-closed structural lemmas from the companion Coq corpus
// (`theories-flocq/Orientation_b64.v` in NetTopologySuite.Proofs).  Plus a
// handful of well-known orientation cases as executable expectations.
//
// The Coq side is intentionally sparse here -- the arithmetic identities
// (antisymmetry, cyclic permutation, translation invariance) hold in IR
// but in binary64 require the no-overflow-precondition machinery that's
// deferred to the next slice.  These C# tests pin the executable
// behaviour today so any future implementation refactor diff-breaks
// before the spec breaks.
// =============================================================================

using NetTopologySuite.Robust.Orientation;
using NetTopologySuite.Robust.Simplify;
using NUnit.Framework;

namespace NetTopologySuite.Test.Robust.Orientation
{
    [TestFixture]
    public class RobustOrientationTests
    {
        private static BPoint P(double x, double y) => new BPoint(x, y);

        // -----------------------------------------------------------------
        // Mirrors Coq's `orient_sign_distinct`: the four constructors are
        // pairwise distinct.  In C# that's enum identity, so the test is
        // structural rather than algorithmic, but pinning it here matches
        // the Coq spec one-for-one.
        // -----------------------------------------------------------------
        [Test]
        public void OrientSign_FourConstructors_AreDistinct()
        {
            Assert.That(OrientSign.Pos,  Is.Not.EqualTo(OrientSign.Neg));
            Assert.That(OrientSign.Pos,  Is.Not.EqualTo(OrientSign.Zero));
            Assert.That(OrientSign.Pos,  Is.Not.EqualTo(OrientSign.Nan));
            Assert.That(OrientSign.Neg,  Is.Not.EqualTo(OrientSign.Zero));
            Assert.That(OrientSign.Neg,  Is.Not.EqualTo(OrientSign.Nan));
            Assert.That(OrientSign.Zero, Is.Not.EqualTo(OrientSign.Nan));
        }

        // -----------------------------------------------------------------
        // Mirrors `b64_orient_sign_total`: the function returns a value in
        // every case.  C# enums already enforce this at the type level, so
        // here we just exercise it across a handful of inputs.
        // -----------------------------------------------------------------
        [Test]
        public void Sign_AlwaysReturnsADefinedConstructor(
            [Values(0.0, 1.0, -1.0)] double x0,
            [Values(0.0, 1.0)]       double x1)
        {
            var p0 = P(x0, 0.0);
            var p1 = P(x1, 1.0);
            var q  = P(2.0, 2.0);
            var s = RobustOrientation.Sign(p0, p1, q);
            Assert.That(s, Is.AnyOf(
                OrientSign.Pos, OrientSign.Neg, OrientSign.Zero, OrientSign.Nan));
        }

        // -----------------------------------------------------------------
        // Standard orientation cases.  Match the Coq spec's expected
        // behaviour on simple integer-coord triangles.
        // -----------------------------------------------------------------
        [Test]
        public void Sign_CCWTriangle_IsPos()
        {
            var s = RobustOrientation.Sign(P(0, 0), P(1, 0), P(0, 1));
            Assert.That(s, Is.EqualTo(OrientSign.Pos));
        }

        [Test]
        public void Sign_CWTriangle_IsNeg()
        {
            var s = RobustOrientation.Sign(P(0, 0), P(0, 1), P(1, 0));
            Assert.That(s, Is.EqualTo(OrientSign.Neg));
        }

        [Test]
        public void Sign_CollinearPoints_IsZero()
        {
            var s = RobustOrientation.Sign(P(0, 0), P(1, 0), P(2, 0));
            Assert.That(s, Is.EqualTo(OrientSign.Zero));
        }

        [Test]
        public void Sign_DegenerateBaseSameTwoPoints_IsZero()
        {
            // cross(A, A, Q) = 0 for any Q when coords are finite (the Coq
            // `cross_degenerate_base` lemma in the R-valued Orientation.v).
            // Holds bit-exactly in binary64 because x - x = +0 for finite x.
            var s = RobustOrientation.Sign(P(3, 4), P(3, 4), P(7, 9));
            Assert.That(s, Is.EqualTo(OrientSign.Zero));
        }

        [Test]
        public void Sign_VertexCoincidesWithP0_IsZero()
        {
            // cross(A, B, A) = 0; Coq's `cross_at_P0_is_collinear`.
            var s = RobustOrientation.Sign(P(3, 4), P(7, 9), P(3, 4));
            Assert.That(s, Is.EqualTo(OrientSign.Zero));
        }

        [Test]
        public void Sign_VertexCoincidesWithP1_IsZero()
        {
            // cross(A, B, B) = 0; Coq's `cross_at_P1_is_collinear`.
            var s = RobustOrientation.Sign(P(3, 4), P(7, 9), P(7, 9));
            Assert.That(s, Is.EqualTo(OrientSign.Zero));
        }

        // -----------------------------------------------------------------
        // NaN-safety.  Any NaN coordinate makes the cross-product yield NaN
        // (NaN propagates through `+`, `-`, `*`); the Sign decoder maps that
        // to `OrientSign.Nan` rather than silently picking Pos/Neg/Zero.
        // -----------------------------------------------------------------
        [Test]
        public void Sign_AnyNaNCoordinate_IsNan(
            [Values(0, 1, 2)] int nanPosition,
            [Values(0, 1)]    int nanCoord)
        {
            var pts = new[] { P(0, 0), P(1, 0), P(0, 1) };
            double nx = nanCoord == 0 ? double.NaN : pts[nanPosition].X;
            double ny = nanCoord == 1 ? double.NaN : pts[nanPosition].Y;
            pts[nanPosition] = new BPoint(nx, ny);

            var s = RobustOrientation.Sign(pts[0], pts[1], pts[2]);
            Assert.That(s, Is.EqualTo(OrientSign.Nan));
        }

        // -----------------------------------------------------------------
        // Orient2d signed area magnitude on the canonical CCW unit triangle.
        // Pinned as a guardrail: any refactor that breaks the formula
        // (e.g. dropping a minus sign or swapping a coordinate) flips this.
        // -----------------------------------------------------------------
        [Test]
        public void Orient2d_UnitTriangle_IsExactlyOne()
        {
            double v = RobustOrientation.Orient2d(P(0, 0), P(1, 0), P(0, 1));
            Assert.That(v, Is.EqualTo(1.0));
        }

        [Test]
        public void Orient2d_OnCollinear_IsExactlyZero()
        {
            double v = RobustOrientation.Orient2d(P(0, 0), P(1, 0), P(2, 0));
            Assert.That(v, Is.EqualTo(0.0));
        }

        // ---------------------------------------------------------------
        // SignFiltered (Shewchuk Stage A).
        // ---------------------------------------------------------------

        [Test]
        public void SignFiltered_CCWTriangle_IsPos()
        {
            var s = RobustOrientation.SignFiltered(P(0, 0), P(1, 0), P(0, 1));
            Assert.That(s, Is.EqualTo(OrientSignRobust.Pos));
        }

        [Test]
        public void SignFiltered_CWTriangle_IsNeg()
        {
            var s = RobustOrientation.SignFiltered(P(0, 0), P(0, 1), P(1, 0));
            Assert.That(s, Is.EqualTo(OrientSignRobust.Neg));
        }

        [Test]
        public void SignFiltered_ExactlyCollinear_IsZero()
        {
            var s = RobustOrientation.SignFiltered(P(0, 0), P(1, 0), P(2, 0));
            Assert.That(s, Is.EqualTo(OrientSignRobust.Zero));
        }

        [Test]
        public void SignFiltered_AnyNaN_IsNan(
            [Values(0, 1, 2)] int nanPosition,
            [Values(0, 1)]    int nanCoord)
        {
            var pts = new[] { P(0, 0), P(1, 0), P(0, 1) };
            double nx = nanCoord == 0 ? double.NaN : pts[nanPosition].X;
            double ny = nanCoord == 1 ? double.NaN : pts[nanPosition].Y;
            pts[nanPosition] = new BPoint(nx, ny);

            var s = RobustOrientation.SignFiltered(pts[0], pts[1], pts[2]);
            Assert.That(s, Is.EqualTo(OrientSignRobust.Nan));
        }

        // -----------------------------------------------------------------
        // Adversarial: a triangle constructed to land just inside the
        // Stage A error bound so the filter must say Uncertain rather
        // than flip a sign.  Constructed by deviating one coordinate of
        // a collinear triangle by exactly 1 ULP at 1.0 (= 2^-52), which
        // gives a `det` of magnitude 2^-52 against a `detsum` of 2.  The
        // Stage A bound is `(3 + 16*eps)*eps * detsum` ~ 1.33e-15, larger
        // than |det| = 2.22e-16 -- so the filter must say Uncertain.
        // -----------------------------------------------------------------
        [Test]
        public void SignFiltered_BelowFilterThreshold_IsUncertain()
        {
            var p0 = P(0.0, 0.0);
            var p1 = P(1.0, 1.0);
            // 1 + 2^-52 = next double up from 1.0.
            double oneUp = System.Math.BitIncrement(1.0);
            var q  = P(1.0, oneUp);

            var s = RobustOrientation.SignFiltered(p0, p1, q);
            Assert.That(s, Is.EqualTo(OrientSignRobust.Uncertain));
        }

        // Naive Sign and SignFiltered must agree on every non-Uncertain,
        // non-NaN case that's well outside the filter threshold.  Pinning
        // here so a future refactor that diverges them flips this test.
        [Test]
        public void SignFiltered_AgreesWithSign_FarFromFilter()
        {
            // Unit triangle, comfortably outside any rounding regime.
            var p0 = P(0, 0);
            var p1 = P(1, 0);
            var q  = P(0, 1);
            var naive    = RobustOrientation.Sign(p0, p1, q);
            var filtered = RobustOrientation.SignFiltered(p0, p1, q);
            Assert.That((int)filtered, Is.EqualTo((int)naive));
        }
    }
}
