using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Connection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Nakatetsu.Track.Interlocking.Tests
{
    public sealed class TrackInterlockingAssetTests
    {
        private TrackInterlockingAsset asset;
        private string assetPath;

        [SetUp]
        public void SetUp()
        {
            asset = ScriptableObject.CreateInstance<TrackInterlockingAsset>();
            assetPath = AssetDatabase.GenerateUniqueAssetPath("Assets/TrackInterlockingSerializationTest.asset");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(assetPath);
            if (asset != null && !EditorUtility.IsPersistent(asset))
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void DefinitionPreservesSeparateReleaseCircuitsAndProtectionTurnoutsWhenSaved()
        {
            var definition = asset.Definition;
            definition.interlockingId = "Station";
            definition.memberTrackCircuitIds.AddRange(new[] { "Entry", "Platform", "Approach", "TurnoutLock" });
            definition.memberConnectionIds.AddRange(new[] { "Main", "Protection" });
            definition.routes.Add(new TrackInterlockingRouteDefinition
            {
                routeId = "Arrival",
                startTrackCircuitId = "Entry",
                destinationTrackCircuitId = "Platform",
                requiredTurnouts = new List<TurnoutRequirement>
                {
                    new() { connectionId = "Main", requiredPosition = TrackSwitchPosition.Normal }
                },
                routeClearTrackCircuitIds = new List<string> { "Entry", "Platform" },
                routeReleaseTrackCircuitIds = new List<string> { "Entry" },
                conflictRouteIds = new List<string> { "Departure" },
                approachLock = new ApproachLockDefinition
                {
                    trackCircuitIds = new List<string> { "Approach" },
                    releaseSeconds = 5f
                },
                overrunProtection = new TrackInterlockingOverrunProtectionDefinition
                {
                    isEnabled = true,
                    turnoutRequirements = new List<TurnoutRequirement>
                    {
                        new() { connectionId = "Protection", requiredPosition = TrackSwitchPosition.Reverse }
                    },
                    releaseSeconds = 10f
                }
            });
            definition.routes.Add(new TrackInterlockingRouteDefinition { routeId = "Departure" });
            definition.turnoutLocks.Add(new TrackInterlockingTurnoutLockDefinition
            {
                connectionId = "Protection",
                trackCircuitIds = new List<string> { "TurnoutLock" }
            });

            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssetIfDirty(asset);
            Resources.UnloadAsset(asset);
            asset = null;
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            asset = AssetDatabase.LoadAssetAtPath<TrackInterlockingAsset>(assetPath);

            definition = asset.Definition;
            var route = definition.routes[0];
            Assert.That(definition.interlockingId, Is.EqualTo("Station"));
            Assert.That(definition.memberTrackCircuitIds, Is.EqualTo(new[] { "Entry", "Platform", "Approach", "TurnoutLock" }));
            Assert.That(definition.memberConnectionIds, Is.EqualTo(new[] { "Main", "Protection" }));
            Assert.That(route.routeId, Is.EqualTo("Arrival"));
            Assert.That(route.startTrackCircuitId, Is.EqualTo("Entry"));
            Assert.That(route.destinationTrackCircuitId, Is.EqualTo("Platform"));
            Assert.That(route.requiredTurnouts[0].connectionId, Is.EqualTo("Main"));
            Assert.That(route.requiredTurnouts[0].requiredPosition, Is.EqualTo(TrackSwitchPosition.Normal));
            Assert.That(route.routeClearTrackCircuitIds, Is.EqualTo(new[] { "Entry", "Platform" }));
            Assert.That(route.routeReleaseTrackCircuitIds, Is.EqualTo(new[] { "Entry" }));
            Assert.That(route.conflictRouteIds, Is.EqualTo(new[] { "Departure" }));
            Assert.That(route.approachLock.trackCircuitIds, Is.EqualTo(new[] { "Approach" }));
            Assert.That(route.approachLock.releaseSeconds, Is.EqualTo(5f));
            Assert.That(route.overrunProtection.isEnabled, Is.True);
            Assert.That(route.overrunProtection.turnoutRequirements[0].connectionId, Is.EqualTo("Protection"));
            Assert.That(route.overrunProtection.turnoutRequirements[0].requiredPosition, Is.EqualTo(TrackSwitchPosition.Reverse));
            Assert.That(route.overrunProtection.releaseSeconds, Is.EqualTo(10f));
            Assert.That(definition.routes[1].overrunProtection.isEnabled, Is.False);
            Assert.That(definition.turnoutLocks[0].connectionId, Is.EqualTo("Protection"));
            Assert.That(definition.turnoutLocks[0].trackCircuitIds, Is.EqualTo(new[] { "TurnoutLock" }));
        }
    }
}
