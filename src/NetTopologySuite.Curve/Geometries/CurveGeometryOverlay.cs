using NetTopologySuite.Operation.Overlay;
using NetTopologySuite.Operation.OverlayNG;

namespace NetTopologySuite.Geometries
{
    /// <summary>
    /// Curve-aware geometry overlay strategies.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="OverlayNGCurve"/> is the default for
    /// <see cref="NtsCurveGeometryServices"/>. Ops mnemonics:
    /// <b>CAP</b> ∩ · <b>CUP</b> ∪ · <b>SUB</b> ∖ · <b>XOR</b> Δ
    /// (see NetTopologySuite.Proofs <c>docs/overlay-ng-curve-ops-mnemonics.md</c>).
    /// </para>
    /// <para>
    /// Phase 0: flattens curve elements for general pairs (no true circular
    /// noding yet). <b>F1</b> — before densify, same-operand algebra:
    /// G1 CAP A∩A→A · G2 CUP A∪A→A · G3 SUB A∖A→∅ · G4 XOR AΔA→∅
    /// (R1 retention via <c>Copy()</c>).
    /// </para>
    /// <para>
    /// Naming: use <b>Curve</b> (noun), never <c>Curved</c> (NTSC0001).
    /// </para>
    /// </remarks>
    public static class CurveGeometryOverlay
    {
        /// <summary>
        /// OverlayNG-backed curve strategy (CAP/CUP/SUB/XOR + F1 self short-circuit).
        /// </summary>
        public static GeometryOverlay OverlayNGCurve { get; } = new OverlayNGCurveImpl();

        /// <summary>
        /// Legacy alias used by <see cref="NtsCurveGeometryServices"/>.
        /// </summary>
        public static GeometryOverlay CurveV2 => OverlayNGCurve;

        /// <summary>
        /// Phase-0 OverlayNGCurve implementation.
        /// </summary>
        private sealed class OverlayNGCurveImpl : GeometryOverlay
        {
            protected override Geometry Overlay(Geometry geom0, Geometry geom1, SpatialFunction opCode)
            {
                // F1: fast algebra before flatten/densify
                if (AreSameOperand(geom0, geom1))
                    return OverlaySelf(geom0, opCode);

                // R2: densify path — honest approx not flagged yet (Phase 0 residual)
                return OverlayNGRobust.Overlay(Flatten(geom0), Flatten(geom1), opCode);
            }

            public override Geometry Union(Geometry a)
            {
                if (a == null)
                    return null;
                if (a.IsEmpty)
                    return a.Copy();

                // Unary CUP of an atomic geometry is identity (G2 family).
                // Multi* may need dissolve — fall through to NG after flatten.
                if (IsAtomic(a))
                    return a.Copy();

                return OverlayNGRobust.Union(Flatten(a));
            }

            /// <summary>
            /// G1–G4 self-ops (CAP/CUP keep me · SUB/XOR empty me). R1: no flatten.
            /// </summary>
            private static Geometry OverlaySelf(Geometry g, SpatialFunction opCode)
            {
                switch (opCode)
                {
                    case SpatialFunction.Intersection: // CAP G1
                    case SpatialFunction.Union:        // CUP G2
                        return g.Copy();
                    case SpatialFunction.Difference:     // SUB G3
                    case SpatialFunction.SymDifference:  // XOR G4
                        return OverlayOp.CreateEmptyResult(opCode, g, g, g.Factory);
                    default:
                        return OverlayNGRobust.Overlay(Flatten(g), Flatten(g), opCode);
                }
            }

            private static bool AreSameOperand(Geometry a, Geometry b)
            {
                if (ReferenceEquals(a, b))
                    return true;
                if (a == null || b == null)
                    return false;
                // Structural equality — FunctionRegistry often clones WKT twice.
                return a.EqualsExact(b);
            }

            private static bool IsAtomic(Geometry g)
            {
                // Multi* / non-homogeneous collections may need real unary union.
                return !(g is MultiPoint || g is MultiLineString || g is MultiPolygon
                         || g is MultiCurve || g is MultiSurface
                         || (g is GeometryCollection && !(g is CurvePolygon) && g.NumGeometries > 1));
            }

            /// <summary>
            /// Flattens curve elements; non-curve geometries are returned unchanged.
            /// </summary>
            private static Geometry Flatten(Geometry geom)
            {
                if (geom == null)
                    return null;

                if (!HasCurve(geom))
                    return geom;

                var factory = geom.Factory;
                var geometries = new Geometry[geom.NumGeometries];
                for (int i = 0; i < geom.NumGeometries; i++)
                {
                    var testGeom = geom.GetGeometryN(i);
                    switch (testGeom)
                    {
                        case GeometryCollection _:
                            geometries[i] = Flatten(testGeom);
                            break;
                        case ILinearizable<LineString> curve:
                            geometries[i] = curve.Linearize();
                            break;
                        case ILinearizable<Polygon> surface:
                            geometries[i] = surface.Linearize();
                            break;
                        default:
                            geometries[i] = testGeom;
                            break;
                    }
                }

                return factory.BuildGeometry(geometries);
            }

            private static bool HasCurve(Geometry geom)
            {
                for (int i = 0; i < geom.NumGeometries; i++)
                {
                    var testGeom = geom.GetGeometryN(i);
                    switch (testGeom)
                    {
                        case GeometryCollection _:
                            if (HasCurve(testGeom))
                                return true;
                            break;
                        case ILinearizable<LineString> _:
                        case ILinearizable<Polygon> _:
                            return true;
                    }
                }

                return false;
            }

            public override string ToString() => "OverlayNGCurve";
        }
    }
}
