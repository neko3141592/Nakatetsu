using System;
using System.Collections.Generic;
using Nakatetsu.Track.Graph.Geometry;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Nakatetsu.Track.Graph.Edge.Tests
{
    public sealed class TrackEdgeVariableOffsetSegmentTests
    {
        [Test]
        public void LinearOffsetUsesAbsoluteGeometryDistanceAndConstantSlope()
        {
            var edge = new TrackEdgeDefinition
            { startDistanceOnGeometryM = 120f, endDistanceOnGeometryM = 20f };
            edge.offsetSegments.Add(new TrackEdgeLinearOffsetSegment
            {
                startDistanceOnGeometryM = 20f, endDistanceOnGeometryM = 120f,
                startOffsetM = -5f, endOffsetM = 5f
            });

            foreach (var (distance, expected) in new[] { (20f, -5f), (70f, 0f), (120f, 5f) })
            {
                Assert.That(edge.TryEvaluateOffsetAtGeometryDistance(distance, out float offset, out float derivative), Is.True);
                Assert.That(offset, Is.EqualTo(expected).Within(0.00001f));
                Assert.That(derivative, Is.EqualTo(0.1f).Within(0.000001f));
            }
        }

        [Test]
        public void ArcMatchesAnOffsetSegmentFromLegacyTrackJson()
        {
            var edge = new TrackEdgeDefinition
            { startDistanceOnGeometryM = 1370f, endDistanceOnGeometryM = 1396.2001f };
            edge.offsetSegments.Add(new TrackEdgeArcOffsetSegment
            {
                startDistanceOnGeometryM = 1370f, endDistanceOnGeometryM = 1396.2001f,
                startOffsetM = -2.5f, radiusM = 400f, startHeadingDeg = 0f
            });

            Assert.That(edge.TryEvaluateOffsetAtGeometryDistance(1370f, out float start, out float startSlope), Is.True);
            Assert.That(start, Is.EqualTo(-2.5f).Within(0.000001f));
            Assert.That(startSlope, Is.Zero.Within(0.000001f));
            Assert.That(edge.TryEvaluateOffsetAtGeometryDistance(1396.2001f, out float end, out float endSlope), Is.True);
            Assert.That(end, Is.EqualTo(-1.641026f).Within(0.0001f));
            Assert.That(endSlope, Is.EqualTo(0.06564f).Within(0.0001f));
        }

        [Test]
        public void NegativeArcRadiusBendsTowardNegativeOffset()
        {
            var arc = new TrackEdgeArcOffsetSegment
            { startDistanceOnGeometryM = 0f, endDistanceOnGeometryM = 10f,
                startOffsetM = 0f, radiusM = -100f, startHeadingDeg = 0f };
            Assert.That(arc.EvaluateOffsetM(10f), Is.EqualTo(-0.501256f).Within(0.00001f));
            Assert.That(arc.EvaluateDerivative(10f), Is.EqualTo(-0.100504f).Within(0.00001f));
        }

        [TestCase(0f, 0f, 10f)]
        [TestCase(100f, 90f, 10f)]
        [TestCase(5f, 0f, 10f)]
        public void InvalidArcFailsThroughEdgeContract(float radiusM, float headingDeg, float lengthM)
        {
            var edge = new TrackEdgeDefinition
            { startDistanceOnGeometryM = 0f, endDistanceOnGeometryM = lengthM };
            edge.offsetSegments.Add(new TrackEdgeArcOffsetSegment
            { startDistanceOnGeometryM = 0f, endDistanceOnGeometryM = lengthM,
                radiusM = radiusM, startHeadingDeg = headingDeg });

            Assert.That(edge.TryEvaluateOffsetAtGeometryDistance(lengthM, out float offset, out float derivative), Is.False);
            Assert.That(offset, Is.Zero);
            Assert.That(derivative, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LinearAndArcCompileIntoUsableDistanceMaps(bool reverse)
        {
            var geometry = new TrackGeometryDefinition { trackGeometryId = "G", lengthM = 20f };
            geometry.horizontalSegments.Add(new TrackGeometryStraightSegment { lengthM = 20f });
            var edge = new TrackEdgeDefinition
            { edgeId = "E", geometryId = "G", startDistanceOnGeometryM = reverse ? 20f : 0f,
                endDistanceOnGeometryM = reverse ? 0f : 20f };
            edge.offsetSegments.Add(new TrackEdgeLinearOffsetSegment
            { startDistanceOnGeometryM = 0f, endDistanceOnGeometryM = 10f,
                startOffsetM = 0f, endOffsetM = 1f });
            edge.offsetSegments.Add(new TrackEdgeArcOffsetSegment
            { startDistanceOnGeometryM = 10f, endDistanceOnGeometryM = 20f,
                startOffsetM = 1f, radiusM = 100f,
                startHeadingDeg = (float)(Math.Atan(0.1) * 180.0 / Math.PI) });

            Assert.That(TrackEdgeCompiler.TryBuildDistanceMap(edge, geometry, 0.1f, out var map, out var error), Is.True, error);
            Assert.That(map, Has.Count.GreaterThan(2));
            Assert.That(map.Exists(s => s.distanceOnGeometryM == 10f), Is.True);
            Assert.That(map[map.Count - 1].distanceOnEdgeM, Is.GreaterThan(20f));
            edge.distanceMap = map;
            Assert.That(edge.TryConvertToGeometryDistance(map[map.Count - 1].distanceOnEdgeM, out float endpoint), Is.True);
            Assert.That(endpoint, Is.EqualTo(edge.endDistanceOnGeometryM));
        }

        [Test]
        public void AssetRoundTripPreservesBothConcreteOffsetTypes()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/VariableOffsetSerializationTest.asset");
            var asset = ScriptableObject.CreateInstance<TrackEdgeAsset>();
            try
            {
                asset.Definition.offsetSegments = new List<TrackEdgeOffsetSegment>
                {
                    new TrackEdgeLinearOffsetSegment
                    { startDistanceOnGeometryM = 0f, endDistanceOnGeometryM = 10f,
                        startOffsetM = -2f, endOffsetM = 1f },
                    new TrackEdgeArcOffsetSegment
                    { startDistanceOnGeometryM = 10f, endDistanceOnGeometryM = 20f,
                        startOffsetM = 1f, radiusM = 200f, startHeadingDeg = 0f }
                };
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssetIfDirty(asset);
                Resources.UnloadAsset(asset);
                asset = null;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var loaded = AssetDatabase.LoadAssetAtPath<TrackEdgeAsset>(path);
                Assert.That(loaded.Definition.offsetSegments[0], Is.TypeOf<TrackEdgeLinearOffsetSegment>());
                Assert.That(loaded.Definition.offsetSegments[1], Is.TypeOf<TrackEdgeArcOffsetSegment>());
                Assert.That(loaded.Definition.offsetSegments[0].EvaluateDerivative(5f), Is.EqualTo(0.3f).Within(0.000001f));
                Assert.That(loaded.Definition.offsetSegments[1].EvaluateOffsetM(20f), Is.GreaterThan(1f));
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
                if (asset != null && !EditorUtility.IsPersistent(asset)) UnityEngine.Object.DestroyImmediate(asset);
            }
        }
    }
}
