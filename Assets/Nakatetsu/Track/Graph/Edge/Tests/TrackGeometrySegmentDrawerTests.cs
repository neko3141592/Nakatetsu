using System;
using Nakatetsu.Track.Graph.Editor;
using Nakatetsu.Track.Graph.Geometry;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nakatetsu.Track.Graph.Tests
{
    public sealed class TrackGeometrySegmentDrawerTests
    {
        [TestCase(typeof(TrackGeometryCircularSegment))]
        [TestCase(typeof(TrackGeometryTransitionInSegment))]
        [TestCase(typeof(TrackGeometryTransitionOutSegment))]
        public void HorizontalTypeAndEditedRadiusSurviveSaving(Type segmentType)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/SegmentDrawerTest.asset");
            var asset = ScriptableObject.CreateInstance<TrackGraphAsset>();
            try
            {
                var geometry = new TrackGeometryDefinition();
                geometry.horizontalSegments.Add(new TrackGeometryStraightSegment { startDistanceM = 80f, lengthM = 120f });
                geometry.horizontalSegments.Add(new TrackGeometryStraightSegment { startDistanceM = 200f, lengthM = 50f });
                asset.Definition.geometries.Add(geometry);
                AssetDatabase.CreateAsset(asset, path);

                using (var serialized = new SerializedObject(asset))
                {
                    SerializedProperty segment = FindSegment(serialized, "horizontalSegmentDefinitions");
                    TrackGeometrySegmentDrawer.SetSegmentType(segment, segmentType);
                    serialized.ApplyModifiedProperties();
                    serialized.Update();
                    FindSegment(serialized, "horizontalSegmentDefinitions").FindPropertyRelative("radiusM").floatValue = -450f;
                    serialized.ApplyModifiedProperties();
                }
                AssetDatabase.SaveAssetIfDirty(asset);
                Resources.UnloadAsset(asset);
                asset = null;

                var restored = AssetDatabase.LoadAssetAtPath<TrackGraphAsset>(path).Definition.geometries[0];
                Assert.That(restored.horizontalSegments[0].GetType(), Is.EqualTo(segmentType));
                Assert.That(restored.horizontalSegments[0].startDistanceM, Is.EqualTo(80f));
                Assert.That(restored.horizontalSegments[0].lengthM, Is.EqualTo(120f));
                Assert.That(segmentType.GetField("radiusM").GetValue(restored.horizontalSegments[0]), Is.EqualTo(-450f));
                Assert.That(restored.horizontalSegments[1], Is.TypeOf<TrackGeometryStraightSegment>());
                Assert.That(restored.horizontalSegments[1].lengthM, Is.EqualTo(50f));
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
                if (asset != null && !EditorUtility.IsPersistent(asset)) Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void VerticalTypeChangePreservesGradientAndSupportsUndo()
        {
            var asset = ScriptableObject.CreateInstance<TrackGraphAsset>();
            try
            {
                var geometry = new TrackGeometryDefinition();
                geometry.verticalSegments.Add(new TrackGeometryConstantGradientSegment
                    { startDistanceM = 80f, lengthM = 120f, gradientPermille = -35f });
                asset.Definition.geometries.Add(geometry);
                using (var serialized = new SerializedObject(asset))
                {
                    Undo.IncrementCurrentGroup();
                    TrackGeometrySegmentDrawer.SetSegmentType(FindSegment(serialized, "verticalSegmentDefinitions"),
                        typeof(TrackGeometryLinearGradientSegment));
                    serialized.ApplyModifiedProperties();
                    Undo.FlushUndoRecordObjects();

                    var linear = (TrackGeometryLinearGradientSegment)geometry.verticalSegments[0];
                    Assert.That(linear.startGradientPermille, Is.EqualTo(-35f));
                    Assert.That(linear.endGradientPermille, Is.EqualTo(-35f));
                    Assert.That(linear.startDistanceM, Is.EqualTo(80f));
                    Assert.That(linear.lengthM, Is.EqualTo(120f));
                    serialized.Update();
                    FindSegment(serialized, "verticalSegmentDefinitions").FindPropertyRelative("endGradientPermille").floatValue = 20f;
                    serialized.ApplyModifiedProperties();
                    Undo.FlushUndoRecordObjects();
                    Undo.PerformUndo();

                    Assert.That(asset.Definition.geometries[0].verticalSegments[0], Is.TypeOf<TrackGeometryConstantGradientSegment>());
                    Assert.That(((TrackGeometryConstantGradientSegment)asset.Definition.geometries[0].verticalSegments[0]).gradientPermille,
                        Is.EqualTo(-35f));
                }
            }
            finally
            {
                Undo.ClearUndo(asset);
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void EmptyElementCanBeAssignedAndEdited()
        {
            var asset = ScriptableObject.CreateInstance<TrackGraphAsset>();
            try
            {
                var geometry = new TrackGeometryDefinition();
                geometry.verticalSegments.Add(null);
                asset.Definition.geometries.Add(geometry);
                using (var serialized = new SerializedObject(asset))
                {
                    TrackGeometrySegmentDrawer.SetSegmentType(FindSegment(serialized, "verticalSegmentDefinitions"),
                        typeof(TrackGeometryConstantGradientSegment));
                    serialized.ApplyModifiedProperties();
                    serialized.Update();
                    FindSegment(serialized, "verticalSegmentDefinitions").FindPropertyRelative("gradientPermille").floatValue = 25f;
                    serialized.ApplyModifiedProperties();
                }
                Assert.That(((TrackGeometryConstantGradientSegment)geometry.verticalSegments[0]).gradientPermille, Is.EqualTo(25f));
            }
            finally { Object.DestroyImmediate(asset); }
        }

        private static SerializedProperty FindSegment(SerializedObject serialized, string fieldName) =>
            serialized.FindProperty("definition").FindPropertyRelative("geometries").GetArrayElementAtIndex(0)
                .FindPropertyRelative(fieldName).GetArrayElementAtIndex(0);
    }
}
