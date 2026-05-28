using System;
using System.IO;
using NetTopologySuite.Geometries;

namespace NetTopologySuite.IO
{
    /// <summary>
    /// Writes curve geometries (and ordinary geometries) to Well-Known Binary.
    /// </summary>
    /// <remarks>
    /// This writer inherits from <see cref="WKBWriter"/> and overrides the public virtual
    /// <see cref="WKBWriter.Write(Geometry)"/> / <see cref="WKBWriter.Write(Geometry, Stream)"/>
    /// entry points rather than relying on the <c>GetGeometryType</c> / <c>WriteOtherGeometry</c>
    /// / <c>GetOtherGeometryRequiredBufferSize</c> override hooks, which upstream removed on
    /// <c>develop</c>. At the top level it dispatches on curve vs ordinary; ordinary geometries
    /// go to <c>base.Write</c>. Curve geometries are written with a vendored header (whose flag
    /// math mirrors <c>WKBWriter.WriteHeader</c>) plus the protected coordinate-sequence helper
    /// and the surviving protected per-type write helpers for nested standard children.
    /// </remarks>
    public class CurveWKBWriter : WKBWriter
    {
        /// <summary>
        /// Creates an instance using the same defaults as the base writer.
        /// </summary>
        public CurveWKBWriter()
            : base()
        {
        }

        /// <inheritdoc/>
        public override byte[] Write(Geometry geometry)
        {
            if (!IsCurve(geometry))
                return base.Write(geometry);

            using (var ms = new MemoryStream())
            {
                Write(geometry, ms);
                return ms.ToArray();
            }
        }

        /// <inheritdoc/>
        public override void Write(Geometry geometry, Stream stream)
        {
            if (!IsCurve(geometry))
            {
                base.Write(geometry, stream);
                return;
            }

            BinaryWriter writer = EncodingType == ByteOrder.LittleEndian
                ? new BinaryWriter(stream)
                : new BEBinaryWriter(stream);
            try
            {
                WriteCurve(geometry, writer, true);
            }
            finally
            {
                ((IDisposable)writer).Dispose();
            }
        }

        private void WriteCurve(Geometry geom, BinaryWriter writer, bool includeSRID)
        {
            uint code;
            switch (geom)
            {
                case CircularString _: code = 8u; break;
                case CompoundCurve _: code = 9u; break;
                case CurvePolygon _: code = 10u; break;
                case MultiCurve _: code = 11u; break;
                case MultiSurface _: code = 12u; break;
                default:
                    throw new ArgumentException("Not a curve geometry: " + geom.GeometryType);
            }

            WriteCurveHeader(writer, geom, code, includeSRID);

            switch (geom)
            {
                case CircularString cs:
                    Write(cs.ControlPoints, true, writer);
                    break;
                case CompoundCurve cc:
                    writer.Write(cc.Curves.Count);
                    for (int i = 0; i < cc.Curves.Count; i++)
                        WriteChild(cc.Curves[i], writer);
                    break;
                case CurvePolygon cp:
                    if (cp.IsEmpty)
                    {
                        writer.Write(0);
                        break;
                    }
                    writer.Write(cp.NumInteriorRings + 1);
                    WriteChild(cp.ExteriorRing, writer);
                    for (int i = 0; i < cp.NumInteriorRings; i++)
                        WriteChild(cp.GetInteriorRingN(i), writer);
                    break;
                case MultiCurve mc:
                    writer.Write(mc.NumGeometries);
                    for (int i = 0; i < mc.NumGeometries; i++)
                        WriteChild(mc.GetGeometryN(i), writer);
                    break;
                case MultiSurface ms:
                    writer.Write(ms.NumGeometries);
                    for (int i = 0; i < ms.NumGeometries; i++)
                        WriteChild(ms.GetGeometryN(i), writer);
                    break;
            }
        }

        private void WriteChild(Geometry child, BinaryWriter writer)
        {
            if (IsCurve(child))
                WriteCurve(child, writer, false);
            else
                Write(child, writer, false);
        }

        // Mirrors WKBWriter.WriteHeader's flag math; the only difference is that we know the
        // curve type code (8..12) directly rather than going through GetGeometryType.
        private void WriteCurveHeader(BinaryWriter writer, Geometry geom, uint baseCurveCode, bool includeSRID)
        {
            writer.Write((byte)EncodingType);

            uint type = baseCurveCode;
            if ((HandleOrdinates & Ordinates.Z) == Ordinates.Z)
            {
                type += 1000;
                if (!Strict) type |= 0x80000000u;
            }
            if ((HandleOrdinates & Ordinates.M) == Ordinates.M)
            {
                type += 2000;
                if (!Strict) type |= 0x40000000u;
            }

            includeSRID &= HandleSRID;
            if (includeSRID)
                type |= 0x20000000u;

            writer.Write(type);

            if (includeSRID)
                writer.Write(geom.SRID);
        }

        private static bool IsCurve(Geometry g) =>
            g is CircularString || g is CompoundCurve || g is CurvePolygon
            || g is MultiCurve || g is MultiSurface;
    }
}
