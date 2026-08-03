using System;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;

namespace NetTopologySuite.Algorithm.Distance
{
    /// <summary>
    /// Curve-aware discrete / directed Hausdorff distance (TAG D-HF).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Core <see cref="DiscreteHausdorffDistance"/> densifies straight control
    /// chords (or relies on a prior <c>Linearize()</c>). This class densifies
    /// <see cref="CircularString"/> / <see cref="CompoundCurve"/> by
    /// <em>arc length</em>: each arc is sampled at equal arc-length steps
    /// controlled by a densify fraction, matching the SFA curve awareness epic
    /// D-HF contract (JTS #1195 Phase 3; proofs #423).
    /// </para>
    /// <para>
    /// Directed form: <c>h(A,B) = max<sub>a∈A</sub> min<sub>b∈B</sub> d(a,b)</c>
    /// (Huttenlocher–Klanderman–Rucklidge). Non-curve inputs fall through to
    /// <see cref="DiscreteHausdorffDistance"/>.
    /// </para>
    /// </remarks>
    public static class CurveDiscreteHausdorffDistance
    {
        /// <summary>
        /// Default densify fraction (equal-arc-length steps ≈ 1/20 of each arc).
        /// </summary>
        public const double DefaultDensifyFraction = 0.05;

        /// <summary>
        /// Oriented (directed) discrete Hausdorff from <paramref name="a"/> to
        /// <paramref name="b"/>, densifying curve components by arc length at
        /// <see cref="DefaultDensifyFraction"/>.
        /// </summary>
        public static double OrientedDistance(Geometry a, Geometry b)
        {
            return OrientedDistance(a, b, DefaultDensifyFraction);
        }

        /// <summary>
        /// Oriented (directed) discrete Hausdorff from <paramref name="a"/> to
        /// <paramref name="b"/> with arc-length densify fraction.
        /// </summary>
        /// <param name="a">Query geometry (samples taken on this side).</param>
        /// <param name="b">Target geometry (nearest-point distance).</param>
        /// <param name="densifyFraction">
        /// Fraction of each arc's length between sample points, in (0, 1].
        /// <c>0.05</c> ≈ 20 equal-arc-length steps per arc (plus endpoints).
        /// </param>
        /// <returns>The oriented discrete Hausdorff distance h(a,b).</returns>
        public static double OrientedDistance(Geometry a, Geometry b, double densifyFraction)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (a.IsEmpty || b.IsEmpty)
                return 0d;
            if (densifyFraction <= 0d || densifyFraction > 1d)
                throw new ArgumentOutOfRangeException(nameof(densifyFraction),
                    "Fraction is not in range (0.0 - 1.0]");

            if (a is CircularString cs)
                return OrientedFromCircularString(cs, b, densifyFraction);
            if (a is CompoundCurve cc)
                return OrientedFromCompoundCurve(cc, b, densifyFraction);

            // Non-curve query: core discrete Hausdorff (chord densify).
            var dist = new DiscreteHausdorffDistance(a, b)
            {
                DensifyFraction = densifyFraction
            };
            return dist.OrientedDistance();
        }

        /// <summary>
        /// Symmetric discrete Hausdorff <c>max(h(a,b), h(b,a))</c> with
        /// arc-length densify on whichever operand is a curve.
        /// </summary>
        public static double Distance(Geometry a, Geometry b, double densifyFraction = DefaultDensifyFraction)
        {
            return Math.Max(
                OrientedDistance(a, b, densifyFraction),
                OrientedDistance(b, a, densifyFraction));
        }

        private static double OrientedFromCompoundCurve(CompoundCurve cc, Geometry target,
            double densifyFraction)
        {
            double max = 0d;
            foreach (var member in cc.Curves)
            {
                double h = member is CircularString cs
                    ? OrientedFromCircularString(cs, target, densifyFraction)
                    : OrientedDistance(member, target, densifyFraction);
                if (h > max) max = h;
            }
            return max;
        }

        private static double OrientedFromCircularString(CircularString cs, Geometry target,
            double densifyFraction)
        {
            int numArcs = cs.NumArcs;
            if (numArcs == 0)
            {
                // Degenerate: fall back to control vertices only.
                return OrientedFromCoordinates(cs.ControlPoints, target);
            }

            var maxPtDist = new PointPairDistance();
            int numSubSegs = Math.Max(1, (int)Math.Round(1.0 / densifyFraction, MidpointRounding.ToEven));

            for (int i = 0; i < numArcs; i++)
            {
                var arc = cs.GetArcN(i);
                SampleArcByArcLength(arc, numSubSegs, target, maxPtDist);
            }

            return maxPtDist.Distance;
        }

        private static double OrientedFromCoordinates(CoordinateSequence seq, Geometry target)
        {
            var maxPtDist = new PointPairDistance();
            for (int i = 0; i < seq.Count; i++)
            {
                var minPtDist = new PointPairDistance();
                DistanceToPoint.ComputeDistance(target, seq.GetCoordinate(i), minPtDist);
                maxPtDist.SetMaximum(minPtDist);
            }
            return maxPtDist.Distance;
        }

        /// <summary>
        /// Sample one circular arc at equal arc-length steps (plus endpoints) and
        /// accumulate max nearest-distance to <paramref name="target"/>.
        /// </summary>
        private static void SampleArcByArcLength(CircularArc arc, int numSubSegs, Geometry target,
            PointPairDistance maxPtDist)
        {
            // Endpoints always included.
            UpdateMax(target, arc.P0, maxPtDist);
            UpdateMax(target, arc.P2, maxPtDist);

            // Collinear / degenerate arc: also mid control, then chord densify.
            if (double.IsPositiveInfinity(arc.Radius) || double.IsNaN(arc.Angle))
            {
                UpdateMax(target, arc.P1, maxPtDist);
                ChordSample(arc.P0, arc.P2, numSubSegs, target, maxPtDist);
                return;
            }

            var center = arc.Center;
            double r = arc.Radius;
            // Oriented sweep P0 → P1 → P2 (same sign convention as CircularArc.Angle).
            double a0 = AngleUtility.Angle(center, arc.P0);
            double sweep01 = AngleUtility.AngleBetweenOriented(arc.P0, center, arc.P1);
            double sweep12 = AngleUtility.AngleBetweenOriented(arc.P1, center, arc.P2);
            if (Math.Sign(sweep12) != Math.Sign(sweep01) && sweep01 != 0d)
                sweep12 += -Math.Sign(sweep12) * AngleUtility.PiTimes2;
            double totalSweep = sweep01 + sweep12;
            if (Math.Abs(totalSweep) < 1e-15)
            {
                UpdateMax(target, arc.P1, maxPtDist);
                return;
            }

            // Equal arc-length steps ≡ equal angle steps on a circle.
            for (int i = 1; i < numSubSegs; i++)
            {
                double t = (double)i / numSubSegs;
                double angle = a0 + t * totalSweep;
                var pt = new Coordinate(
                    center.X + r * Math.Cos(angle),
                    center.Y + r * Math.Sin(angle));
                UpdateMax(target, pt, maxPtDist);
            }

            // Mid control is on the arc; include it so densify never drops a
            // known on-arc vertex (matches DiscreteHausdorff vertex coverage).
            UpdateMax(target, arc.P1, maxPtDist);
        }

        private static void ChordSample(Coordinate p0, Coordinate p1, int numSubSegs, Geometry target,
            PointPairDistance maxPtDist)
        {
            double delx = (p1.X - p0.X) / numSubSegs;
            double dely = (p1.Y - p0.Y) / numSubSegs;
            for (int i = 1; i < numSubSegs; i++)
            {
                UpdateMax(target, new Coordinate(p0.X + i * delx, p0.Y + i * dely), maxPtDist);
            }
        }

        private static void UpdateMax(Geometry target, Coordinate pt, PointPairDistance maxPtDist)
        {
            var minPtDist = new PointPairDistance();
            DistanceToPoint.ComputeDistance(target, pt, minPtDist);
            maxPtDist.SetMaximum(minPtDist);
        }
    }
}
