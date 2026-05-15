// =============================================================================
// NetTopologySuite.Curve.Robust.Intersect.RobustLineIntersectorSharedEndpointTests
// -----------------------------------------------------------------------------
// Direct unit tests for `RobustLineIntersector.TryGetSharedEndpoint`.  No
// RocqRef dependency -- the method's logic is simple coordinate-equality
// probing, and its Coq counterpart (`b64_shared_endpoint_witness`) has a
// soundness theorem in `theories-flocq/Intersect_b64.v`
// (`b64_shared_endpoint_witness_sound`).  These tests pin the four pairing
// cases + the no-match case + a handful of edge cases (zero coords, sign-
// of-zero, NaN).
// =============================================================================

using NetTopologySuite.Robust.Intersect;
using NetTopologySuite.Robust.Simplify;
using NUnit.Framework;

namespace NetTopologySuite.Test.Robust.Intersect
{
    [TestFixture]
    public class RobustLineIntersectorSharedEndpointTests
    {
        // -----------------------------------------------------------------
        // The four pairings: P0=Q0, P0=Q1, P1=Q0, P1=Q1.  Each test pins
        // both the non-null return and the specific witness endpoint.
        // -----------------------------------------------------------------

        [Test]
        public void Pairing_P0_Q0_returns_P0()
        {
            var p0 = new BPoint(3, 4);
            var p1 = new BPoint(7, 9);
            var q0 = new BPoint(3, 4);
            var q1 = new BPoint(11, 13);

            var shared = RobustLineIntersector.TryGetSharedEndpoint(p0, p1, q0, q1);

            Assert.That(shared, Is.Not.Null);
            Assert.That(shared!.Value.X, Is.EqualTo(3.0));
            Assert.That(shared.Value.Y, Is.EqualTo(4.0));
        }

        [Test]
        public void Pairing_P0_Q1_returns_P0()
        {
            var p0 = new BPoint(3, 4);
            var p1 = new BPoint(7, 9);
            var q0 = new BPoint(11, 13);
            var q1 = new BPoint(3, 4);

            var shared = RobustLineIntersector.TryGetSharedEndpoint(p0, p1, q0, q1);

            Assert.That(shared, Is.Not.Null);
            Assert.That(shared!.Value.X, Is.EqualTo(3.0));
            Assert.That(shared.Value.Y, Is.EqualTo(4.0));
        }

        [Test]
        public void Pairing_P1_Q0_returns_P1()
        {
            var p0 = new BPoint(3, 4);
            var p1 = new BPoint(7, 9);
            var q0 = new BPoint(7, 9);
            var q1 = new BPoint(11, 13);

            var shared = RobustLineIntersector.TryGetSharedEndpoint(p0, p1, q0, q1);

            Assert.That(shared, Is.Not.Null);
            Assert.That(shared!.Value.X, Is.EqualTo(7.0));
            Assert.That(shared.Value.Y, Is.EqualTo(9.0));
        }

        [Test]
        public void Pairing_P1_Q1_returns_P1()
        {
            var p0 = new BPoint(3, 4);
            var p1 = new BPoint(7, 9);
            var q0 = new BPoint(11, 13);
            var q1 = new BPoint(7, 9);

            var shared = RobustLineIntersector.TryGetSharedEndpoint(p0, p1, q0, q1);

            Assert.That(shared, Is.Not.Null);
            Assert.That(shared!.Value.X, Is.EqualTo(7.0));
            Assert.That(shared.Value.Y, Is.EqualTo(9.0));
        }

        // -----------------------------------------------------------------
        // No coincidence: returns null.
        // -----------------------------------------------------------------

        [Test]
        public void NoSharedEndpoint_returns_null()
        {
            // Two segments that cross at an interior point but don't share endpoints.
            var p0 = new BPoint(0, 0);
            var p1 = new BPoint(2, 0);
            var q0 = new BPoint(1, -1);
            var q1 = new BPoint(1,  1);

            var shared = RobustLineIntersector.TryGetSharedEndpoint(p0, p1, q0, q1);

            Assert.That(shared, Is.Null);
        }

        [Test]
        public void DisjointParallel_returns_null()
        {
            var p0 = new BPoint(0, 0);
            var p1 = new BPoint(1, 0);
            var q0 = new BPoint(0, 1);
            var q1 = new BPoint(1, 1);

            var shared = RobustLineIntersector.TryGetSharedEndpoint(p0, p1, q0, q1);

            Assert.That(shared, Is.Null);
        }

        // -----------------------------------------------------------------
        // First-match-wins when multiple pairings coincide.  The Coq spec
        // is deterministic: P0=Q0 is checked first, then P0=Q1, then
        // P1=Q0, then P1=Q1.
        // -----------------------------------------------------------------

        [Test]
        public void DuplicateEndpoints_returns_P0()
        {
            // Both segments are the same point (degenerate).  P0=Q0 matches first.
            var p0 = new BPoint(5, 5);
            var p1 = new BPoint(5, 5);
            var q0 = new BPoint(5, 5);
            var q1 = new BPoint(5, 5);

            var shared = RobustLineIntersector.TryGetSharedEndpoint(p0, p1, q0, q1);

            Assert.That(shared, Is.Not.Null);
            Assert.That(shared!.Value.X, Is.EqualTo(5.0));
            Assert.That(shared.Value.Y, Is.EqualTo(5.0));
        }

        // -----------------------------------------------------------------
        // Edge cases: zero coordinates, IEEE 754 quirks.
        // -----------------------------------------------------------------

        [Test]
        public void ZeroCoordinates_match()
        {
            var p0 = new BPoint(0, 0);
            var p1 = new BPoint(1, 0);
            var q0 = new BPoint(0, 0);
            var q1 = new BPoint(0, 1);

            var shared = RobustLineIntersector.TryGetSharedEndpoint(p0, p1, q0, q1);

            Assert.That(shared, Is.Not.Null);
            Assert.That(shared!.Value.X, Is.EqualTo(0.0));
            Assert.That(shared.Value.Y, Is.EqualTo(0.0));
        }

        [Test]
        public void PositiveAndNegativeZero_match()
        {
            // IEEE 754: +0.0 == -0.0 is true.  TryGetSharedEndpoint treats
            // them as equal -- the geometric point is the origin in both
            // cases.
            var p0 = new BPoint( 0.0,  0.0);
            var p1 = new BPoint( 1.0,  0.0);
            var q0 = new BPoint(-0.0, -0.0);
            var q1 = new BPoint( 0.0,  1.0);

            var shared = RobustLineIntersector.TryGetSharedEndpoint(p0, p1, q0, q1);

            Assert.That(shared, Is.Not.Null);
        }

        [Test]
        public void NaN_coord_does_not_match_itself()
        {
            // IEEE 754: NaN != NaN.  An NaN-bearing endpoint cannot match
            // anything, even an identical-bit-pattern NaN on the other
            // side -- consistent with the Coq function's behaviour
            // (Bcompare returns None on NaN, not Some Eq).
            var p0 = new BPoint(double.NaN, 0);
            var p1 = new BPoint(1, 0);
            var q0 = new BPoint(double.NaN, 0);
            var q1 = new BPoint(2, 2);

            var shared = RobustLineIntersector.TryGetSharedEndpoint(p0, p1, q0, q1);

            Assert.That(shared, Is.Null);
        }

        // -----------------------------------------------------------------
        // Integer regime: |coord| <= 2^25.  The regime the Coq soundness
        // theorem covers.
        // -----------------------------------------------------------------

        [Test]
        public void IntegerRegime_boundary_shared_endpoint()
        {
            const double K = 33554432.0; // 2^25
            var p0 = new BPoint(-K, -K);
            var p1 = new BPoint( K,  K);
            var q0 = new BPoint( K,  K);
            var q1 = new BPoint( K, -K);

            var shared = RobustLineIntersector.TryGetSharedEndpoint(p0, p1, q0, q1);

            Assert.That(shared, Is.Not.Null);
            Assert.That(shared!.Value.X, Is.EqualTo(K));
            Assert.That(shared.Value.Y, Is.EqualTo(K));
        }
    }
}
