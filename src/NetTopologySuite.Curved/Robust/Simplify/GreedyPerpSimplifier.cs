// =============================================================================
// NetTopologySuite.Curve.Robust.Simplify.GreedyPerpSimplifier
// -----------------------------------------------------------------------------
// Line-for-line C# transliteration of the Coq Fixpoint
// `greedy_simplify_perp_b64_aux` from theories-flocq/Validate_binary64.v
// in NetTopologySuite.Proofs.  This is the *transliteration* commit: the
// recursion structure and branching are kept verbatim against the Coq so
// the C# and the Coq diff cleanly.  An idiomatic refactor (iterative
// driver, no list copying) is intentionally deferred to a follow-up
// commit with the same test suite as guard-rail.
//
// Coq source:
//
//     Fixpoint greedy_simplify_perp_b64_aux
//         (eps : binary64) (kept : BPoint) (rest : list BPoint) : list BPoint :=
//       match rest with
//       | []          => [kept]
//       | q :: more =>
//           match more with
//           | []          => [kept; q]
//           | r :: _tail =>
//               let c   := b64_cross kept r q in
//               let lhs := b64_mult c c in
//               let rhs := b64_mult (b64_mult eps eps) (b64_dist_sq kept r) in
//               if b64_le lhs rhs
//               then greedy_simplify_perp_b64_aux eps kept more
//               else kept :: greedy_simplify_perp_b64_aux eps q more
//           end
//       end.
//
//     Definition greedy_simplify_perp_b64
//         (eps : binary64) (pts : list BPoint) : list BPoint :=
//       match pts with
//       | []         => []
//       | p :: rest  => greedy_simplify_perp_b64_aux eps p rest
//       end.
//
// Structural facts proved Qed-closed on the Coq side and asserted by the
// test suite here:
//
//   * empty input        -> empty output                (`_nil`)
//   * single-point input -> input verbatim              (`_singleton`)
//   * two-point input    -> input verbatim              (`_two_points`)
//   * non-empty input    -> non-empty output            (`_nonempty`)
//   * output length      <= input length                (`_length_le`)
//   * head of input      -> appears in output           (`_in_head`)
//   * head of output     == head of input               (`_preserves_head`)
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
/// expressed in binary64 as the Coq version.
/// </summary>
public static class GreedyPerpSimplifier
{
    /// <summary>
    /// Auxiliary form taking the already-fixed <paramref name="kept"/>
    /// vertex and the list of remaining candidates.  Transliterates the
    /// Coq Fixpoint <c>greedy_simplify_perp_b64_aux</c> verbatim.
    /// </summary>
    /// <param name="eps">Tolerance (the simplifier keeps a point whenever
    /// the squared perpendicular distance exceeds <c>eps^2 * dist_sq</c>).</param>
    /// <param name="kept">The most recently kept vertex.</param>
    /// <param name="rest">The remaining candidate vertices, in input order.</param>
    /// <returns>The simplified suffix, always beginning with <paramref name="kept"/>.</returns>
    public static List<BPoint> Aux(double eps, BPoint kept, IReadOnlyList<BPoint> rest)
    {
        if (rest is null)
        {
            throw new ArgumentNullException(nameof(rest));
        }

        // match rest with | [] => [kept]
        if (rest.Count == 0)
        {
            return new List<BPoint> { kept };
        }

        BPoint q = rest[0];
        // `more` is the structural tail `q :: more` -> `more`
        var more = new SliceList<BPoint>(rest, 1);

        // match more with | [] => [kept; q]
        if (more.Count == 0)
        {
            return new List<BPoint> { kept, q };
        }

        BPoint r = more[0];

        // let c   := b64_cross kept r q
        // let lhs := b64_mult c c
        // let rhs := b64_mult (b64_mult eps eps) (b64_dist_sq kept r)
        double c   = B64Ops.Cross(kept, r, q);
        double lhs = c * c;
        double rhs = (eps * eps) * B64Ops.DistSq(kept, r);

        if (B64Ops.Le(lhs, rhs))
        {
            // drop q, recurse with same kept on `more`
            return Aux(eps, kept, more);
        }
        else
        {
            // emit kept, recurse with q as new kept on `more`
            var tail = Aux(eps, q, more);
            var result = new List<BPoint>(1 + tail.Count) { kept };
            result.AddRange(tail);
            return result;
        }
    }

    /// <summary>
    /// Top-level simplifier.  Transliterates the Coq:
    /// <code>
    /// Definition greedy_simplify_perp_b64 (eps : binary64) (pts : list BPoint) : list BPoint :=
    ///   match pts with
    ///   | []         =&gt; []
    ///   | p :: rest  =&gt; greedy_simplify_perp_b64_aux eps p rest
    ///   end.
    /// </code>
    /// </summary>
    public static List<BPoint> Simplify(double eps, IReadOnlyList<BPoint> points)
    {
        if (points is null)
        {
            throw new ArgumentNullException(nameof(points));
        }

        if (points.Count == 0)
        {
            return new List<BPoint>();
        }

        BPoint p = points[0];
        var rest = new SliceList<BPoint>(points, 1);
        return Aux(eps, p, rest);
    }

    /// <summary>
    /// Zero-allocation tail-slice view over an <see cref="IReadOnlyList{T}"/>.
    /// Used to mirror Coq's <c>match xs with q :: more</c> pattern without
    /// copying the underlying list at every recursion step.  The
    /// transliteration intentionally stays recursive (matching the Coq
    /// Fixpoint shape); only the slice is non-naive so that the recursion
    /// is linear-time, not quadratic.
    /// </summary>
    private sealed class SliceList<T> : IReadOnlyList<T>
    {
        private readonly IReadOnlyList<T> _source;
        private readonly int _offset;

        public SliceList(IReadOnlyList<T> source, int offset)
        {
            _source = source;
            _offset = offset;
        }

        public T this[int index] => _source[_offset + index];

        public int Count => _source.Count - _offset;

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = _offset; i < _source.Count; i++)
            {
                yield return _source[i];
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
}
