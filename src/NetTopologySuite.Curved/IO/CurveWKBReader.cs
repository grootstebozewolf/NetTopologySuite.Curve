using System;
using System.IO;
using NetTopologySuite.Geometries;

namespace NetTopologySuite.IO
{
    /// <summary>
    /// Reads curve geometries (and ordinary geometries) from Well-Known Binary.
    /// </summary>
    /// <remarks>
    /// This reader inherits from <see cref="WKBReader"/> and overrides the public virtual
    /// <see cref="WKBReader.Read(Stream)"/> entry point rather than relying on the
    /// <c>ReadOtherGeometry</c> override hook, which upstream removed on <c>develop</c>.
    /// At the top level it peeks the geometry type code; standard geometries are delegated
    /// to <c>base.Read</c>, curve geometries (codes 8–12) are parsed here. Nested standard
    /// children inside curve containers are read via the surviving protected base helpers
    /// (<c>ReadGeometry</c>, <c>ReadCoordinateSequenceLineString</c>).
    /// </remarks>
    public class CurveWKBReader : WKBReader
    {
        private readonly NtsCurveGeometryServices _services;

        /// <summary>
        /// Creates an instance using the supplied curve geometry services.
        /// </summary>
        public CurveWKBReader(NtsCurveGeometryServices services)
            : base(services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        /// <inheritdoc/>
        public override Geometry Read(Stream stream)
        {
            byte[] bytes = ReadAllBytes(stream);
            if (!IsCurveTopLevel(bytes))
                return base.Read(new MemoryStream(bytes));

            using (var ms = new MemoryStream(bytes))
            using (var reader = new BiEndianBinaryReader(ms))
                return ReadCurveGeometry(reader, _services.DefaultSRID);
        }

        private static byte[] ReadAllBytes(Stream stream)
        {
            if (stream is MemoryStream ms)
                return ms.ToArray();

            using (var buf = new MemoryStream())
            {
                stream.CopyTo(buf);
                return buf.ToArray();
            }
        }

        private static bool IsCurveTopLevel(byte[] bytes)
        {
            if (bytes.Length < 5)
                return false;

            var bo = (ByteOrder)bytes[0];
            uint t = bo == ByteOrder.LittleEndian
                ? (uint)(bytes[1] | (bytes[2] << 8) | (bytes[3] << 16) | (bytes[4] << 24))
                : (uint)((bytes[1] << 24) | (bytes[2] << 16) | (bytes[3] << 8) | bytes[4]);
            uint baseType = (t & 0xffff) % 1000;
            return baseType >= 8 && baseType <= 12;
        }

        private Geometry ReadCurveGeometry(BiEndianBinaryReader reader, int sridIn)
        {
            reader.Endianess = (ByteOrder)reader.ReadByte();
            uint type = reader.ReadUInt32();

            CoordinateSystem cs;
            if ((type & (0x80000000u | 0x40000000u)) == (0x80000000u | 0x40000000u))
                cs = CoordinateSystem.XYZM;
            else if ((type & 0x80000000u) == 0x80000000u)
                cs = CoordinateSystem.XYZ;
            else if ((type & 0x40000000u) == 0x40000000u)
                cs = CoordinateSystem.XYM;
            else
                cs = CoordinateSystem.XY;

            int srid = sridIn;
            if ((type & 0x20000000u) != 0)
            {
                int newSrid = reader.ReadInt32();
                if (HandleSRID && newSrid >= 0)
                    srid = newSrid;
            }

            uint ordinatePrefix = (type & 0xffff) / 1000;
            if (ordinatePrefix == 1) cs = CoordinateSystem.XYZ;
            else if (ordinatePrefix == 2) cs = CoordinateSystem.XYM;
            else if (ordinatePrefix == 3) cs = CoordinateSystem.XYZM;

            uint baseType = (type & 0xffff) % 1000;
            switch (baseType)
            {
                case 8u: return ReadCircularString(reader, cs, srid);
                case 9u: return ReadCompoundCurve(reader, srid);
                case 10u: return ReadCurvePolygon(reader, srid);
                case 11u: return ReadMultiCurve(reader, srid);
                case 12u: return ReadMultiSurface(reader, srid);
                default:
                    throw new ParseException("Curve-only dispatch reached non-curve type code: " + baseType);
            }
        }

        private CurveGeometryFactory Factory(int srid) =>
            (CurveGeometryFactory)_services.CreateGeometryFactory(
                _services.DefaultPrecisionModel, srid, _services.DefaultCoordinateSequenceFactory);

        private CircularString ReadCircularString(BiEndianBinaryReader reader, CoordinateSystem cs, int srid)
        {
            int numPoints = reader.ReadInt32();
            var sequence = ReadCoordinateSequenceLineString(reader, numPoints, cs);
            return Factory(srid).CreateCircularString(sequence);
        }

        private CompoundCurve ReadCompoundCurve(BiEndianBinaryReader reader, int srid)
        {
            int numCurves = reader.ReadInt32();
            var curves = new Geometry[numCurves];
            for (int i = 0; i < numCurves; i++)
                curves[i] = ReadChild(reader, srid);
            return Factory(srid).CreateCompoundCurve(curves);
        }

        private CurvePolygon ReadCurvePolygon(BiEndianBinaryReader reader, int srid)
        {
            int numRings = reader.ReadInt32();
            if (numRings == 0)
                return Factory(srid).CreateCurvePolygon();

            var exteriorRing = ReadChild(reader, srid);
            var interiorRings = new Geometry[numRings - 1];
            for (int i = 0; i < numRings - 1; i++)
                interiorRings[i] = ReadChild(reader, srid);

            return Factory(srid).CreateCurvePolygon(exteriorRing, interiorRings);
        }

        private MultiCurve ReadMultiCurve(BiEndianBinaryReader reader, int srid)
        {
            int numCurves = reader.ReadInt32();
            if (numCurves == 0)
                return Factory(srid).CreateMultiCurve();

            var curves = new Geometry[numCurves];
            for (int i = 0; i < numCurves; i++)
                curves[i] = ReadChild(reader, srid);

            return Factory(srid).CreateMultiCurve(curves);
        }

        private MultiSurface ReadMultiSurface(BiEndianBinaryReader reader, int srid)
        {
            int numSurfaces = reader.ReadInt32();
            if (numSurfaces == 0)
                return Factory(srid).CreateMultiSurface();

            var surfaces = new Geometry[numSurfaces];
            for (int i = 0; i < numSurfaces; i++)
                surfaces[i] = ReadChild(reader, srid);

            return Factory(srid).CreateMultiSurface(surfaces);
        }

        private Geometry ReadChild(BiEndianBinaryReader reader, int srid)
        {
            long pos = reader.BaseStream.Position;
            var savedEndianess = reader.Endianess;

            byte childBO = reader.ReadByte();
            reader.Endianess = (ByteOrder)childBO;
            uint childType = reader.ReadUInt32();
            uint childBase = (childType & 0xffff) % 1000;

            reader.BaseStream.Position = pos;
            reader.Endianess = savedEndianess;

            if (childBase >= 8 && childBase <= 12)
                return ReadCurveGeometry(reader, srid);

            return Read(reader);
        }
    }
}
