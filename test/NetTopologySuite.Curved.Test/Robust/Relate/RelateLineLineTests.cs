// =============================================================================
// NetTopologySuite.Curve.Robust.Relate.RelateLineLineTests
// -----------------------------------------------------------------------------
// Mirrors predicate lemmas on pinned matrices from
//   theories/RelateLineLine.v in NetTopologySuite.Proofs (issue #67 S3).
// =============================================================================

using NetTopologySuite.Robust.Relate;
using NUnit.Framework;

namespace NetTopologySuite.Test.Robust.Relate
{
    [TestFixture]
    public class RelateLineLineTests
    {
        // -----------------------------------------------------------------
        // Witness matrices (ll_matrix_disjoint_witness, etc.)
        // -----------------------------------------------------------------
        [Test]
        public void MatrixDisjoint_IsDisjoint()
        {
            Assert.That(DE9IM.ImDisjoint(RelateLineLine.MatrixDisjoint), Is.True);
        }

        [Test]
        public void MatrixPointIi_IsIntersectsAndCrosses()
        {
            Assert.That(DE9IM.ImIntersects(RelateLineLine.MatrixPointIi), Is.True);
            Assert.That(DE9IM.ImCrosses(RelateLineLine.MatrixPointIi), Is.True);
        }

        [Test]
        public void MatrixOverlapIi_IsOverlaps()
        {
            Assert.That(DE9IM.ImOverlaps(RelateLineLine.MatrixOverlapIi), Is.True);
        }

        // -----------------------------------------------------------------
        // Romanschek paper test predicate lemmas.
        // -----------------------------------------------------------------
        [Test]
        public void PaperTest6_Intersects_NotCrosses()
        {
            Assert.That(DE9IM.ImIntersects(RelateLineLine.PaperTest6), Is.True);
            Assert.That(DE9IM.ImCrosses(RelateLineLine.PaperTest6), Is.False);
        }

        [Test]
        public void PaperTest6_IiDiffersFromPointWitness()
        {
            Assert.That(
                RelateLineLine.PaperTest6.Ii,
                Is.Not.EqualTo(RelateLineLine.MatrixPointIi.Ii));
        }

        [Test]
        public void PaperTest7_IntersectsAndOverlaps()
        {
            Assert.That(DE9IM.ImIntersects(RelateLineLine.PaperTest7), Is.True);
            Assert.That(DE9IM.ImOverlaps(RelateLineLine.PaperTest7), Is.True);
        }

        [Test]
        public void PaperTest7_AgreesOverlapWitnessCore()
        {
            Assert.That(RelateLineLine.PaperTest7.Ii, Is.EqualTo(RelateLineLine.MatrixOverlapIi.Ii));
            Assert.That(RelateLineLine.PaperTest7.Bb, Is.EqualTo(RelateLineLine.MatrixOverlapIi.Bb));
        }

        [Test]
        public void PaperTest8_Intersects()
        {
            Assert.That(DE9IM.ImIntersects(RelateLineLine.PaperTest8), Is.True);
        }

        [Test]
        public void PaperTest9_IntersectsAndOverlaps()
        {
            Assert.That(DE9IM.ImIntersects(RelateLineLine.PaperTest9), Is.True);
            Assert.That(DE9IM.ImOverlaps(RelateLineLine.PaperTest9), Is.True);
        }

        [Test]
        public void PaperTest10_Intersects_NotDisjoint()
        {
            Assert.That(DE9IM.ImIntersects(RelateLineLine.PaperTest10), Is.True);
            Assert.That(DE9IM.ImDisjoint(RelateLineLine.PaperTest10), Is.False);
        }

        [Test]
        public void PaperTest13_IntersectsAndCrosses()
        {
            Assert.That(DE9IM.ImIntersects(RelateLineLine.PaperTest13), Is.True);
            Assert.That(DE9IM.ImCrosses(RelateLineLine.PaperTest13), Is.True);
        }

        [Test]
        public void PaperTest13_AgreesPointWitnessIi()
        {
            Assert.That(
                RelateLineLine.PaperTest13.Ii,
                Is.EqualTo(RelateLineLine.MatrixPointIi.Ii));
        }
    }
}