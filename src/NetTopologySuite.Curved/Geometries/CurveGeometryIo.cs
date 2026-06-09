using NetTopologySuite.IO;

namespace NetTopologySuite.Geometries
{
    internal static class CurveGeometryIo
    {
        internal static string ToText(Geometry geometry)
        {
            var services = (NtsCurveGeometryServices)((CurveGeometryFactory)geometry.Factory).GeometryServices;
            return services.CurveWKTWriter.Write(geometry);
        }

        internal static byte[] ToBinary(Geometry geometry)
        {
            var services = (NtsCurveGeometryServices)((CurveGeometryFactory)geometry.Factory).GeometryServices;
            return services.CurveWKBWriter.Write(geometry);
        }
    }
}