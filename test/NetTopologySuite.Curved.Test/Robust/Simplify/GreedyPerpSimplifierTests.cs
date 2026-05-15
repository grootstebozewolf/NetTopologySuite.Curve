// =============================================================================
// NetTopologySuite.Curve.Robust.Simplify.GreedyPerpSimplifierTests
// -----------------------------------------------------------------------------
// Smoke tests mirroring the Qed-closed structural lemmas in the companion
// Coq corpus (`theories-flocq/Validate_binary64.v` in
// NetTopologySuite.Proofs).  Each test asserts in C# what the Coq side has
// already proved: the test names point at the Coq lemma they correspond to.
//
// The point of this round is to verify that the line-for-line C# port
// behaves the way the Coq spec says it must.  A divergence here would mean
// either (a) the C# port mis-transliterates, or (b) we have an IEEE 754
// edge case the Coq side does not yet cover.  Either is a finding worth
// chasing.
// =============================================================================

using System.Collections.Generic;
using System.Linq;
using NetTopologySuite.Robust.Simplify;
using NUnit.Framework;

namespace NetTopologySuite.Test.Robust.Simplify;

[TestFixture]
public class GreedyPerpSimplifierTests
{
    private const double Eps = 0.5;

    private static BPoint P(double x, double y) => new BPoint(x, y);

    // -------------------------------------------------------------------------
    // Mirrors: greedy_simplify_perp_b64_nil
    //          forall eps, greedy_simplify_perp_b64 eps [] = [].
    // -------------------------------------------------------------------------
    [Test]
    public void Simplify_OfEmpty_IsEmpty()
    {
        var result = GreedyPerpSimplifier.Simplify(Eps, new List<BPoint>());
        Assert.That(result, Is.Empty);
    }

    // -------------------------------------------------------------------------
    // Mirrors: greedy_simplify_perp_b64_singleton
    //          forall eps p, greedy_simplify_perp_b64 eps [p] = [p].
    // -------------------------------------------------------------------------
    [Test]
    public void Simplify_OfSingleton_IsInputVerbatim()
    {
        var p = P(1.0, 2.0);
        var result = GreedyPerpSimplifier.Simplify(Eps, new[] { p });
        Assert.That(result, Is.EqualTo(new[] { p }));
    }

    // -------------------------------------------------------------------------
    // Mirrors: greedy_simplify_perp_b64_two_points
    //          forall eps p q, greedy_simplify_perp_b64 eps [p; q] = [p; q].
    // Two-point inputs are returned verbatim: no third point to test the
    // perpendicular-distance predicate against.
    // -------------------------------------------------------------------------
    [Test]
    public void Simplify_OfTwoPoints_IsInputVerbatim()
    {
        var p = P(0.0, 0.0);
        var q = P(1.0, 1.0);
        var result = GreedyPerpSimplifier.Simplify(Eps, new[] { p, q });
        Assert.That(result, Is.EqualTo(new[] { p, q }));
    }

    // -------------------------------------------------------------------------
    // Mirrors: greedy_simplify_perp_b64_nonempty
    //          forall eps p rest, greedy_simplify_perp_b64 eps (p :: rest) <> [].
    // -------------------------------------------------------------------------
    [Test]
    public void Simplify_OfNonEmpty_IsNonEmpty()
    {
        var points = new[] { P(0, 0), P(1, 0), P(2, 0), P(3, 0) };
        var result = GreedyPerpSimplifier.Simplify(Eps, points);
        Assert.That(result, Is.Not.Empty);
    }

    // -------------------------------------------------------------------------
    // Mirrors: greedy_simplify_perp_b64_length_le
    //          length (greedy_simplify_perp_b64 eps pts) <= length pts.
    // -------------------------------------------------------------------------
    [Test]
    public void Simplify_LengthNeverIncreases([Values(0, 1, 2, 3, 7, 25)] int n)
    {
        var points = Enumerable.Range(0, n).Select(i => P(i, 0)).ToList();
        var result = GreedyPerpSimplifier.Simplify(Eps, points);
        Assert.That(result.Count, Is.LessThanOrEqualTo(points.Count));
    }

    // -------------------------------------------------------------------------
    // Mirrors: greedy_simplify_perp_b64_preserves_head
    //          hd default (greedy_simplify_perp_b64 eps (p :: rest)) = p.
    // -------------------------------------------------------------------------
    [Test]
    public void Simplify_PreservesHead()
    {
        var head = P(42, -17);
        var points = new[] { head, P(1, 0), P(2, 0), P(3, 0) };
        var result = GreedyPerpSimplifier.Simplify(Eps, points);
        Assert.That(result[0], Is.EqualTo(head));
    }

    // -------------------------------------------------------------------------
    // Mirrors: greedy_simplify_perp_b64_in_head
    //          In p (greedy_simplify_perp_b64 eps (p :: rest)).
    // -------------------------------------------------------------------------
    [Test]
    public void Simplify_OutputContainsInputHead()
    {
        var head = P(42, -17);
        var points = new[] { head, P(1, 0), P(2, 0), P(3, 0) };
        var result = GreedyPerpSimplifier.Simplify(Eps, points);
        Assert.That(result, Does.Contain(head));
    }

    // -------------------------------------------------------------------------
    // Sanity: on a straight-line input with eps strictly positive, the
    // simplifier should drop the collinear middle points.  The Coq side
    // does not yet claim this -- it is a candidate semantic theorem for
    // the deferred bridge -- but the behaviour is the whole point of the
    // algorithm, so we pin it as an executable expectation here.
    // -------------------------------------------------------------------------
    [Test]
    public void Simplify_DropsCollinearInterior()
    {
        var points = new[] { P(0, 0), P(1, 0), P(2, 0), P(3, 0), P(4, 0) };
        var result = GreedyPerpSimplifier.Simplify(Eps, points);
        // first kept always; last point always reached (the two-point base
        // case keeps both endpoints); colinear interior is dropped.
        Assert.That(result[0], Is.EqualTo(points[0]));
        Assert.That(result[^1], Is.EqualTo(points[^1]));
        Assert.That(result.Count, Is.LessThan(points.Length));
    }

    // -------------------------------------------------------------------------
    // Sanity: NaN coordinate triggers the safe-default behaviour
    // (`b64_le` returns false on NaN), so nothing gets dropped that
    // would have been dropped on a clean trace.  We do not over-claim
    // exact output shape here -- only that the simplifier does not throw
    // and emits the head verbatim.
    // -------------------------------------------------------------------------
    [Test]
    public void Simplify_DoesNotThrowOnNaN()
    {
        var head = P(0, 0);
        var nanPoint = P(double.NaN, 0);
        var points = new[] { head, nanPoint, P(2, 0) };
        Assert.DoesNotThrow(() => GreedyPerpSimplifier.Simplify(Eps, points));
        var result = GreedyPerpSimplifier.Simplify(Eps, points);
        Assert.That(result[0], Is.EqualTo(head));
    }
}
