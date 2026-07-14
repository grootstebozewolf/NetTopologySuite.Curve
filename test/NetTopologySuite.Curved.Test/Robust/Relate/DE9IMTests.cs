// =============================================================================
// NetTopologySuite.Curve.Robust.Relate.DE9IMTests
// -----------------------------------------------------------------------------
// Mirrors the Qed-closed structural lemmas and witness matrices from
//   theories/DE9IM.v in NetTopologySuite.Proofs.
// =============================================================================

using NetTopologySuite.Robust.Relate;
using NUnit.Framework;

namespace NetTopologySuite.Test.Robust.Relate
{
    [TestFixture]
    public class DE9IMTests
    {
        // -----------------------------------------------------------------
        // Mirrors disjoint_intersects3_example_holds.
        // Disjoint and intersects_3 are NOT mutually exclusive — a geometry
        // can be disjoint in II/IB/BI/BB yet have IE = 0 (exterior touch).
        // -----------------------------------------------------------------
        [Test]
        public void DisjointIntersects3Example_IsDisjointAndIntersects3()
        {
            var m = DE9IM.DisjointIntersects3Example;
            Assert.That(DE9IM.ImDisjoint(m), Is.True);
            Assert.That(DE9IM.PatIntersects3.Matches(m), Is.True);
            Assert.That(DE9IM.ImIntersects(m), Is.True);
        }

        // -----------------------------------------------------------------
        // Mirrors not_intersects_gap_example_holds.
        // -----------------------------------------------------------------
        [Test]
        public void NotIntersectsGapExample_IsNeitherIntersectsNorDisjoint()
        {
            var m = DE9IM.NotIntersectsGapExample;
            Assert.That(DE9IM.ImIntersects(m), Is.False);
            Assert.That(DE9IM.ImDisjoint(m), Is.False);
        }

        // -----------------------------------------------------------------
        // Mirrors matrix_transpose_twice.
        // -----------------------------------------------------------------
        [Test]
        public void MatrixTranspose_Twice_IsIdentity()
        {
            var m = DE9IM.DisjointIntersects3Example;
            Assert.That(m.Transpose().Transpose(), Is.EqualTo(m));
        }

        // -----------------------------------------------------------------
        // Mirrors matrix_matches_transpose / im_contains_transpose_within.
        // -----------------------------------------------------------------
        [Test]
        public void Contains_Transpose_IsWithin()
        {
            var m = new IntersectionMatrix(
                DimValue.Of(2), DimValue.Empty, DimValue.Of(1),
                DimValue.Empty, DimValue.Empty, DimValue.Empty,
                DimValue.Empty, DimValue.Empty, DimValue.Of(2));

            Assert.That(DE9IM.ImContains(m), Is.True);
            Assert.That(DE9IM.ImWithin(m.Transpose()), Is.True);
        }

        [Test]
        public void PatternMatches_Transpose_IsSymmetric()
        {
            var m = DE9IM.NotIntersectsGapExample;
            Assert.That(
                DE9IM.PatContains.Matches(m),
                Is.EqualTo(DE9IM.PatWithin.Matches(m.Transpose())));
        }

        // -----------------------------------------------------------------
        // Mirrors im_covers_transpose_coveredBy.
        // -----------------------------------------------------------------
        [Test]
        public void Covers_Transpose_IsCoveredBy()
        {
            var m = new IntersectionMatrix(
                DimValue.Of(1), DimValue.Empty, DimValue.Of(0),
                DimValue.Empty, DimValue.Empty, DimValue.Empty,
                DimValue.Empty, DimValue.Empty, DimValue.Of(1));

            Assert.That(DE9IM.ImCovers(m), Is.True);
            Assert.That(DE9IM.ImCoveredBy(m.Transpose()), Is.True);
        }

        // -----------------------------------------------------------------
        // Mirrors predicate_disjoint_not_intersects_partial.
        // -----------------------------------------------------------------
        [Test]
        public void Disjoint_ImpliesNotIntersectsPartial_OnAllFiniteMatrices()
        {
            foreach (var m in SampleMatrices())
            {
                Assert.That(DE9IM.DisjointImpliesNotIntersectsPartial(m), Is.True,
                    "disjoint ⇒ ¬(intersects₀ ∨ intersects₁ ∨ intersects₄) failed");
            }
        }

        // -----------------------------------------------------------------
        // Mirrors im_intersects_not_disjoint_partial.
        // -----------------------------------------------------------------
        [Test]
        public void IntersectsPartial_ImpliesNotDisjoint_OnAllFiniteMatrices()
        {
            foreach (var m in SampleMatrices())
            {
                Assert.That(DE9IM.IntersectsPartialImpliesNotDisjoint(m), Is.True,
                    "intersects₀/₁/₄ ⇒ ¬disjoint failed");
            }
        }

        // -----------------------------------------------------------------
        // Mirrors predicate_contains_transpose_within /
        // predicate_covers_transpose_coveredBy.
        // -----------------------------------------------------------------
        [Test]
        public void PredicateContains_Transpose_IsWithin()
        {
            foreach (var m in SampleMatrices())
            {
                bool contains = DE9IM.PredicateHolds(RelatePredicate.Contains, m);
                bool within   = DE9IM.PredicateHolds(RelatePredicate.Within, m.Transpose());
                Assert.That(contains, Is.EqualTo(within));
            }
        }

        [Test]
        public void PredicateCovers_Transpose_IsCoveredBy()
        {
            foreach (var m in SampleMatrices())
            {
                bool covers    = DE9IM.PredicateHolds(RelatePredicate.Covers, m);
                bool coveredBy = DE9IM.PredicateHolds(RelatePredicate.CoveredBy, m.Transpose());
                Assert.That(covers, Is.EqualTo(coveredBy));
            }
        }

        // -----------------------------------------------------------------
        // PatternChar structural facts (char_false_empty / char_true_nonempty).
        // -----------------------------------------------------------------
        [Test]
        public void PatternFalse_MatchesOnlyEmpty()
        {
            Assert.That(PatternChar.False.Matches(DimValue.Empty), Is.True);
            Assert.That(PatternChar.False.Matches(DimValue.Of(0)), Is.False);
            Assert.That(PatternChar.False.Matches(DimValue.Of(1)), Is.False);
        }

        [Test]
        public void PatternTrue_MatchesOnlyNonempty()
        {
            Assert.That(PatternChar.True.Matches(DimValue.Empty), Is.False);
            Assert.That(PatternChar.True.Matches(DimValue.Of(0)), Is.True);
            Assert.That(PatternChar.True.Matches(DimValue.Of(2)), Is.True);
        }

        // -----------------------------------------------------------------
        // Standard disjoint pattern on the all-F matrix.
        // -----------------------------------------------------------------
        [Test]
        public void AllEmpty_IsDisjoint()
        {
            var m = new IntersectionMatrix(
                DimValue.Empty, DimValue.Empty, DimValue.Empty,
                DimValue.Empty, DimValue.Empty, DimValue.Empty,
                DimValue.Empty, DimValue.Empty, DimValue.Empty);
            Assert.That(DE9IM.ImDisjoint(m), Is.True);
            Assert.That(DE9IM.ImIntersects(m), Is.False);
        }

        // -----------------------------------------------------------------
        // All ten RelatePredicate enum values are total (no throw).
        // -----------------------------------------------------------------
        [Test]
        public void PredicateHolds_AllTenPredicates_AreTotal()
        {
            var m = DE9IM.DisjointIntersects3Example;
            foreach (RelatePredicate p in System.Enum.GetValues(typeof(RelatePredicate)))
            {
                Assert.DoesNotThrow(() => DE9IM.PredicateHolds(p, m));
            }
        }

        // -----------------------------------------------------------------
        // Enumerate the finite {Empty, 0, 1, 2}^9 domain for the structural
        // lemmas (4^9 = 262144 cells — fast enough).
        // -----------------------------------------------------------------
        private static System.Collections.Generic.IEnumerable<IntersectionMatrix> SampleMatrices()
        {
            var dims = new[]
            {
                DimValue.Empty,
                DimValue.Of(0),
                DimValue.Of(1),
                DimValue.Of(2),
            };

            foreach (var ii in dims)
            foreach (var ib in dims)
            foreach (var ie in dims)
            foreach (var bi in dims)
            foreach (var bb in dims)
            foreach (var be in dims)
            foreach (var ei in dims)
            foreach (var eb in dims)
            foreach (var ee in dims)
            {
                yield return new IntersectionMatrix(ii, ib, ie, bi, bb, be, ei, eb, ee);
            }
        }
    }
}