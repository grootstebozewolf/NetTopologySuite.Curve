// =============================================================================
// NetTopologySuite.Curved.Compat.ILinearizable<T>
// -----------------------------------------------------------------------------
// Fork-local copy of the interface that the deleted upstream
// `enhancement/curved` branch contributed at
// `NetTopologySuite/src/NetTopologySuite/Geometries/ILinearizable.cs` and
// that was never merged into upstream develop.  See docs/DOVETAIL.md for
// the bigger picture.
//
// During the transition (sessions 2-8 of the dovetail plan) the fork's
// curve types implement BOTH this fork-local interface AND the upstream
// `NetTopologySuite.Geometries.ILinearizable<T>` that still exists on the
// pinned submodule commit 2772c9b3.  One method body satisfies both
// declarations.  Once session 9 bumps the submodule to upstream develop
// the upstream interface is gone, and the dual implementation collapses
// to this fork-local one.
//
// Pattern-match sites inside `src/NetTopologySuite.Curved/` are
// fully-qualified to this type so the fork-side dispatch survives the
// bump unchanged.
// =============================================================================

using NetTopologySuite.Geometries;

namespace NetTopologySuite.Curved.Compat
{
    /// <summary>
    /// Interface for geometries that can be either approximated to linear
    /// geometries themselves or their components.  Fork-local mirror of the
    /// upstream-removed <c>NetTopologySuite.Geometries.ILinearizable&lt;T&gt;</c>.
    /// </summary>
    /// <typeparam name="T">The type of the linearized geometry.</typeparam>
    public interface ILinearizable<out T> where T : Geometry
    {
        /// <summary>
        /// Approximates this geometry through linearization of non-linear
        /// components.
        /// </summary>
        /// <returns>A linearized approximation of this geometry.</returns>
        T Linearize();

        /// <summary>
        /// Approximates this geometry through linearization of non-linear
        /// components.
        /// </summary>
        /// <param name="arcSegmentLength">Maximum length of a linear segment in
        /// the approximation.</param>
        /// <returns>A linearized approximation of this geometry.</returns>
        T Linearize(double arcSegmentLength);
    }
}
