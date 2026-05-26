// =============================================================================
// NetTopologySuite.Curved.Compat.Curve
// -----------------------------------------------------------------------------
// Fork-local bridge to the upstream `Curve : Geometry, ILineal` abstract
// base.  See docs/DOVETAIL.md for context.
//
// During the transition on the pinned submodule commit 2772c9b3 this type
// derives from upstream `NetTopologySuite.Geometries.Curve` and adds
// nothing of its own.  The fork's curve types (CircularString,
// CompoundCurve) target this bridge instead of upstream Curve directly,
// so all of the following still work transparently while the upstream
// type exists:
//
//   * Inheritance: CircularString is-a Compat.Curve is-a upstream.Curve.
//     Pattern matches `if (x is Curve)` (upstream) on fork instances
//     continue to match.
//   * Type-parameter constraints: `Surface<T> where T : Curve` accepts
//     `Surface<Compat.Curve>` because Compat.Curve satisfies the
//     upstream constraint.
//   * Arrays / lists: `Curve[]` (upstream) keep accepting fork curve
//     types as covariant array members.
//
// At session 9 the submodule pin moves to upstream develop and the
// upstream `NetTopologySuite.Geometries.Curve` is gone.  The bridge
// `: NetTopologySuite.Geometries.Curve` line below stops compiling and
// is replaced with a verbatim port of upstream's 2772c9b3 Curve body
// (Dimension, BoundaryDimension, IsRing, IsClosed, StartPoint, EndPoint
// + ILineal marker) plus `: Geometry, ILineal`.  Fork types continue
// inheriting from Compat.Curve unchanged.
// =============================================================================

using System;
using NetTopologySuite.Geometries;

namespace NetTopologySuite.Curved.Compat
{
    /// <summary>
    /// Bridge to the upstream-removed
    /// <c>NetTopologySuite.Geometries.Curve</c> abstract base.  Fork-local
    /// curve types target this instead of the upstream type directly so
    /// the transition off the deleted <c>enhancement/curved</c> branch is
    /// a one-file edit (this file) at the bump.
    /// </summary>
    [Serializable]
    public abstract class Curve : NetTopologySuite.Geometries.Curve
    {
        /// <summary>
        /// Creates an instance of this class.
        /// </summary>
        /// <param name="factory">The factory creating this curve.</param>
        protected Curve(GeometryFactory factory) : base(factory)
        {
        }
    }
}
