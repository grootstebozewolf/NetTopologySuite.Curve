using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using NUnit.Framework;

namespace NetTopologySuite.Test.CurveAwareness
{
    /// <summary>
    /// Pins the F-CP (Structural CurvePolygon) contract of the SFA Curve
    /// Awareness epic (locationtech/jts#1195, Phase 1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The JTS-side companion of this class is a red-test suite that drives
    /// implementation work via "delete on green" — see
    /// <c>modules/curved/src/test/java/org/locationtech/jts/spec/curveawareness/CurvePolygonStructuralSpec.java</c>
    /// on the <c>feature/sfa-curve-F-CP-spike-optionA</c> branch.
    /// </para>
    /// <para>
    /// In NetTopologySuite.Curve, four of the six F-CP sub-TAGs are
    /// <em>green by architecture</em>: the repo's <c>CurvePolygon</c> extends
    /// <c>Surface&lt;Curve&gt;</c>, so the JTS Option-A/B/C dovetail
    /// (FCP-DOVE, epic §7 risk #1) never materialises. The two remaining
    /// sub-TAGs (FCP-MEM, FCP-WKT) depend on the reader/writer behavior in
    /// <c>WKTReaderEx</c> / <c>WKTWriterEx</c> and are measured here.
    /// </para>
    /// <para>
    /// Per the NTS convention (assert-style regression net, not the JTS
    /// "delete on green"), this class stays in the codebase indefinitely:
    /// any future refactor that accidentally collapses structural rings to
    /// flat <see cref="LinearRing"/> on copy, read, or linearisation goes
    /// red with a TAG-tagged message that points to the regression.
    /// </para>
    /// <para>See <c>docs/SPEC_F_CP.md</c> for the rationale, the cross-walk
    /// to the JTS spec, and the link to the Coq proofs that back later
    /// Phase-2 work.</para>
    /// </remarks>
    [TestFixture]
    [Category("CurveAwareness")]
    public class CurvePolygonStructuralSpec
    {
        // A compound shell made of two semi-circles, plus one linear hole.
        private const string WktCpCompoundShellWithHole =
            "CURVEPOLYGON (" +
            "COMPOUNDCURVE (" +
            "CIRCULARSTRING (0 0, 5 5, 10 0), " +
            "CIRCULARSTRING (10 0, 5 -5, 0 0)" +
            "), " +
            "(2 -1, 8 -1, 8 1, 2 1, 2 -1)" +
            ")";

        // A CircularString-shelled CurvePolygon (single closed arc).
        private const string WktCpArcShell =
            "CURVEPOLYGON (CIRCULARSTRING (0 0, 10 0, 5 5, 0 5, 0 0))";

        // A CurvePolygon with a CircularString hole inside a flat shell.
        private const string WktCpArcHole =
            "CURVEPOLYGON (" +
            "(0 0, 100 0, 100 100, 0 100, 0 0), " +
            "CIRCULARSTRING (40 50, 50 60, 60 50, 50 40, 40 50)" +
            ")";

        private NtsCurveGeometryServices _services;

        [SetUp]
        public void SetUp()
        {
            _services = new NtsCurveGeometryServices(
                CoordinateArraySequenceFactory.Instance,
                new PrecisionModel(PrecisionModels.Floating),
                0,
                new CoordinateEqualityComparer(),
                defaultArcSegmentLength: 1.0);
        }

        private CurvePolygon ReadCurvePolygon(string wkt)
        {
            var g = _services.WKTReader.Read(wkt);
            Assert.That(g, Is.InstanceOf<CurvePolygon>(),
                "Reader must produce a CurvePolygon, got " + g.GeometryType);
            return (CurvePolygon)g;
        }

        // ============================================================
        // FCP-S — shell is exposed as a Curve, not a LinearRing
        // ============================================================

        [Test]
        public void FCP_S_compound_shell_exposed_as_CompoundCurve()
        {
            var cp = ReadCurvePolygon(WktCpCompoundShellWithHole);
            Assert.That(cp.ExteriorRing, Is.InstanceOf<CompoundCurve>(),
                "FCP-S: compound shell should be exposed as CompoundCurve, got "
                + cp.ExteriorRing.GetType().Name);
        }

        [Test]
        public void FCP_S_arc_shell_exposed_as_CircularString()
        {
            var cp = ReadCurvePolygon(WktCpArcShell);
            Assert.That(cp.ExteriorRing, Is.InstanceOf<CircularString>(),
                "FCP-S: arc shell should be exposed as CircularString, got "
                + cp.ExteriorRing.GetType().Name);
        }

        // ============================================================
        // FCP-MEM — shell preserves member subtypes
        // ============================================================

        [Test]
        public void FCP_MEM_compound_shell_members_retain_subtypes()
        {
            var cp = ReadCurvePolygon(WktCpCompoundShellWithHole);
            Assert.That(cp.ExteriorRing, Is.InstanceOf<CompoundCurve>());

            var cc = (CompoundCurve)cp.ExteriorRing;
            Assert.That(cc.Curves.Count, Is.EqualTo(2),
                "FCP-MEM: compound shell should have two members");
            Assert.That(cc.Curves[0], Is.InstanceOf<CircularString>(),
                "FCP-MEM: member 0 should be a CircularString");
            Assert.That(cc.Curves[1], Is.InstanceOf<CircularString>(),
                "FCP-MEM: member 1 should be a CircularString");
        }

        // ============================================================
        // FCP-H — interior rings are Curves too (per OGC SFA §6.1.10)
        // ============================================================

        [Test]
        public void FCP_H_arc_hole_exposed_as_CircularString()
        {
            var cp = ReadCurvePolygon(WktCpArcHole);
            Assert.That(cp.NumInteriorRings, Is.EqualTo(1),
                "FCP-H: CurvePolygon has one interior ring");

            var hole = cp.GetInteriorRingN(0);
            Assert.That(hole, Is.InstanceOf<CircularString>(),
                "FCP-H: arc hole should be a CircularString, got "
                + hole.GetType().Name);
        }

        // ============================================================
        // FCP-CP — copy() preserves curve identity of shell + holes
        // ============================================================

        [Test]
        public void FCP_CP_copy_preserves_shell_subtype()
        {
            var cp = ReadCurvePolygon(WktCpCompoundShellWithHole);
            var copy = (CurvePolygon)cp.Copy();

            Assert.That(cp.ExteriorRing, Is.InstanceOf<CompoundCurve>(),
                "FCP-CP precondition: original shell is a CompoundCurve");
            Assert.That(copy.ExteriorRing, Is.InstanceOf<CompoundCurve>(),
                "FCP-CP: copied shell must also be a CompoundCurve, got "
                + copy.ExteriorRing.GetType().Name);
            Assert.That(copy.ExteriorRing, Is.Not.SameAs(cp.ExteriorRing),
                "FCP-CP: copy must be a deep copy of the shell");
        }

        [Test]
        public void FCP_CP_copy_preserves_arc_hole_subtype()
        {
            var cp = ReadCurvePolygon(WktCpArcHole);
            var copy = (CurvePolygon)cp.Copy();

            Assert.That(cp.GetInteriorRingN(0), Is.InstanceOf<CircularString>());
            Assert.That(copy.GetInteriorRingN(0), Is.InstanceOf<CircularString>(),
                "FCP-CP: copied hole must remain a CircularString, got "
                + copy.GetInteriorRingN(0).GetType().Name);
        }

        // ============================================================
        // FCP-TL — Linearize walks shell and holes
        // ============================================================

        [Test]
        public void FCP_TL_linearisation_walks_shell_and_holes()
        {
            var cp = ReadCurvePolygon(WktCpCompoundShellWithHole);
            var flat = cp.Linearize(arcSegmentLength: 0.1);

            Assert.That(flat, Is.InstanceOf<Polygon>(),
                "FCP-TL: linearisation result is a Polygon");

            // The compound shell densifies into many chord coords at fine
            // tolerance; the linear hole keeps its 5.
            Assert.That(flat.ExteriorRing.NumPoints, Is.GreaterThan(10),
                "FCP-TL: shell should be densified beyond the compound's "
                + "control points (got " + flat.ExteriorRing.NumPoints + ")");
            Assert.That(flat.GetInteriorRingN(0).NumPoints, Is.EqualTo(5),
                "FCP-TL: linear hole should pass through unchanged");
        }

        // ============================================================
        // FCP-WKT — round-trip preserves the structural tag
        // ============================================================

        [Test]
        public void FCP_WKT_roundtrip_preserves_compound_shell()
        {
            var cp = ReadCurvePolygon(WktCpCompoundShellWithHole);
            var emitted = _services.WKTWriter.Write(cp);

            Assert.That(emitted.ToUpperInvariant(), Does.Contain("COMPOUNDCURVE"),
                "FCP-WKT: emitted WKT must contain the COMPOUNDCURVE tag, got: " + emitted);

            var roundTripped = (CurvePolygon)_services.WKTReader.Read(emitted);
            Assert.That(roundTripped.ExteriorRing, Is.InstanceOf<CompoundCurve>(),
                "FCP-WKT: round-tripped shell must remain a CompoundCurve, got "
                + roundTripped.ExteriorRing.GetType().Name);
        }

        [Test]
        public void FCP_WKT_roundtrip_preserves_arc_shell()
        {
            var cp = ReadCurvePolygon(WktCpArcShell);
            var emitted = _services.WKTWriter.Write(cp);

            Assert.That(emitted.ToUpperInvariant(), Does.Contain("CIRCULARSTRING"),
                "FCP-WKT: emitted WKT must contain the CIRCULARSTRING tag, got: " + emitted);

            var roundTripped = (CurvePolygon)_services.WKTReader.Read(emitted);
            Assert.That(roundTripped.ExteriorRing, Is.InstanceOf<CircularString>(),
                "FCP-WKT: round-tripped shell must remain a CircularString, got "
                + roundTripped.ExteriorRing.GetType().Name);
        }

        // ============================================================
        // FCP-DOVE — not applicable in C#
        // ============================================================

        /// <summary>
        /// FCP-DOVE in the JTS-side epic is the dovetail decision for the
        /// legacy <c>Polygon.getExteriorRing(): LinearRing</c> API. NTS'
        /// <c>CurvePolygon</c> extends <c>Surface&lt;Curve&gt;</c>, not
        /// <c>Polygon</c>, so no legacy <c>LinearRing</c>-typed accessor
        /// constrains the design and the option-A/B/C choice never arises.
        /// </summary>
        /// <remarks>
        /// This test is the on-codebase record of that architectural fact.
        /// If a future refactor reintroduces a <c>LinearRing</c>-typed
        /// accessor on <c>CurvePolygon</c>, this test goes red and the
        /// epic §7 risk #1 conversation has to be re-opened.
        /// </remarks>
        [Test]
        public void FCP_DOVE_not_applicable_in_csharp()
        {
            var exteriorRingType = typeof(CurvePolygon).GetProperty(nameof(CurvePolygon.ExteriorRing))?.PropertyType;
            Assert.That(exteriorRingType, Is.Not.Null,
                "FCP-DOVE: CurvePolygon must expose an ExteriorRing accessor");
            Assert.That(exteriorRingType, Is.EqualTo(typeof(Curve)),
                "FCP-DOVE: CurvePolygon.ExteriorRing must be typed as Curve "
                + "(the polymorphic base of LinearRing / LineString / CircularString / "
                + "CompoundCurve). If this returns LinearRing again, the JTS-side "
                + "FCP-DOVE A/B/C dovetail decision needs to be made for this repo too.");
        }
    }
}
