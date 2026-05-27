// =============================================================================
// NetTopologySuite.Curved.Compat.Surface<T>
// -----------------------------------------------------------------------------
// Fork-local bridge to the upstream-removed
// `NetTopologySuite.Geometries.Surface<T>` abstract base.  See
// docs/DOVETAIL.md for context.
//
// On the pinned commit 2772c9b3 this bridge extends upstream Surface<T>
// and additionally implements the fork-local ISurface marker, so that
//
//   * Pattern matches on upstream Surface<T> or ISurface (in upstream
//     code, and in fork code like MultiSurface's ISurface check) keep
//     matching fork instances of CurvePolygon transparently.
//   * Fork-side pattern matches that use the fork-local
//     `NetTopologySuite.Curved.Compat.ISurface` also match, which
//     prepares those call sites for the session 9 bump.
//
// At session 9 the upstream Surface<T> and ISurface disappear.  The
// `: NetTopologySuite.Geometries.Surface<T>` line below stops compiling
// and is replaced with a verbatim port of upstream's 2772c9b3 Surface<T>
// body (Dimension.Surface, BoundaryDimension, ExteriorRing/
// NumInteriorRings/GetInteriorRingN abstracts, etc.) plus `: Geometry,
// ISurface` (the fork-local ISurface).  Fork types continue inheriting
// from Compat.Surface<T> unchanged.
// =============================================================================

using System;
using NetTopologySuite.Geometries;

namespace NetTopologySuite.Curved.Compat
{
    /// <summary>
    /// Bridge to the upstream-removed
    /// <c>NetTopologySuite.Geometries.Surface&lt;T&gt;</c> abstract base.
    /// Fork-local surface types target this instead of the upstream type
    /// directly so the transition off the deleted
    /// <c>enhancement/curved</c> branch is a one-file edit (this file)
    /// at the bump.
    /// </summary>
    /// <typeparam name="T">Type of the rings, must inherit from upstream
    /// <c>Curve</c> (which the fork-local <c>Compat.Curve</c> bridge
    /// satisfies).</typeparam>
    [Serializable]
    public abstract class Surface<T>
        : NetTopologySuite.Geometries.Surface<T>,
          NetTopologySuite.Curved.Compat.ISurface
        where T : NetTopologySuite.Geometries.Curve
    {
        /// <summary>
        /// Creates an instance of this class.
        /// </summary>
        /// <param name="factory">The factory creating this surface.</param>
        protected Surface(GeometryFactory factory) : base(factory)
        {
        }
    }
}
