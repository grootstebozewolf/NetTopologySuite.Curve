// =============================================================================
// NetTopologySuite.Curved.Compat.ISurface
// -----------------------------------------------------------------------------
// Fork-local mirror of the upstream-removed
// `NetTopologySuite.Geometries.ISurface` marker interface.  See
// docs/DOVETAIL.md for context.
//
// On the pinned commit 2772c9b3 the upstream `ISurface` still exists.
// During the transition fork surface types (via Compat.Surface<T>)
// implement BOTH interfaces; the upstream one is kept so upstream
// pattern matches continue to work, the fork-local one prepares for
// the session 9 bump that removes the upstream interface.
// =============================================================================

using NetTopologySuite.Geometries;

namespace NetTopologySuite.Curved.Compat
{
    /// <summary>
    /// Marker for geometries that have <c>Dimension</c> of
    /// <see cref="NetTopologySuite.Geometries.Dimension.Surface"/> and only
    /// have one component.  Fork-local mirror of the upstream-removed
    /// <c>NetTopologySuite.Geometries.ISurface</c>.
    /// </summary>
    public interface ISurface : IPolygonal
    {
    }
}
