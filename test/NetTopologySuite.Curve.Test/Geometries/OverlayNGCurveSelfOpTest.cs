using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using NUnit.Framework;

namespace NetTopologySuite.Test.Geometries
{
    /// <summary>
    /// Phase 0 suite for <see cref="CurveGeometryOverlay.OverlayNGCurve"/>.
    /// Mnemonics: CAP ∩ · CUP ∪ · SUB ∖ · XOR Δ
    /// (docs/overlay-ng-curve-ops-mnemonics.md in NetTopologySuite.Proofs).
    /// </summary>
    [TestFixture]
    public class OverlayNGCurveSelfOpTest
    {
        private NtsCurveGeometryServices _services;

        /// <summary>Regular 6-sample circular disk r=5 (valid CIRCULARSTRING shell).</summary>
        private const string DiskR5 =
            "CURVEPOLYGON (CIRCULARSTRING (5 0, 2.5 4.330127019, -2.5 4.330127019, -5 0, -2.5 -4.330127019, 2.5 -4.330127019, 5 0))";

        /// <summary>Concentric disk r=3, CCW (B_fix).</summary>
        private const string DiskR3 =
            "CURVEPOLYGON (CIRCULARSTRING (3 0, 1.5 2.598076211, -1.5 2.598076211, -3 0, -1.5 -2.598076211, 1.5 -2.598076211, 3 0))";

        [SetUp]
        public void SetUp()
        {
            _services = new NtsCurveGeometryServices(
                CoordinateArraySequenceFactory.Instance,
                new PrecisionModel(PrecisionModels.Floating),
                0,
                new CoordinateEqualityComparer(),
                defaultArcSegmentLength: 0.5);
        }

        private Geometry Read(string wkt) => _services.WKTReader.Read(wkt);

        private Geometry EmptyPolygon() =>
            _services.CreateGeometryFactory().CreatePolygon();

        // ---- G1 CAP · I meet myself → me ------------------------------------

        [Test]
        public void G1_CAP_SelfIntersection_IsMe()
        {
            var g = Read(DiskR5);
            var i = g.Intersection(g);
            Assert.That(i, Is.InstanceOf<CurvePolygon>(), "R1: keep CurvePolygon");
            Assert.That(i.EqualsExact(g), Is.True, "G1 CAP: A ∩ A → A");
        }

        // ---- G2 CUP · double pour → same cup --------------------------------

        [Test]
        public void G2_CUP_SelfUnion_IsMe()
        {
            var g = Read(DiskR5);
            var u = g.Union(g);
            Assert.That(u, Is.InstanceOf<CurvePolygon>(), "R1: keep CurvePolygon");
            Assert.That(u.EqualsExact(g), Is.True, "G2 CUP: A ∪ A → A");
        }

        [Test]
        public void G2_CUP_UnaryUnion_IsMe()
        {
            var g = Read(DiskR5);
            var u = g.Union();
            Assert.That(u, Is.InstanceOf<CurvePolygon>());
            Assert.That(u.EqualsExact(g), Is.True, "unary CUP identity");
        }

        // ---- G3 SUB · erase myself → empty ----------------------------------

        [Test]
        public void G3_SUB_SelfDifference_IsEmpty()
        {
            var g = Read(DiskR5);
            Assert.That(g.Difference(g).IsEmpty, Is.True, "G3 SUB: A ∖ A → ∅");
        }

        // ---- G4 XOR · mirror cancel → empty ---------------------------------

        [Test]
        public void G4_XOR_SelfSymDifference_IsEmpty()
        {
            var g = Read(DiskR5);
            Assert.That(g.SymmetricDifference(g).IsEmpty, Is.True, "G4 XOR: A Δ A → ∅");
        }

        // ---- G5 · nothing in the room ---------------------------------------

        [Test]
        public void G5_EmptyPartner_CAP_CUP_SUB()
        {
            var a = Read(DiskR5);
            var empty = EmptyPolygon();

            Assert.That(a.Intersection(empty).IsEmpty, Is.True, "A ∩ ∅ → ∅");
            Assert.That(a.Union(empty).EqualsExact(a), Is.True, "A ∪ ∅ → A");
            Assert.That(a.Difference(empty).EqualsExact(a), Is.True, "A ∖ ∅ → A");
            Assert.That(empty.Difference(a).IsEmpty, Is.True, "∅ ∖ A → ∅");
        }

        // ---- F1 · short-circuit before densify (equalsExact clones) ---------

        [Test]
        public void F1_EqualInstances_NotOnlyReference_StillAlgebra()
        {
            var a = Read(DiskR5);
            var b = Read(DiskR5);
            Assert.That(ReferenceEquals(a, b), Is.False);
            Assert.That(a.Intersection(b).EqualsExact(a), Is.True, "F1 CAP");
            Assert.That(a.Union(b).EqualsExact(a), Is.True, "F1 CUP");
            Assert.That(a.Difference(b).IsEmpty, Is.True, "F1 SUB");
            Assert.That(a.SymmetricDifference(b).IsEmpty, Is.True, "F1 XOR");
        }

        [Test]
        public void NestedDisks_SUB_BA_IsEmpty()
        {
            var a = Read(DiskR5);
            var b = Read(DiskR3);
            Assert.That(b.Difference(a).IsEmpty, Is.True, "inner ∖ outer → ∅");
        }

        [Test]
        public void V3_TypeGate_DefaultIsOverlayNGCurve()
        {
            Assert.That(_services.GeometryOverlay.ToString(), Is.EqualTo("OverlayNGCurve"));
        }
    }
}
