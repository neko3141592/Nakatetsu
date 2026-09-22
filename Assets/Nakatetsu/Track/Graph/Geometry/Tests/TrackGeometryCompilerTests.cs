using NUnit.Framework;
using UnityEngine;
using Nakatetsu.Track.Graph.Geometry;

namespace Nakatetsu.Track.Graph.Geometry.Tests
{
    public sealed class TrackGeometryCompilerTests
    {
        [Test]
        public void RebuildStoresCurveEndpointAndHeightAcrossGradientGap()
        {
            var definition = new TrackGeometryDefinition { originPosition = new Vector3(10, 5, 20) };
            float arcLength = Mathf.PI * 50f;
            definition.horizontalSegments.Add(new TrackGeometryHorizontalSegment
            { lengthM = arcLength, trackCurveType = TrackGeometryCurveType.Curve, radiusM = 100 });
            definition.horizontalSegments.Add(new TrackGeometryHorizontalSegment
            { startDistanceM = arcLength, lengthM = 20 });
            definition.verticalSegments.Add(new TrackGeometryVerticalSegment
            { lengthM = 50, startGradientPermille = 0, endGradientPermille = 20 });
            definition.verticalSegments.Add(new TrackGeometryVerticalSegment
            { startDistanceM = 75, lengthM = 25, startGradientPermille = 20, endGradientPermille = 0 });

            var context = new TrackGeometryContext();
            TrackGeometryCompiler.Rebuild(definition, context);
            var start = context.Workspace.horizontalStarts[1];
            Assert.That(start.segmentIndex, Is.EqualTo(1));
            Assert.That(start.startDistanceM, Is.EqualTo(arcLength));
            Assert.That(start.endDistanceM, Is.EqualTo(arcLength + 20));
            Assert.That(Vector3.Distance(start.position, new Vector3(110, 5, 120)), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(start.rotation * Vector3.forward, Vector3.right), Is.LessThan(.001f));
            Assert.That(context.Workspace.verticalStarts[1].heightM, Is.EqualTo(1).Within(.0001));
        }

        [Test]
        public void RebuildReplacesPreviousCacheAndNullClearsIt()
        {
            var definition = new TrackGeometryDefinition();
            definition.horizontalSegments.Add(new TrackGeometryHorizontalSegment());
            var context = new TrackGeometryContext();
            TrackGeometryCompiler.Rebuild(definition, context);
            definition.originPosition = Vector3.right;
            TrackGeometryCompiler.Rebuild(definition, context);
            Assert.That(context.Workspace.horizontalStarts.Count, Is.EqualTo(1));
            Assert.That(context.Workspace.horizontalStarts[0].position, Is.EqualTo(Vector3.right));
            TrackGeometryCompiler.Rebuild(null, context);
            Assert.That(context.Workspace.horizontalStarts, Is.Empty);
            Assert.That(context.Workspace.verticalStarts, Is.Empty);
        }
    }
}
