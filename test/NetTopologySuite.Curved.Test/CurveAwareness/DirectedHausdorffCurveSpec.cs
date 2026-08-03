using NetTopologySuite.Algorithm.Distance;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using NUnit.Framework;

namespace NetTopologySuite.Test.CurveAwareness
{
    /// <summary>
    /// Red product pin for TAG <c>D-HF</c> (curve-aware discrete / directed
    /// Hausdorff) — JTS epic #1195 Phase 3, proofs epic #423.
    /// </summary>
    /// <remarks>
    /// <para>
    /// JTS companion: <c>CurveAwarenessSpecTest.test_D_HF_hausdorffFrechetCurveAware</c>
    /// on <c>feature/sfa-curve-rgr</c>. Same asymmetric-arc witness: control-point
    /// discrete Hausdorff under-estimates the continuous directed max-min because
    /// densify walks the control chords, not the arc.
    /// </para>
    /// <para>
    /// Continuous apex of the circle through (0,0), (2,3), (10,0) relative to the
    /// x-axis baseline is ≈ 3.96764 (centre (5, −7/6), r ≈ 5.134). Control-only
    /// and chord-fraction densify both land near the mid control height 3.
    /// </para>
    /// <para>
    /// Spec (delete-on-green for the intentional red assertion): densify / sample
    /// by arc length so discrete h(A,B) approaches the continuous directed
    /// Hausdorff. NTS already has <see cref="DiscreteHausdorffDistance"/> with
    /// <see cref="DiscreteHausdorffDistance.OrientedDistance()"/>; the curve-aware
    /// path is the gap. <c>DirectedHausdorffDistance</c> (JTS 1.21 / NTS#812) is
    /// the longer-term continuous-style port — same witness applies.
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
        /// D-HF: oriented discrete Hausdorff on an asymmetric CircularString must
        /// approach the continuous arc apex by sampling along arc length, not by
        /// walking control points / control-chord fractions alone.
        /// </summary>
        [Test]
        public void D_HF_orientedHausdorff_samplesArcNotControlChords()
        {
            var arc = Read("CIRCULARSTRING (0 0, 2 3, 10 0)");
            var baseline = Read("LINESTRING (0 0, 10 0)");

            Assert.That(arc, Is.InstanceOf<CircularString>(),
                "D-HF: reader must keep CircularString identity");

            var cs = (CircularString)arc;
            // Control-point polyline (what DiscreteHausdorff sees if it only walks
            // CircularString.ControlPoints / inherited coordinate sequence without
            // arc-length densify). Mid control (2,3) → height 3 on the baseline.
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

            // What Linearize() already gives: dense chord approximation of the arc.
            // D-HF is red because DiscreteHausdorffDistance has no arc-length densify
            // parameter on the curve itself — callers must Linearize first, and the
            // densify fraction still walks polyline chords of whatever was linearized.
            double linearizedDist = new DiscreteHausdorffDistance(cs.Linearize(), baseline)
                .OrientedDistance();

            // Red ratchet: the control-point discrete reading must match continuous h.
            // Today control-only ≈ 3 and chord densify on controls cannot exceed 3
            // (chords lie inside the arc). Linearize() may approach ExpectedContinuous
            // as a workaround, but that is densify-via-flatten, not D-HF arc sampling.
            Assert.That(controlOnlyDist, Is.EqualTo(ExpectedContinuous).Within(Tol),
                "D-HF: oriented DiscreteHausdorffDistance on CIRCULARSTRING(0 0, 2 3, 10 0) "
                + "vs LINESTRING(0 0, 10 0) should approach continuous h≈" + ExpectedContinuous
                + " by sampling along arc length without forcing Linearize(); "
                + "control-only got " + controlOnlyDist
                + ", control-chord densify(frac=0.05) got " + controlChordDensifyDist
                + ", Linearize() workaround got " + linearizedDist
                + " (control path ≈ mid-control height 3 — densify walks control chords, not the arc).");
        }
    }
}
