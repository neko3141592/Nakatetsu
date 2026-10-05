using Nakatetsu.Track.Interlocking.Editor;
using Nakatetsu.Track.Simulation.Connection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nakatetsu.Track.Interlocking.Tests
{
    public sealed class TrackInterlockingAssetTests
    {
        private const string ProtectionPath = "definition.routes.Array.data[0].overrunProtection";
        private TrackInterlockingAsset asset;
        private string assetPath;
        private EditorWindow window;

        [SetUp]
        public void SetUp()
        {
            asset = ScriptableObject.CreateInstance<TrackInterlockingAsset>();
            asset.Definition.routes.Add(new InterlockingRoute { routeId = "Protected" });
            asset.Definition.routes.Add(new InterlockingRoute { routeId = "Unprotected" });
            assetPath = AssetDatabase.GenerateUniqueAssetPath("Assets/TrackInterlockingSerializationTest.asset");
        }

        [TearDown]
        public void TearDown()
        {
            if (window != null)
                window.Close();
            AssetDatabase.DeleteAsset(assetPath);
            if (asset != null && !EditorUtility.IsPersistent(asset))
                Object.DestroyImmediate(asset);
        }

        [Test]
        public void InspectorCanCreateProtectionAndSaveAllFieldsWhilePreservingUnprotectedRoute()
        {
            using var serialized = new SerializedObject(asset);
            var property = serialized.FindProperty(ProtectionPath);
            var gui = CreateInspector(property);
            Assert.That(asset.Definition.routes[0].overrunProtection, Is.Null);
            gui.Q<Toggle>().value = true;

            Assert.That(asset.Definition.routes[0].overrunProtection, Is.Not.Null);
            Assert.That(gui.Query<UnityEditor.UIElements.PropertyField>().ToList(), Has.Count.EqualTo(3));
            SetStringList(property.FindPropertyRelative("common.clearTrackCircuitIds"), "Common");
            SetTurnout(property.FindPropertyRelative("common.requiredTurnouts"), "21", 1);
            SetStringList(property.FindPropertyRelative("normalAdditional.clearTrackCircuitIds"), "Additional");
            SetTurnout(property.FindPropertyRelative("normalAdditional.requiredTurnouts"), "22", 2);
            property.FindPropertyRelative("release.mode").enumValueIndex = (int)OverrunReleaseMode.TimedAfterArrival;
            property.FindPropertyRelative("release.triggerTrackCircuitId").stringValue = "Arrival";
            property.FindPropertyRelative("release.releaseSeconds").floatValue = 45f;
            serialized.ApplyModifiedProperties();

            SaveAndReload();

            var protection = asset.Definition.routes[0].overrunProtection;
            Assert.That(protection, Is.Not.Null);
            Assert.That(protection.common.clearTrackCircuitIds, Is.EqualTo(new[] { "Common" }));
            Assert.That(protection.common.requiredTurnouts[0].connectionId, Is.EqualTo("21"));
            Assert.That(protection.common.requiredTurnouts[0].requiredPosition, Is.EqualTo((TrackSwitchPosition)1));
            Assert.That(protection.normalAdditional.clearTrackCircuitIds, Is.EqualTo(new[] { "Additional" }));
            Assert.That(protection.normalAdditional.requiredTurnouts[0].connectionId, Is.EqualTo("22"));
            Assert.That(protection.normalAdditional.requiredTurnouts[0].requiredPosition, Is.EqualTo((TrackSwitchPosition)2));
            Assert.That(protection.release.mode, Is.EqualTo(OverrunReleaseMode.TimedAfterArrival));
            Assert.That(protection.release.triggerTrackCircuitId, Is.EqualTo("Arrival"));
            Assert.That(protection.release.releaseSeconds, Is.EqualTo(45f));
            Assert.That(asset.Definition.routes[1].overrunProtection, Is.Null);
        }

        [Test]
        public void InspectorCanRemoveProtectionAndPersistNull()
        {
            asset.Definition.routes[0].overrunProtection = new OverrunProtectionDefinition();
            using var serialized = new SerializedObject(asset);
            var gui = CreateInspector(serialized.FindProperty(ProtectionPath));
            gui.Q<Toggle>().value = false;
            Assert.That(gui.Query<UnityEditor.UIElements.PropertyField>().ToList(), Is.Empty);

            SaveAndReload();

            Assert.That(asset.Definition.routes[0].overrunProtection, Is.Null);
        }

        [Test]
        public void InspectorCreationSupportsUndoAndRedo()
        {
            using var serialized = new SerializedObject(asset);
            var gui = CreateInspector(serialized.FindProperty(ProtectionPath));
            Undo.IncrementCurrentGroup();
            gui.Q<Toggle>().value = true;
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.That(asset.Definition.routes[0].overrunProtection, Is.Null);
            Undo.PerformRedo();
            Assert.That(asset.Definition.routes[0].overrunProtection, Is.Not.Null);
        }

        private VisualElement CreateInspector(SerializedProperty property)
        {
            var gui = new OverrunProtectionDefinitionDrawer().CreatePropertyGUI(property);
            window = ScriptableObject.CreateInstance<EditorWindow>();
            window.Show();
            window.rootVisualElement.Add(gui);
            return gui;
        }

        private void SaveAndReload()
        {
            window.Close();
            window = null;
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssetIfDirty(asset);
            Resources.UnloadAsset(asset);
            asset = null;
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            asset = AssetDatabase.LoadAssetAtPath<TrackInterlockingAsset>(assetPath);
        }

        private static void SetStringList(SerializedProperty property, string value)
        {
            property.arraySize = 1;
            property.GetArrayElementAtIndex(0).stringValue = value;
        }

        private static void SetTurnout(SerializedProperty property, string connectionId, int position)
        {
            property.arraySize = 1;
            var turnout = property.GetArrayElementAtIndex(0);
            turnout.FindPropertyRelative("connectionId").stringValue = connectionId;
            turnout.FindPropertyRelative("requiredPosition").enumValueIndex = position;
        }
    }
}
