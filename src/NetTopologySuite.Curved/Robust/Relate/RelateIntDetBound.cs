// =============================================================================
// NetTopologySuite.Curve.Robust.Relate.RelateIntDetBound
// -----------------------------------------------------------------------------
// C# transliteration of the integer orientation-determinant bounds from
//   theories/RelateIntDetBound.v in NetTopologySuite.Proofs.
//
// Grounds the Romanschek–Clemen–Huhnt DE-9IM / relate approach (ISPRS IJGI
// 2021, 10, 715): coordinates carried as integers, det(a,b,c) the only
// arithmetic that can overflow.  The Coq module proves:
//   - |idet| <= 2·c² for coords in [0,c]  (algebraic bound)
//   - 32-bit coords => det fits in signed 64-bit
//   - cmax = floor(sqrt(2⁶³−1)) brackets the 64-bit window
//   - tight witnesses at ±cmax²
// =============================================================================

using System;

namespace NetTopologySuite.Robust.Relate
{
    /// <summary>
    /// Integer orientation determinant and overflow-range bounds for the
    /// exact-integer relate pipeline.  Mirrors
    /// <c>theories/RelateIntDetBound.v</c>.
    /// </summary>
    public static class RelateIntDetBound
    {
        /// <summary>Largest signed 32-bit integer (paper's post-translate window).</summary>
        public const long I32Max = (1L << 31) - 1;

        /// <summary>
        /// floor(sqrt(2⁶³−1)) — largest c with c² fitting in signed Int64
        /// (paper Equations (5),(8)).
        /// </summary>
        public const long Cmax = 3_037_000_499L;

        /// <summary>Signed Int64 maximum (2⁶³−1).</summary>
        public const long Int64Max = long.MaxValue;

        /// <summary>
        /// Orientation determinant on integer coordinates (paper Eq. (2)):
        /// (bx−ax)(cy−ay) − (cx−ax)(by−ay).  Mirrors Coq <c>idet</c>.
        /// </summary>
        public static long Idet(long ax, long ay, long bx, long by, long cx, long cy)
        {
            return (bx - ax) * (cy - ay) - (cx - ax) * (by - ay);
        }

        /// <summary>
        /// Algebraic bound: |idet| ≤ 2·c² when every coordinate lies in [0,c].
        /// Mirrors Coq <c>idet_abs_le_2sq</c>.
        /// </summary>
        public static bool IdetAbsLe2Sq(
            long c, long ax, long ay, long bx, long by, long cx, long cy)
        {
            long det = Idet(ax, ay, bx, by, cx, cy);
            long bound = 2L * c * c;
            long abs = det < 0 ? -det : det;
            return abs <= bound;
        }

        /// <summary>
        /// 32-bit coordinate regime: det fits in signed 64-bit.
        /// Mirrors Coq <c>idet_fits_int64_for_int32_coords</c>.
        /// </summary>
        public static bool IdetFitsInt64ForInt32Coords(
            long ax, long ay, long bx, long by, long cx, long cy)
        {
            long det = Idet(ax, ay, bx, by, cx, cy);
            return det >= -Int64Max && det <= Int64Max;
        }

        /// <summary>Coq <c>cmax_sq_le_int64</c>: cmax² ≤ 2⁶³−1.</summary>
        public static bool CmaxSqLeInt64() => Cmax * Cmax <= Int64Max;

        /// <summary>Coq <c>cmax_succ_sq_gt_int64</c>: (cmax+1)² overflows Int64.</summary>
        public static bool CmaxSuccSqGtInt64()
        {
            long s = Cmax + 1;
            return unchecked(s * s) > Int64Max || s * s < 0;
        }

        /// <summary>Coq <c>idet_max_witness</c>: det at (0,0,cmax,0,0,cmax) = cmax².</summary>
        public static long IdetMaxWitness() => Idet(0, 0, Cmax, 0, 0, Cmax);

        /// <summary>Coq <c>idet_min_witness</c>: det at (0,0,0,cmax,cmax,0) = −cmax².</summary>
        public static long IdetMinWitness() => Idet(0, 0, 0, Cmax, Cmax, 0);
    }
}