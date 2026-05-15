// =============================================================================
// NetTopologySuite.Curve.Robust.Simplify.GreedyPerpSimplifier
// -----------------------------------------------------------------------------
// Idiomatic iterative C# implementation of the greedy perpendicular-distance
// polyline simplifier specified in
//
//     theories-flocq/Validate_binary64.v
//     greedy_simplify_perp_b64_aux / greedy_simplify_perp_b64
//
// in the companion NetTopologySuite.Proofs corpus.  The earlier line-for-line
// transliteration (recursive `Aux` + internal `SliceList` view) is replaced
// here by a single sweep over the input with two indices: `keptIdx`, pointing
// at the most recently kept vertex, and `qIdx`, the current candidate.
//
// Correspondence with the Coq Fixpoint clauses:
//
//     match rest with
//     | []          => [kept]                          --> n == 1 short-circuit
//     | [q]         => [kept; q]                       --> emit keptIdx + n-1
//     | q :: r :: _ =>                                 --> the main while-loop
//         if (cross kept r q)^2 <= eps^2 * dist_sq kept r
//         then ... eps kept more                       --> drop q, qIdx++
//         else kept :: ... eps q more                  --> emit kept, advance
//     end
//
// The structural facts proved Qed-closed on the Coq side are still mirrored
// 1:1 by `GreedyPerpSimplifierTests` (head preservation, length never
// increases, output non-empty for non-empty input, etc).  Bit-equality with
// the Coq-extracted RocqRef binary is enforced by
// `GreedyPerpSimplifierRocqRefTests`.
// =============================================================================

using System;
using System.Collections.Generic;

namespace NetTopologySuite.Robust.Simplify
{
    /// <summary>
    /// Greedy perpendicular-distance polyline simplifier on binary64 points.
    /// The decision rule is squared-cross-product against an eps^2 budget:
    /// <code>
    ///   (cross kept r q)^2  &lt;=  eps^2 * dist_sq kept r
    /// </code>
    /// matching the binary64 form proved structurally sound in the companion
    /// Coq corpus (see file header above).
    /// </summary>
    public static class GreedyPerpSimplifier
    {
        /// <summary>
        /// Simplify a polyline of binary64 points.  Output begins with the
        /// input head, ends with the input tail, and contains a subset of
        /// the original points in their original order.
        /// </summary>
        /// <param name="eps">Tolerance.  Points whose squared perpendicular
        /// distance from the chord <c>(kept, r)</c> does not exceed
        /// <c>eps^2 * dist_sq(kept, r)</c> are dropped.</param>
        /// <param name="points">The input polyline.</param>
        /// <returns>The simplified polyline.</returns>
        public static List<BPoint> Simplify(double eps, IReadOnlyList<BPoint> points)
        {
            if (points is null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            int n = points.Count;
            if (n == 0)
            {
                return new List<BPoint>();
            }
            if (n == 1)
            {
                return new List<BPoint> { points[0] };
            }
            if (n == 2)
            {
                return new List<BPoint> { points[0], points[1] };
            }

            // n >= 3.  The first and last points are always emitted; the loop
            // decides which interior points survive.
            var result = new List<BPoint>(n);
            int keptIdx = 0;
            int qIdx = 1;
            double epsSq = eps * eps;

            while (qIdx < n - 1)
            {
                BPoint kept = points[keptIdx];
                BPoint q    = points[qIdx];
                BPoint r    = points[qIdx + 1];

                double c   = B64Ops.Cross(kept, r, q);
                double lhs = c * c;
                double rhs = epsSq * B64Ops.DistSq(kept, r);

                if (B64Ops.Le(lhs, rhs))
                {
                    // drop q -- kept unchanged, advance past q.
                    qIdx++;
                }
                else
                {
                    // emit kept; q becomes the new kept; advance past q.
                    result.Add(kept);
                    keptIdx = qIdx;
                    qIdx++;
                }
            }

            // qIdx == n - 1.  Mirrors the [kept; q] base case of the Coq
            // Fixpoint: emit the last surviving kept vertex and then the
            // input tail.
            result.Add(points[keptIdx]);
            result.Add(points[n - 1]);
            return result;
        }
    }
}
