// Fork-local port of upstream 2772c9b3 NetTopologySuite.Geometries.Surface<T>.
// See docs/DOVETAIL.md session 9.

using System;
using NetTopologySuite.Geometries;

namespace NetTopologySuite.Curved.Compat
{
    /// <summary>
    /// Abstract base class for geometries that have a <c>Dimension</c> of
    /// <see cref="Dimension.Surface"/> and only have <b>one</b> component.
    /// </summary>
    /// <typeparam name="T">Type of the rings; must inherit from <see cref="Curve"/>.</typeparam>
    [Serializable]
    public abstract class Surface<T> : Geometry, ISurface
        where T : Geometry
    {
        /// <summary>
        /// Creates an instance of this class.
        /// </summary>
        /// <param name="factory">The factory creating this surface.</param>
        protected Surface(GeometryFactory factory) : base(factory)
        {
        }

        /// <inheritdoc cref="Geometry.Dimension"/>
        public sealed override Dimension Dimension => Dimension.Surface;

        /// <inheritdoc cref="Geometry.BoundaryDimension"/>
        public sealed override Dimension BoundaryDimension => Dimension.Curve;

        /// <summary>
        /// Gets the exterior ring of the surface.
        /// </summary>
        public abstract T ExteriorRing { get; }

        /// <summary>
        /// Gets the number of interior rings inside the polygon.
        /// </summary>
        public abstract int NumInteriorRings { get; }

        /// <summary>
        /// Gets the interior ring at <paramref name="index"/>.
        /// </summary>
        /// <param name="index">The index of the requested ring.</param>
        /// <returns>An interior ring.</returns>
        public abstract T GetInteriorRingN(int index);
    }
}