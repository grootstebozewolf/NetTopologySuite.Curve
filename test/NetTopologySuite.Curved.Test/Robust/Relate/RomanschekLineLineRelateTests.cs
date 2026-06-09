// =============================================================================
// NetTopologySuite.Curve.Robust.Relate.RomanschekLineLineRelateTests
// -----------------------------------------------------------------------------
// Differential tests: NTS Geometry.Relate() vs Romanschek et al. (IJGI 2021)
// pinned matrices from oracle/de9im_line_line_vectors.txt in
// NetTopologySuite.Proofs (issue #67 S3 seed).
//
// No RELATE_MATRIX oracle mode exists yet — this exercises the geometry
// carrier path the future oracle mode will diff against.
// =============================================================================

using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NUnit.Framework;

namespace NetTopologySuite.Test.Robust.Relate
{
    [TestFixture]
    public class RomanschekLineLineRelateTests
    {
        private static readonly WKTReader Reader = new WKTReader();

        [TestCase(
            "LINESTRING(682 623, 496 1104)",
            "LINESTRING(1 1, 513 1057)",
            "FF1FF0102",
            TestName = "Romanschek test 6")]
        [TestCase(
            "LINESTRING(1 1, 513 1057)",
            "LINESTRING(1 1, 513 1057)",
            "1FFF0FFF2",
            TestName = "Romanschek test 7")]
        [TestCase(
            "LINESTRING(1 1, 513 1057)",
            "LINESTRING(353 727, 161 331)",
            "101FF0FF2",
            TestName = "Romanschek test 8")]
        [TestCase(
            "LINESTRING(673 1387, 1 1)",
            "LINESTRING(1 1, 513 1057)",
            "101F00FF2",
            TestName = "Romanschek test 9")]
        [TestCase(
            "LINESTRING(241 496, 297 604)",
            "LINESTRING(1 1, 513 1057)",
            "FF10F0102",
            TestName = "Romanschek test 10")]
        [TestCase(
            "LINESTRING(190 389, 200 413)",
            "LINESTRING(1 1, 513 1057)",
            "0F1FF0102",
            TestName = "Romanschek test 13")]
        public void NtsRelate_MatchesPinnedMatrix(string wktA, string wktB, string expected)
        {
            var a = Reader.Read(wktA);
            var b = Reader.Read(wktB);
            var actual = a.Relate(b).ToString();
            Assert.That(actual, Is.EqualTo(expected));
        }
    }
}