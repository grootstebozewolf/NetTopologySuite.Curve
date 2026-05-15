// =============================================================================
// NetTopologySuite.Curve.Robust.Intersect.RobustLineIntersector
// -----------------------------------------------------------------------------
// C# transliteration of the Coq segment-pair intersection predicate from
//   theories-flocq/Intersect_b64.v in NetTopologySuite.Proofs.
//
// Four calls to RobustOrientation.SignFiltered + a case dispatch with the
// priority: NaN > Uncertain > same-strict-side rejection > zero-degenerate
// > proper crossing.  This matches the Coq `b64_intersect_sign_filtered`
// dispatch exactly, so the C# port is bit-equal with the Coq-extracted
// reference (validated by the differential test class).
// =============================================================================

using NetTopologySuite.Robust.Orientation;
using NetTopologySuite.Robust.Simplify;

namespace NetTopologySuite.Robust.Intersect
{
    /// <summary>
    /// Segment-pair intersection predicate on binary64 points, with
    /// Shewchuk Stage A filtering.  Mirrors
    /// <c>b64_intersect_sign_filtered</c> in
    /// <c>theories-flocq/Intersect_b64.v</c>.
    /// </summary>
    public static class RobustLineIntersector
    {
        /// <summary>
        /// Five-valued intersection sign for the segments
        /// (<paramref name="p0"/>, <paramref name="p1"/>) and
        /// (<paramref name="q0"/>, <paramref name="q1"/>).  Transliterates
        /// the Coq <c>b64_intersect_sign_filtered</c>.
        /// </summary>
        /// <remarks>
        /// Bit-equal against the Coq-extracted reference on every test
        /// case in <c>RobustLineIntersectorRocqRefTests</c>.  In the integer
        /// regime (<c>|coord| &lt;= 2^25</c>, integer-valued) the Coq proof
        /// guarantees that a <see cref="IntersectSign.None"/> result implies
        /// the underlying R-side segments genuinely do not share a point;
        /// see <c>b64_intersect_sign_filtered_none_sound_small_int</c>.
        /// </remarks>
        public static IntersectSign SignFiltered(
            BPoint p0, BPoint p1, BPoint q0, BPoint q1)
        {
            OrientSignRobust pq0 = RobustOrientation.SignFiltered(p0, p1, q0);
            OrientSignRobust pq1 = RobustOrientation.SignFiltered(p0, p1, q1);
            OrientSignRobust qp0 = RobustOrientation.SignFiltered(q0, q1, p0);
            OrientSignRobust qp1 = RobustOrientation.SignFiltered(q0, q1, p1);

            if (IsNan(pq0) || IsNan(pq1) || IsNan(qp0) || IsNan(qp1))
            {
                return IntersectSign.Nan;
            }

            if (IsUncertain(pq0) || IsUncertain(pq1)
                || IsUncertain(qp0) || IsUncertain(qp1))
            {
                return IntersectSign.Uncertain;
            }

            if (SameStrictSide(pq0, pq1) || SameStrictSide(qp0, qp1))
            {
                return IntersectSign.None;
            }

            if (IsZero(pq0) || IsZero(pq1) || IsZero(qp0) || IsZero(qp1))
            {
                return IntersectSign.Collinear;
            }

            return IntersectSign.Point;
        }

        private static bool IsNan(OrientSignRobust s) =>
            s == OrientSignRobust.Nan;

        private static bool IsUncertain(OrientSignRobust s) =>
            s == OrientSignRobust.Uncertain;

        private static bool IsZero(OrientSignRobust s) =>
            s == OrientSignRobust.Zero;

        private static bool SameStrictSide(OrientSignRobust s1, OrientSignRobust s2) =>
            (s1 == OrientSignRobust.Pos && s2 == OrientSignRobust.Pos)
            || (s1 == OrientSignRobust.Neg && s2 == OrientSignRobust.Neg);

        /// <summary>
        /// Rounded intersection point coordinates for two segments that the
        /// filter classifies as <see cref="IntersectSign.Point"/>.  Returns
        /// <c>null</c> for any other result.  Transliterates Coq
        /// <c>b64_intersect_point</c> in
        /// <c>theories-flocq/Intersect_b64.v</c>.
        /// </summary>
        /// <remarks>
        /// <para>Formula (Cramer's rule, parameter on segment
        /// <paramref name="p0"/>-<paramref name="p1"/> -- matches NTS
        /// upstream's <c>RobustLineIntersector</c> convention):</para>
        /// <code>
        ///   s   = orient(Q0, Q1, P0) / (orient(Q0, Q1, P0) - orient(Q0, Q1, P1))
        ///   X.x = P0.x + s * (P1.x - P0.x)
        ///   X.y = P0.y + s * (P1.y - P0.y)
        /// </code>
        /// <para>Bit-equal against the Coq-extracted reference on every
        /// test case in <c>RobustLineIntersectorRocqRefTests</c>.  In the
        /// integer regime the cross-product inputs <c>qp0</c> / <c>qp1</c>
        /// are exact integers; division and the subsequent multiplication
        /// + addition introduce binary64 rounding -- the returned
        /// <see cref="BPoint"/> is the rounded approximation of the true
        /// rational intersection.  A forward-error soundness theorem
        /// against the true rational is deferred (comparable in scope to
        /// Phase 0 Stage D / forward-error analysis).</para>
        /// </remarks>
        public static BPoint? IntersectionPoint(
            BPoint p0, BPoint p1, BPoint q0, BPoint q1)
        {
            if (SignFiltered(p0, p1, q0, q1) != IntersectSign.Point)
            {
                return null;
            }

            double qp0 = RobustOrientation.Orient2d(q0, q1, p0);
            double qp1 = RobustOrientation.Orient2d(q0, q1, p1);
            double den = qp0 - qp1;

            // den is integer-nonzero in the integer regime (Phase 0
            // exactness on the orient2d inputs).  In the general regime
            // it's a rounded value; if it happens to round to zero or
            // NaN we abort defensively, matching the Coq function's
            // None branch.
            if (den == 0.0 || double.IsNaN(den))
            {
                return null;
            }

            double s  = qp0 / den;
            double dx = p1.X - p0.X;
            double dy = p1.Y - p0.Y;
            return new BPoint(p0.X + s * dx, p0.Y + s * dy);
        }
    }
}
