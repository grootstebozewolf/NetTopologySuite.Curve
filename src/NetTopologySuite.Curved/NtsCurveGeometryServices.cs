using System;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace NetTopologySuite
{
    /// <summary>
    /// A geometry service provider class that supports curved geometries.
    /// </summary>
    public class NtsCurveGeometryServices : NtsGeometryServices
    {
        /// <summary>
        /// Creates a new instance of this class using the provided arguments.
        /// </summary>
        /// <remarks>
        /// The <see cref="GeometryOverlay"/> argument from <see cref="NtsCurveGeometryServices"/> constructor is set internally to
        /// <see cref="CurveGeometryOverlay.CurveV2"/>.
        /// </remarks>
        /// <param name="coordinateSequenceFactory">A coordinate sequence factory</param>
        /// <param name="precisionModel">A precision model</param>
        /// <param name="srid">A spatial reference identifier</param>
        /// <param name="coordinateEqualityComparer">A coordinate equality comparer</param>
        /// <param name="defaultArcSegmentLength">An arc segment length value that is used to flatten curved geometries. Must be positive.</param>
        public NtsCurveGeometryServices(CoordinateSequenceFactory coordinateSequenceFactory,
            PrecisionModel precisionModel, int srid, 
            CoordinateEqualityComparer coordinateEqualityComparer, double defaultArcSegmentLength)
            : base(coordinateSequenceFactory, precisionModel, srid,
                CurveGeometryOverlay.CurveV2, GeometryRelate.Legacy, coordinateEqualityComparer)
        {
            if (defaultArcSegmentLength < 0d)
                throw new ArgumentOutOfRangeException($"Must not be negative", nameof(defaultArcSegmentLength));

            DefaultArcSegmentLength = defaultArcSegmentLength;
            CurveWKTReader = new NetTopologySuite.IO.CurveWKTReader(this);
            CurveWKTWriter = new NetTopologySuite.IO.CurveWKTWriter(3);
            CurveWKBReader = new CurveWKBReader(this);
            CurveWKBWriter = new CurveWKBWriter();
        }

        /// <summary>
        /// Gets a reader that parses curve geometries (and ordinary geometries) from Well-Known Text.
        /// </summary>
        /// <remarks>
        /// The inherited <see cref="NtsGeometryServices.WKTReader"/> is a plain <see cref="WKTReader"/>
        /// and does not understand curve tagged text: upstream removed the override hook it used to rely
        /// on. Use this reader for curve WKT.
        /// </remarks>
        public NetTopologySuite.IO.CurveWKTReader CurveWKTReader { get; }

        /// <summary>
        /// Gets a writer that emits curve geometries (and ordinary geometries) as Well-Known Text.
        /// </summary>
        /// <remarks>
        /// The inherited <see cref="NtsGeometryServices.WKTWriter"/> is a plain <see cref="WKTWriter"/>
        /// and does not understand curve geometries: upstream removed the
        /// <c>AppendOtherGeometryTaggedText</c> override hook the old <c>WKTWriterEx</c> relied on.
        /// Use this writer for curve WKT (structural sibling of <see cref="CurveWKTReader"/>).
        /// </remarks>
        public NetTopologySuite.IO.CurveWKTWriter CurveWKTWriter { get; }

        /// <summary>
        /// Gets a reader that parses curve geometries (and ordinary geometries) from Well-Known Binary.
        /// </summary>
        public CurveWKBReader CurveWKBReader { get; }

        /// <summary>
        /// Gets a writer that emits curve geometries (and ordinary geometries) as Well-Known Binary.
        /// </summary>
        public CurveWKBWriter CurveWKBWriter { get; }

        /// <summary>
        /// Gets a value indicating the default arc segment length that is used to flatten curved geometries.
        /// </summary>
        private double DefaultArcSegmentLength { get; }

        /// <inheritdoc cref="CreateGeometryFactoryCore"/>
        protected override GeometryFactory CreateGeometryFactoryCore(
            PrecisionModel precisionModel, ElevationModel elevationModel, int srid,
            CoordinateSequenceFactory coordinateSequenceFactory)
        {
            return new CurveGeometryFactory(precisionModel, srid, coordinateSequenceFactory, this,
                DefaultArcSegmentLength);
        }
    }
}
