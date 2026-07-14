// =============================================================================
// NetTopologySuite.Curve.Robust.Relate.RelateLineLine
// -----------------------------------------------------------------------------
// C# transliteration of pinned line-line DE-9IM matrices from
//   theories/RelateLineLine.v in NetTopologySuite.Proofs (issue #67 S3 seed).
//
// Witness matrices and Romanschek et al. (IJGI 2021) Table 5/6 paper tests.
// Predicate evaluation uses DE9IM.v algebra — not a RelateNG implementation.
// =============================================================================

namespace NetTopologySuite.Robust.Relate
{
    /// <summary>
    /// Line-line DE-9IM witness and paper-test matrices from
    /// <c>theories/RelateLineLine.v</c>.
    /// </summary>
    public static class RelateLineLine
    {
        public static readonly IntersectionMatrix MatrixDisjoint =
            new IntersectionMatrix(
                DimValue.Empty, DimValue.Empty, DimValue.Empty,
                DimValue.Empty, DimValue.Empty, DimValue.Empty,
                DimValue.Empty, DimValue.Empty, DimValue.Empty);

        public static readonly IntersectionMatrix MatrixPointIi =
            new IntersectionMatrix(
                DimValue.Of(0), DimValue.Empty, DimValue.Empty,
                DimValue.Empty, DimValue.Empty, DimValue.Empty,
                DimValue.Empty, DimValue.Empty, DimValue.Empty);

        public static readonly IntersectionMatrix MatrixOverlapIi =
            new IntersectionMatrix(
                DimValue.Of(1), DimValue.Empty, DimValue.Empty,
                DimValue.Empty, DimValue.Of(0), DimValue.Empty,
                DimValue.Empty, DimValue.Empty, DimValue.Of(0));

        /// <summary>Romanschek test 6 — matrix <c>FF1FF0102</c>.</summary>
        public static readonly IntersectionMatrix PaperTest6 =
            IntersectionMatrix.FromNtsString("FF1FF0102");

        /// <summary>Romanschek test 7 — matrix <c>1FFF0FFF2</c>.</summary>
        public static readonly IntersectionMatrix PaperTest7 =
            IntersectionMatrix.FromNtsString("1FFF0FFF2");

        /// <summary>Romanschek test 8 — matrix <c>101FF0FF2</c>.</summary>
        public static readonly IntersectionMatrix PaperTest8 =
            IntersectionMatrix.FromNtsString("101FF0FF2");

        /// <summary>Romanschek test 9 — matrix <c>101F00FF2</c>.</summary>
        public static readonly IntersectionMatrix PaperTest9 =
            IntersectionMatrix.FromNtsString("101F00FF2");

        /// <summary>Romanschek test 10 — matrix <c>FF10F0102</c>.</summary>
        public static readonly IntersectionMatrix PaperTest10 =
            IntersectionMatrix.FromNtsString("FF10F0102");

        /// <summary>Romanschek test 13 — matrix <c>0F1FF0102</c>.</summary>
        public static readonly IntersectionMatrix PaperTest13 =
            IntersectionMatrix.FromNtsString("0F1FF0102");
    }
}