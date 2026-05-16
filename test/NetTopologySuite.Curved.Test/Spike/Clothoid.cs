// =============================================================================
// NetTopologySuite.Test.Spike.Clothoid
// -----------------------------------------------------------------------------
// SPIKE helper.  Builds an Euler-spiral (clothoid) segment from G^1 Hermite
// data (P0, P1, kappa_0, kappa_1) by delegating the chord-length parameter L
// inverse problem to the proprietary clothoid-halley-coq Clothoid.Halley
// library, then linearises by direct trapezoidal-rule integration of the
// parametric form.
//
// SCOPE
// =====
// This type lives in the TEST assembly, NOT the production
// NetTopologySuite.Curve library.  The production assembly targets
// netstandard2.0; Clothoid.Halley targets net8.0.  Promoting Clothoid to
// production would require either multi-targeting Clothoid.Halley to
// include netstandard2.0 (a change to the proprietary upstream), multi-
// targeting NetTopologySuite.Curve to include net8.0 (a sweeping change),
// or re-implementing the Halley solver locally (a duplicate-code risk
// since the upstream reference has a 9,058-record bit-exact golden corpus).
// None of those is in scope for this spike; the goal here is to validate
// that the chord-clothoid-chord shape can be carried through the existing
// Robust.* predicate layer end-to-end and that the Azimuth turn-sign
// bridge gives non-degenerate decisions at the join points.
//
// MATH (Bertolazzi-Frego L-form, normalised tau in [0, 1])
// --------------------------------------------------------
//   psi(tau)  = kappa_0 * tau + 1/2 (kappa_1 - kappa_0) * tau^2     (heading)
//   x_norm(tau) = L * integral_0^tau cos(L * psi(s)) ds            (in metres)
//   y_norm(tau) = L * integral_0^tau sin(L * psi(s)) ds            (in metres)
//
// In normalised coordinates the clothoid starts at the origin with tangent
// along +x.  The endpoint at tau=1 lands at (L*P(L), L*Q(L)) where
// P(L), Q(L) are the moment integrals computed by ClothoidSolver.  After
// solving for L, we rotate so the normalised endpoint aligns with the
// world-space chord (P1 - P0) and translate by P0.
// =============================================================================

using System;
using Clothoid.Halley;
using NetTopologySuite.Geometries;

namespace NetTopologySuite.Test.Spike
{
    /// <summary>
    /// Spike-only clothoid helper.  See file header for scope notes.
    /// </summary>
    internal sealed class Clothoid
    {
        /// <summary>Start point of the clothoid (world coords).</summary>
        public Coordinate P0 { get; }

        /// <summary>End point of the clothoid (world coords).</summary>
        public Coordinate P1 { get; }

        /// <summary>Curvature at the start (signed: positive = left turn).</summary>
        public double Kappa0 { get; }

        /// <summary>Curvature at the end (signed: positive = left turn).</summary>
        public double Kappa1 { get; }

        /// <summary>Arc length L of the clothoid in metres, recovered from the
        /// G^1 Hermite data via the upstream Halley solver.</summary>
        public double L { get; }

        /// <summary>Number of Halley iterations consumed (for diagnostics).
        /// A value of <see cref="ClothoidSolver.MaxIterDefault"/> means the
        /// solver did not converge within the iteration budget.</summary>
        public int HalleyIterations { get; }

        /// <summary>
        /// Construct a clothoid from G^1 Hermite data.  Solves for L via the
        /// upstream Halley solver; stores the recovered L plus iteration count.
        /// </summary>
        /// <param name="p0">Start point.</param>
        /// <param name="p1">End point.</param>
        /// <param name="kappa0">Curvature at <paramref name="p0"/>.</param>
        /// <param name="kappa1">Curvature at <paramref name="p1"/>.</param>
        public Clothoid(Coordinate p0, Coordinate p1, double kappa0, double kappa1)
        {
            P0 = p0 ?? throw new ArgumentNullException(nameof(p0));
            P1 = p1 ?? throw new ArgumentNullException(nameof(p1));
            Kappa0 = kappa0;
            Kappa1 = kappa1;

            var result = ClothoidSolver.SolveHalleyL(
                new[] { p0.X, p0.Y },
                new[] { p1.X, p1.Y },
                kappa0,
                kappa1);

            L = result.L;
            HalleyIterations = result.Iterations;
        }

        /// <summary>
        /// Sample the clothoid at <c>samples + 1</c> points uniformly in
        /// parameter <c>tau</c> over <c>[0, 1]</c>.  Returned array's first
        /// element is <see cref="P0"/> and last element approximately equals
        /// <see cref="P1"/> (modulo the numerical integration error of the
        /// trapezoidal rule used here, currently &lt;= 1e-6 metres for
        /// typical inputs).
        /// </summary>
        /// <param name="samples">Number of sub-intervals across the clothoid.
        /// Must be at least 2.</param>
        public Coordinate[] Linearize(int samples)
        {
            if (samples < 2)
                throw new ArgumentOutOfRangeException(
                    nameof(samples), "samples must be >= 2");

            // Sub-step count per sample interval for the trapezoidal accumulation.
            // 64 sub-steps per interval is empirically good enough for ~1e-7
            // accuracy on the smooth integrands cos(L*psi) and sin(L*psi).
            const int subStepsPerInterval = 64;
            int totalSteps = samples * subStepsPerInterval;
            double dtau = 1.0 / totalSteps;
            double halfDk = 0.5 * (Kappa1 - Kappa0);

            // Normalised local coordinates at each sample boundary.  xs[0] = ys[0] = 0
            // (clothoid starts at origin with tangent along +x in normalised frame).
            double[] xs = new double[samples + 1];
            double[] ys = new double[samples + 1];
            double xAcc = 0.0;
            double yAcc = 0.0;
            double cPrev = 1.0;       // cos(L * psi(0)) = cos(0) = 1
            double sPrev = 0.0;       // sin(L * psi(0)) = sin(0) = 0
            int sampleIdx = 1;

            for (int step = 1; step <= totalSteps; step++)
            {
                double tau = step * dtau;
                double psi = Kappa0 * tau + halfDk * tau * tau;
                double phi = L * psi;
                double c = Math.Cos(phi);
                double s = Math.Sin(phi);

                // Trapezoidal-rule increment: integrate cos / sin over [tau-dtau, tau].
                xAcc += 0.5 * dtau * (cPrev + c);
                yAcc += 0.5 * dtau * (sPrev + s);
                cPrev = c;
                sPrev = s;

                if (step % subStepsPerInterval == 0)
                {
                    // Multiply by L to convert tau-parameter integrals into metre
                    // coordinates: dx/dtau = L * cos(L*psi(tau)).
                    xs[sampleIdx] = L * xAcc;
                    ys[sampleIdx] = L * yAcc;
                    sampleIdx++;
                }
            }

            // Align normalised frame with world frame: rotate so that the
            // normalised endpoint (xs[samples], ys[samples]) ends up at
            // (P1.X - P0.X, P1.Y - P0.Y), then translate by P0.
            double targetX = P1.X - P0.X;
            double targetY = P1.Y - P0.Y;
            double endX = xs[samples];
            double endY = ys[samples];

            // theta0 = angle(target) - angle(normalised endpoint).
            // (atan2 of (0, 0) is 0 in .NET; a degenerate clothoid -- L == 0 --
            //  hits this branch and we early-return a zero-length polyline.)
            double theta0;
            if (L == 0.0 || (endX == 0.0 && endY == 0.0))
            {
                theta0 = Math.Atan2(targetY, targetX);
            }
            else
            {
                theta0 = Math.Atan2(targetY, targetX) - Math.Atan2(endY, endX);
            }
            double cos0 = Math.Cos(theta0);
            double sin0 = Math.Sin(theta0);

            var result = new Coordinate[samples + 1];
            for (int i = 0; i <= samples; i++)
            {
                double xr = cos0 * xs[i] - sin0 * ys[i];
                double yr = sin0 * xs[i] + cos0 * ys[i];
                result[i] = new Coordinate(xr + P0.X, yr + P0.Y);
            }
            return result;
        }
    }
}
