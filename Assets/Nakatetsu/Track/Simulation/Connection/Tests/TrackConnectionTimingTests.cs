using Nakatetsu.Track.Graph;
using Nakatetsu.Track.Graph.Connection;
using Nakatetsu.Track.Graph.Edge;
using Nakatetsu.Track.Graph.Node;
using NUnit.Framework;

namespace Nakatetsu.Track.Simulation.Connection.Tests
{
    public sealed class TrackConnectionTimingTests
    {
        private const string First = "NT01_21A";
        private const string Second = "NT01_21B";
        private TrackGraphContext graph;
        private TrackConnectionContext context;

        [SetUp]
        public void SetUp()
        {
            var definition = new TrackGraphDefinition();
            AddSwitch(definition, First);
            AddSwitch(definition, Second);
            graph = new TrackGraphContext();
            Assert.That(graph.TryBuildLookups(definition, out var error), Is.True, error);
            context = new TrackConnectionContext();
            Assert.That(TrackConnectionLogic.TryInitialize(context, graph, TrackSwitchPosition.Normal,
                out error), Is.True, error);
        }

        [Test]
        public void TimedSwitchHasNoUsableRouteUntilDurationElapses()
        {
            Request(First, TrackSwitchPosition.Reverse, 3f);
            AssertMoving(First, TrackSwitchPosition.Reverse);
            Assert.That(TrackConnectionLogic.TryResolveNextEdge(context, First, First + "-approach",
                out _, out _), Is.False);

            TrackConnectionLogic.Calculate(context, 2.5f);
            AssertMoving(First, TrackSwitchPosition.Reverse);
            TrackConnectionLogic.Calculate(context, 0.5f);
            AssertConfirmed(First, TrackSwitchPosition.Reverse);
            Assert.That(TrackConnectionLogic.TryResolveNextEdge(context, First, First + "-approach",
                out var next, out var error), Is.True, error);
            Assert.That(next, Is.EqualTo(First + "-reverse"));
        }

        [Test]
        public void LargeStepCompletesAtRequestedPosition()
        {
            Request(First, TrackSwitchPosition.Reverse, 3f);
            TrackConnectionLogic.Calculate(context, 30f);
            AssertConfirmed(First, TrackSwitchPosition.Reverse);
        }

        [Test]
        public void SameTargetRequestsDoNotRestartOrShortenRunningTimer()
        {
            Request(First, TrackSwitchPosition.Reverse, 3f);
            TrackConnectionLogic.Calculate(context, 2f);
            Request(First, TrackSwitchPosition.Reverse, 10f);
            Request(First, TrackSwitchPosition.Reverse, null);
            Request(First, TrackSwitchPosition.Reverse, 0f);
            AssertMoving(First, TrackSwitchPosition.Reverse);
            TrackConnectionLogic.Calculate(context, 1f);
            AssertConfirmed(First, TrackSwitchPosition.Reverse);
        }

        [Test]
        public void OppositeRequestIsRejectedWithoutLosingPendingSwitch()
        {
            Request(First, TrackSwitchPosition.Reverse, 3f);
            TrackConnectionLogic.Calculate(context, 2f);
            Assert.That(TrackConnectionLogic.TryRequestPosition(context, First,
                TrackSwitchPosition.Normal, 0f, out var error), Is.False);
            Assert.That(error, Is.Not.Empty);
            AssertMoving(First, TrackSwitchPosition.Reverse);
            TrackConnectionLogic.Calculate(context, 1f);
            AssertConfirmed(First, TrackSwitchPosition.Reverse);
        }

        [Test]
        public void DifferentSwitchesCompleteIndependently()
        {
            Request(First, TrackSwitchPosition.Reverse, 1f);
            Request(Second, TrackSwitchPosition.Reverse, 3f);
            TrackConnectionLogic.Calculate(context, 1f);
            AssertConfirmed(First, TrackSwitchPosition.Reverse);
            AssertMoving(Second, TrackSwitchPosition.Reverse);
            TrackConnectionLogic.Calculate(context, 2f);
            AssertConfirmed(Second, TrackSwitchPosition.Reverse);
        }

        [Test]
        public void ManualConfirmationClearsTimerBeforeNextRequest()
        {
            Request(First, TrackSwitchPosition.Reverse, 3f);
            TrackConnectionLogic.Calculate(context, 1f);
            Assert.That(TrackConnectionLogic.TryConfirmPosition(context, First,
                TrackSwitchPosition.Reverse, out var error), Is.True, error);
            Request(First, TrackSwitchPosition.Normal, null);
            TrackConnectionLogic.Calculate(context, 100f);
            AssertMoving(First, TrackSwitchPosition.Normal);
        }

        [Test]
        public void ReinitializeDiscardsPendingTimers()
        {
            Request(First, TrackSwitchPosition.Reverse, 3f);
            TrackConnectionLogic.Calculate(context, 1f);
            Assert.That(TrackConnectionLogic.TryInitialize(context, graph, TrackSwitchPosition.Normal,
                out var error), Is.True, error);
            Request(First, TrackSwitchPosition.Reverse, null);
            TrackConnectionLogic.Calculate(context, 100f);
            AssertMoving(First, TrackSwitchPosition.Reverse);
        }

        [Test]
        public void NullDurationRequiresExplicitConfirmation()
        {
            Request(First, TrackSwitchPosition.Reverse, null);
            TrackConnectionLogic.Calculate(context, 100f);
            AssertMoving(First, TrackSwitchPosition.Reverse);
            Assert.That(TrackConnectionLogic.TryConfirmPosition(context, First,
                TrackSwitchPosition.Reverse, out var error), Is.True, error);
            AssertConfirmed(First, TrackSwitchPosition.Reverse);
        }

        [Test]
        public void LegacyRequestRemainsManual()
        {
            Assert.That(TrackConnectionLogic.TryRequestPosition(context, First,
                TrackSwitchPosition.Reverse, out var error), Is.True, error);
            TrackConnectionLogic.Calculate(context, 100f);
            AssertMoving(First, TrackSwitchPosition.Reverse);
        }

        [Test]
        public void ZeroDurationImmediatelyConfirmsNewRequest()
        {
            Request(First, TrackSwitchPosition.Reverse, 0f);
            AssertConfirmed(First, TrackSwitchPosition.Reverse);
        }

        [Test]
        public void RequestForAlreadyConfirmedPositionDoesNotStartMoving()
        {
            Request(First, TrackSwitchPosition.Normal, 3f);
            AssertConfirmed(First, TrackSwitchPosition.Normal);
            TrackConnectionLogic.Calculate(context, 3f);
            AssertConfirmed(First, TrackSwitchPosition.Normal);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidDurationDoesNotChangePosition(float duration)
        {
            Assert.That(TrackConnectionLogic.TryRequestPosition(context, First,
                TrackSwitchPosition.Reverse, duration, out var error), Is.False);
            Assert.That(error, Is.Not.Empty);
            AssertConfirmed(First, TrackSwitchPosition.Normal);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidDeltaDoesNotAdvanceTimer(float delta)
        {
            Request(First, TrackSwitchPosition.Reverse, 3f);
            TrackConnectionLogic.Calculate(context, delta);
            AssertMoving(First, TrackSwitchPosition.Reverse);
            TrackConnectionLogic.Calculate(context, 2f);
            AssertMoving(First, TrackSwitchPosition.Reverse);
            TrackConnectionLogic.Calculate(context, 1f);
            AssertConfirmed(First, TrackSwitchPosition.Reverse);
        }

        private void Request(string id, TrackSwitchPosition position, float? duration)
        {
            Assert.That(TrackConnectionLogic.TryRequestPosition(context, id, position, duration,
                out var error), Is.True, error);
        }

        private void AssertMoving(string id, TrackSwitchPosition requested)
        {
            Assert.That(context.TryGetState(id, out var state), Is.True);
            Assert.That(state.RequestedPosition, Is.EqualTo(requested));
            Assert.That(state.ActualPosition, Is.EqualTo(TrackSwitchPosition.Unknown));
            Assert.That(state.IsMoving, Is.True);
        }

        private void AssertConfirmed(string id, TrackSwitchPosition expected)
        {
            Assert.That(context.TryGetState(id, out var state), Is.True);
            Assert.That(state.RequestedPosition, Is.EqualTo(expected));
            Assert.That(state.ActualPosition, Is.EqualTo(expected));
            Assert.That(state.IsMoving, Is.False);
        }

        private static void AddSwitch(TrackGraphDefinition definition, string id)
        {
            string approach = id + "-approach";
            string normal = id + "-normal";
            string reverse = id + "-reverse";
            definition.nodes.Add(new TrackNodeDefinition
            {
                nodeId = id,
                connectedEdgeIds = new() { approach, normal, reverse }
            });
            foreach (string edgeId in new[] { approach, normal, reverse })
            {
                definition.edges.Add(new TrackEdgeDefinition { edgeId = edgeId, nodeAId = id });
            }
            definition.connections.Add(new TrackConnectionDefinition
            {
                connectionId = id,
                nodeId = id,
                edgePairs = new()
                {
                    new TrackEdgePair
                    {
                        pairId = id + "-N", edgeAId = approach, edgeBId = normal,
                        condition = TrackConnectionCondition.Normal
                    },
                    new TrackEdgePair
                    {
                        pairId = id + "-R", edgeAId = approach, edgeBId = reverse,
                        condition = TrackConnectionCondition.Reverse
                    }
                }
            });
        }
    }
}
