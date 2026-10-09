using System.Collections.Generic;
using System.Linq;
using Nakatetsu.Track.Atc;
using Nakatetsu.Track.Graph;
using Nakatetsu.Track.Graph.Circuit;
using Nakatetsu.Track.Graph.Connection;
using Nakatetsu.Track.Graph.Edge;
using Nakatetsu.Track.Graph.Geometry;
using Nakatetsu.Track.Graph.Node;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Track.Simulation.Connection;
using NUnit.Framework;

namespace Nakatetsu.Track.Interlocking.Tests
{
    public sealed class TrackAtcGraphCompilerTests
    {
        [Test]
        public void CircuitRoutesResolveTwoCompositeEdgesWithoutChangingPhysicalGraph()
        {
            Create(out var track, out var source, out var interlocking);
            var compiled = Compile(track, source, interlocking);
            var turnoutEdges = compiled.atcEdge.Where(edge => edge.trackCircuitId == "21T").ToArray();

            Assert.That(track.edges.Count, Is.EqualTo(3));
            Assert.That(track.nodes.Count, Is.EqualTo(4));
            Assert.That(turnoutEdges.Length, Is.EqualTo(2));
            Assert.That(turnoutEdges.Select(edge => edge.atcEdgeId).Distinct().Count(), Is.EqualTo(2));
            foreach (var edge in turnoutEdges)
            {
                Assert.That(edge.physicalSpans.Count, Is.EqualTo(2));
                Assert.That(edge.physicalSpans[0].trackEdgeId, Is.EqualTo("common"));
                Assert.That(edge.physicalSpans[0].startDistanceOnEdgeM, Is.EqualTo(80f));
                Assert.That(edge.lengthM, Is.EqualTo(50f));
            }

            var normal = compiled.routes.Single(route => route.atcRouteId == "normal");
            var reverse = compiled.routes.Single(route => route.atcRouteId == "reverse");
            Assert.That(normal.atcEdgeIds.Count, Is.EqualTo(2));
            Assert.That(reverse.atcEdgeIds.Count, Is.EqualTo(2));
            Assert.That(normal.atcEdgeIds[0], Is.Not.EqualTo(reverse.atcEdgeIds[0]));
            Assert.That(compiled.atcEdge.Single(edge => edge.atcEdgeId == normal.atcEdgeIds[0])
                .physicalSpans[1].trackEdgeId, Is.EqualTo("normal"));
            Assert.That(compiled.atcEdge.Single(edge => edge.atcEdgeId == reverse.atcEdgeIds[0])
                .physicalSpans[1].trackEdgeId, Is.EqualTo("reverse"));
        }

        [Test]
        public void ReversedPhysicalSpanKeepsCompositeDistanceGradientAndSpeedLimit()
        {
            Create(out var track, out var source, out var interlocking);
            var compiled = Compile(track, source, interlocking);
            var route = compiled.routes.Single(item => item.atcRouteId == "reverse");
            var edge = compiled.atcEdge.Single(item => item.atcEdgeId == route.atcEdgeIds[0]);

            Assert.That(edge.physicalSpans[1].startDistanceOnEdgeM, Is.EqualTo(100f));
            Assert.That(edge.physicalSpans[1].endDistanceOnEdgeM, Is.EqualTo(70f));
            Assert.That(edge.gradientProfiles.All(item => item.gradientPermille > 19.9f), Is.True);
            var restriction = edge.speedLimitSections.Single(item => item.speedLimitKmh == 25f);
            Assert.That(restriction.startDistanceOnAtcEdgeM, Is.EqualTo(30f).Within(0.001f));
            Assert.That(restriction.endDistanceOnAtcEdgeM, Is.EqualTo(40f).Within(0.001f));
        }

        [Test]
        public void OppositeCircuitOrderProducesOppositeEntryDirection()
        {
            Create(out var track, out var source, out var interlocking);
            source.routes[0].trackCircuitIds.Reverse();
            var compiled = Compile(track, source, interlocking);
            var route = compiled.routes.Single(item => item.atcRouteId == "normal");
            Assert.That(route.entryDirection, Is.EqualTo(TrackAtcTravelDirection.BtoA));
            Assert.That(compiled.atcEdge.Single(edge => edge.atcEdgeId == route.atcEdgeIds[0])
                .trackCircuitId, Is.EqualTo("2RT"));
        }

        [Test]
        public void SingleCircuitRouteUsesExplicitEntryDirection()
        {
            Create(out var track, out var source, out var interlocking);
            source.routes[0].trackCircuitIds.RemoveAt(1);
            source.routes[0].entryDirection = TrackAtcTravelDirection.BtoA;
            var compiled = Compile(track, source, interlocking);
            var route = compiled.routes.Single(item => item.atcRouteId == "normal");
            Assert.That(route.atcEdgeIds.Count, Is.EqualTo(1));
            Assert.That(route.entryDirection, Is.EqualTo(TrackAtcTravelDirection.BtoA));
        }

        [Test]
        public void TurnoutRequirementConflictingWithCircuitPathRejectsCompilation()
        {
            Create(out var track, out var source, out var interlocking);
            interlocking.routes[0].requiredTurnouts[0].requiredPosition = TrackSwitchPosition.Reverse;
            var errors = new List<string>();
            Assert.That(TrackAtcGraphCompiler.TryCompile(track, source, new[] { interlocking },
                out var compiled, errors), Is.False);
            Assert.That(compiled, Is.Null);
            Assert.That(errors, Is.Not.Empty);
        }

        [Test]
        public void AdjacentSectionsOnSamePhysicalEdgeHaveOneMappingSpan()
        {
            Create(out var track, out var source, out var interlocking);
            var sections = track.circuits.Single(circuit => circuit.circuitId == "21T").sections;
            sections[0].endDistanceOnGeometryM = 90f;
            sections.Add(Section("common", 90f, 100f));
            var compiled = Compile(track, source, interlocking);
            foreach (var edge in compiled.atcEdge.Where(edge => edge.trackCircuitId == "21T"))
            {
                Assert.That(edge.physicalSpans.Count, Is.EqualTo(2));
                Assert.That(edge.physicalSpans[0].startDistanceOnEdgeM, Is.EqualTo(80f));
                Assert.That(edge.physicalSpans[0].endDistanceOnEdgeM, Is.EqualTo(100f));
                Assert.That(edge.lengthM, Is.EqualTo(50f));
            }
        }

        [Test]
        public void RouteMayStartInBlockCircuitAndKeepsItsTravelDirection()
        {
            Create(out var track, out var source, out var interlocking);
            source.routes[0].trackCircuitIds.Insert(0, "20T");
            var compiled = Compile(track, source, interlocking);
            var block = compiled.atcEdge.Single(edge => edge.trackCircuitId == "20T");
            Assert.That(block.direction, Is.EqualTo(TrackAtcTravelDirection.AtoB));
            Assert.That(compiled.routes.Single(route => route.atcRouteId == "normal").atcEdgeIds[0],
                Is.EqualTo(block.atcEdgeId));
        }

        [Test]
        public void InputOrderingDoesNotChangeGeneratedIdsOrRoutePaths()
        {
            Create(out var track, out var source, out var interlocking);
            var first = Compile(track, source, interlocking);
            track.edges.Reverse();
            track.nodes.Reverse();
            track.circuits.Reverse();
            source.circuits.Reverse();
            source.routes.Reverse();
            var second = Compile(track, source, interlocking);
            Assert.That(second.atcEdge.Select(edge => edge.atcEdgeId),
                Is.EqualTo(first.atcEdge.Select(edge => edge.atcEdgeId)));
            Assert.That(second.routes.Select(route => string.Join(",", route.atcEdgeIds)),
                Is.EqualTo(first.routes.Select(route => string.Join(",", route.atcEdgeIds))));
        }

        private static TrackAtcGraphDefinition Compile(TrackGraphDefinition track,
            TrackAtcGraphCompileDefinition source, TrackStationInterlockingDefinition interlocking)
        {
            var errors = new List<string>();
            Assert.That(TrackAtcGraphCompiler.TryCompile(track, source, new[] { interlocking },
                out var compiled, errors), Is.True, string.Join("; ", errors));
            return compiled;
        }

        private static void Create(out TrackGraphDefinition track,
            out TrackAtcGraphCompileDefinition source, out TrackStationInterlockingDefinition interlocking)
        {
            track = new TrackGraphDefinition { graphId = "test" };
            var geometry = new TrackGeometryDefinition { trackGeometryId = "line", lengthM = 200f };
            geometry.horizontalSegments.Add(new TrackGeometryStraightSegment { lengthM = 200f });
            geometry.verticalSegments.Add(new TrackGeometryConstantGradientSegment
            {
                lengthM = 200f,
                gradientPermille = 20f
            });
            track.geometries.Add(geometry);
            track.nodes.Add(new TrackNodeDefinition { nodeId = "start", connectedEdgeIds = new() { "common" } });
            track.nodes.Add(new TrackNodeDefinition { nodeId = "switch", connectedEdgeIds = new() { "common", "normal", "reverse" } });
            track.nodes.Add(new TrackNodeDefinition { nodeId = "normal-end", connectedEdgeIds = new() { "normal" } });
            track.nodes.Add(new TrackNodeDefinition { nodeId = "reverse-end", connectedEdgeIds = new() { "reverse" } });
            track.edges.Add(Edge("common", "start", "switch", 0f, 100f));
            track.edges.Add(Edge("normal", "switch", "normal-end", 100f, 200f));
            track.edges.Add(Edge("reverse", "reverse-end", "switch", 200f, 100f));
            track.connections.Add(new TrackConnectionDefinition
            {
                connectionId = "21", nodeId = "switch",
                edgePairs = new()
                {
                    new() { pairId = "normal", edgeAId = "common", edgeBId = "normal", condition = TrackConnectionCondition.Normal },
                    new() { pairId = "reverse", edgeAId = "common", edgeBId = "reverse", condition = TrackConnectionCondition.Reverse }
                }
            });
            track.circuits.Add(new TrackCircuitDefinition { circuitId = "20T", sections = new() { Section("common", 0f, 80f) } });
            track.circuits.Add(new TrackCircuitDefinition
            {
                circuitId = "21T",
                sections = new() { Section("common", 80f, 100f), Section("normal", 100f, 130f), Section("reverse", 100f, 130f) }
            });
            track.circuits.Add(new TrackCircuitDefinition { circuitId = "2RT", sections = new() { Section("normal", 130f, 200f) } });
            track.circuits.Add(new TrackCircuitDefinition { circuitId = "1RT", sections = new() { Section("reverse", 130f, 200f) } });

            source = new TrackAtcGraphCompileDefinition { atcGraphId = "test-atc", maximumOperatingSpeedKmh = 100f };
            foreach (var circuit in track.circuits)
            {
                source.circuits.Add(new TrackAtcCircuitSetting
                {
                    trackCircuitId = circuit.circuitId,
                    controlKind = circuit.circuitId == "20T" ? TrackAtcEdgeControlKind.Block : TrackAtcEdgeControlKind.Interlocking,
                    speedLimitKmh = 100f
                });
            }
            source.speedLimits.Add(new TrackAtcSpeedLimitSourceSection
            {
                trackEdgeId = "reverse", startDistanceOnGeometryM = 110f, endDistanceOnGeometryM = 120f, speedLimitKmh = 25f
            });
            source.routes.Add(new TrackAtcRouteSourceDefinition { atcRouteId = "normal", interlockingRouteId = "normal", trackCircuitIds = new() { "21T", "2RT" } });
            source.routes.Add(new TrackAtcRouteSourceDefinition { atcRouteId = "reverse", interlockingRouteId = "reverse", trackCircuitIds = new() { "21T", "1RT" } });
            interlocking = new TrackStationInterlockingDefinition { interlockingId = "test" };
            interlocking.routes.Add(new TrackStationInterlockingRouteDefinition
            {
                routeId = "normal", requiredTurnouts = new() { new() { connectionId = "21", requiredPosition = TrackSwitchPosition.Normal } }
            });
            interlocking.routes.Add(new TrackStationInterlockingRouteDefinition
            {
                routeId = "reverse", requiredTurnouts = new() { new() { connectionId = "21", requiredPosition = TrackSwitchPosition.Reverse } }
            });
        }

        private static TrackEdgeDefinition Edge(string id, string nodeA, string nodeB, float start, float end)
        {
            var edge = new TrackEdgeDefinition
            {
                edgeId = id, geometryId = "line", nodeAId = nodeA, nodeBId = nodeB,
                startDistanceOnGeometryM = start, endDistanceOnGeometryM = end,
                travelDirection = end > start ? TrackEdgeTravelDirection.AtoB : TrackEdgeTravelDirection.BtoA,
                distanceMap = new()
                {
                    new() { distanceOnGeometryM = start, distanceOnEdgeM = 0f },
                    new() { distanceOnGeometryM = end, distanceOnEdgeM = 100f }
                }
            };
            edge.offsetSegments.Add(new TrackEdgeConstantOffsetSegment
            {
                startDistanceOnGeometryM = 0f, endDistanceOnGeometryM = 200f, offsetM = 0f
            });
            return edge;
        }

        private static TrackCircuitSection Section(string edgeId, float start, float end)
        {
            return new TrackCircuitSection { edgeId = edgeId, startDistanceOnGeometryM = start, endDistanceOnGeometryM = end };
        }
    }
}
