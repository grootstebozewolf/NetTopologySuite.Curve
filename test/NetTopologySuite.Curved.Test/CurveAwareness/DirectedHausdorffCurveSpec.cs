using NetTopologySuite.Algorithm.Distance;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using NUnit.Framework;

namespace NetTopologySuite.Test.CurveAwareness
{
    /// <summary>
    /// Green pin for TAG <c>D-HF</c> (curve-aware discrete / directed Hausdorff)
    /// — JTS epic #1195 Phase 3, proofs #423.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Witness: asymmetric <c>CIRCULARSTRING (0 0, 2 3, 10 0)</c> vs baseline
    /// <c>LINESTRING (0 0, 10 0)</c>. Continuous directed Hausdorff (apex of the
    /// circle above the x-axis) is ≈ 3.96764. Control-point discrete densify
    /// under-estimates at mid-control height 3; arc-length densify via
    /// <see cref="CurveDiscreteHausdorffDistance"/> approaches continuous.
    /// </para>
    /// </remarks>
    [TestFixture]
    [Category("CurveAwareness")]
    [Category("D-HF")]
    public class DirectedHausdorffCurveSpec
    {
        /// <summary>
        /// Continuous directed Hausdorff h(arc, baseline) for the witness arc
        /// (analytic max height of the circle above the x-axis).
        /// </summary>
        private const double ExpectedContinuous = 3.96764;

        private const double Tol = 1e-3;

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

        private Geometry Read(string wkt) => _services.WKTReader.Read(wkt);

        /// <summary>
        /// D-HF Green: arc-length densify approaches continuous directed Hausdorff.
        /// </summary>
        [Test]
        public void D_HF_orientedHausdorff_samplesArcNotControlChords()
        {
            var arc = Read("CIRCULARSTRING (0 0, 2 3, 10 0)");
            var baseline = Read("LINESTRING (0 0, 10 0)");

            Assert.That(arc, Is.InstanceOf<CircularString>(),
                "D-HF: reader must keep CircularString identity");

            var cs = (CircularString)arc;

            // Control-point polyline: what core DiscreteHausdorff sees without
            // arc-length densify. Mid control (2,3) → height 3 on the baseline.
            var controlCoords = new Coordinate[cs.ControlPoints.Count];
            for (int i = 0; i < controlCoords.Length; i++)
                controlCoords[i] = cs.ControlPoints.GetCoordinate(i).Copy();
            var controlPolyline = _services.CreateGeometryFactory()
                .CreateLineString(controlCoords);

            double controlOnlyDist = new DiscreteHausdorffDistance(controlPolyline, baseline)
                .OrientedDistance();
            double controlChordDensifyDist = new DiscreteHausdorffDistance(controlPolyline, baseline)
            {
                DensifyFraction = 0.05
            }.OrientedDistance();

            // Product path: densify each arc by equal arc-length steps.
            double curveAwareDist = CurveDiscreteHausdorffDistance.OrientedDistance(
                cs, baseline, densifyFraction: 0.05);

            Assert.That(controlOnlyDist, Is.EqualTo(3.0).Within(1e-9),
                "control-only discrete should stay at mid-control height 3 (regression of the gap)");
            Assert.That(controlChordDensifyDist, Is.EqualTo(3.0).Within(1e-9),
                "control-chord densify cannot exceed mid-control height (chords inside the arc)");

            Assert.That(curveAwareDist, Is.EqualTo(ExpectedContinuous).Within(Tol),
                "D-HF: CurveDiscreteHausdorffDistance arc-length densify on "
                + "CIRCULARSTRING(0 0, 2 3, 10 0) vs LINESTRING(0 0, 10 0) should approach "
                + "continuous h≈" + ExpectedContinuous + "; got " + curveAwareDist
                + " (control-only=" + controlOnlyDist
                + ", control-chord densify=" + controlChordDensifyDist + ")");
        }

        /// <summary>
        /// Default densify fraction (0.05) matches the explicit 0.05 path on the witness.
        /// </summary>
        [Test]
        public void D_HF_defaultDensifyFraction_matchesExplicit()
        {
            var cs = (CircularString)Read("CIRCULARSTRING (0 0, 2 3, 10 0)");
            var baseline = Read("LINESTRING (0 0, 10 0)");

            double withDefault = CurveDiscreteHausdorffDistance.OrientedDistance(cs, baseline);
            double withExplicit = CurveDiscreteHausdorffDistance.OrientedDistance(cs, baseline, 0.05);

            Assert.That(withDefault, Is.EqualTo(withExplicit).Within(1e-12));
            Assert.That(withDefault, Is.EqualTo(ExpectedContinuous).Within(Tol));
        }
    }
}
