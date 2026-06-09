// =============================================================================
// NetTopologySuite.Curve.Robust.Relate.RelateIntDetBoundTests
// -----------------------------------------------------------------------------
// Mirrors the Qed-closed theorems from
//   theories/RelateIntDetBound.v in NetTopologySuite.Proofs.
// =============================================================================

using NetTopologySuite.Robust.Relate;
using NUnit.Framework;

namespace NetTopologySuite.Test.Robust.Relate
{
    [TestFixture]
    public class RelateIntDetBoundTests
    {
        // -----------------------------------------------------------------
        // Mirrors idet_max_witness / idet_min_witness.
        // -----------------------------------------------------------------
        [Test]
        public void IdetMaxWitness_EqualsCmaxSquared()
        {
            long expected = RelateIntDetBound.Cmax * RelateIntDetBound.Cmax;
            Assert.That(RelateIntDetBound.IdetMaxWitness(), Is.EqualTo(expected));
        }

        [Test]
        public void IdetMinWitness_EqualsNegCmaxSquared()
        {
            long expected = -(RelateIntDetBound.Cmax * RelateIntDetBound.Cmax);
            Assert.That(RelateIntDetBound.IdetMinWitness(), Is.EqualTo(expected));
        }

        // -----------------------------------------------------------------
        // Mirrors cmax_sq_le_int64 / cmax_succ_sq_gt_int64.
        // -----------------------------------------------------------------
        [Test]
        public void CmaxSq_FitsInInt64()
        {
            Assert.That(RelateIntDetBound.CmaxSqLeInt64(), Is.True);
        }

        [Test]
        public void CmaxSuccSq_OverflowsInt64()
        {
            Assert.That(RelateIntDetBound.CmaxSuccSqGtInt64(), Is.True);
        }

        // -----------------------------------------------------------------
        // Mirrors idet_range_tight_at_int64_edge corollary.
        // -----------------------------------------------------------------
        [Test]
        public void IdetRange_TightAtInt64Edge()
        {
            long cmaxSq = RelateIntDetBound.Cmax * RelateIntDetBound.Cmax;
            Assert.That(RelateIntDetBound.IdetMaxWitness(), Is.EqualTo(cmaxSq));
            Assert.That(cmaxSq, Is.LessThanOrEqualTo(RelateIntDetBound.Int64Max));
            Assert.That(RelateIntDetBound.CmaxSuccSqGtInt64(), Is.True);
        }

        // -----------------------------------------------------------------
        // Mirrors idet_fits_int64_for_int32_coords at boundary corners.
        // -----------------------------------------------------------------
        [Test]
        public void IdetFitsInt64_AtInt32MaxCorners()
        {
            long m = RelateIntDetBound.I32Max;
            Assert.That(
                RelateIntDetBound.IdetFitsInt64ForInt32Coords(0, 0, m, 0, 0, m),
                Is.True);
            Assert.That(
                RelateIntDetBound.IdetFitsInt64ForInt32Coords(0, 0, 0, m, m, 0),
                Is.True);
            Assert.That(
                RelateIntDetBound.IdetFitsInt64ForInt32Coords(m, m, m, m, m, m),
                Is.True);
        }

        // -----------------------------------------------------------------
        // Mirrors idet_abs_le_2sq — algebraic bound on random samples.
        // -----------------------------------------------------------------
        [Test]
        public void IdetAbsLe2Sq_HoldsOnRandomCoords(
            [Random(0, int.MaxValue, 60)] int seed)
        {
            var rng = new System.Random(seed);
            long c = RelateIntDetBound.I32Max;
            long ax = NextCoord(rng, c);
            long ay = NextCoord(rng, c);
            long bx = NextCoord(rng, c);
            long by = NextCoord(rng, c);
            long cx = NextCoord(rng, c);
            long cy = NextCoord(rng, c);

            Assert.That(
                RelateIntDetBound.IdetAbsLe2Sq(c, ax, ay, bx, by, cx, cy),
                Is.True,
                $"|idet| > 2*c^2 at ({ax},{ay}),({bx},{by}),({cx},{cy})");
        }

        private static long NextCoord(System.Random rng, long c)
        {
            // I32Max exceeds int.MaxValue; draw via ulong modulo.
            ulong span = (ulong)c + 1UL;
            return (long)(rng.NextInt64((long)span));
        }

        // -----------------------------------------------------------------
        // Paper Equation (2) sanity: unit right triangle has det = 1.
        // -----------------------------------------------------------------
        [Test]
        public void Idet_UnitRightTriangle_IsOne()
        {
            Assert.That(RelateIntDetBound.Idet(0, 0, 1, 0, 0, 1), Is.EqualTo(1L));
        }

        [Test]
        public void Idet_Collinear_IsZero()
        {
            Assert.That(RelateIntDetBound.Idet(0, 0, 1, 0, 2, 0), Is.EqualTo(0L));
        }
    }
}