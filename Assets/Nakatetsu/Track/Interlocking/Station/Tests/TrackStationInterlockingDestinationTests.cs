using NUnit.Framework;

namespace Nakatetsu.Track.Interlocking.Tests
{
    public sealed class TrackStationInterlockingDestinationTests : TrackStationInterlockingTestFixture
    {
        [Test]
        public void SingleRouteWithoutSuffixKeepsExplicitDestination()
        {
            ConfigureDestinations();
            route.routeId = "1R";
            Initialize();
            Assert.That(Request("1R").Accepted, Is.True);
            Assert.That(route.destinationCode, Is.EqualTo("A"));
        }

        [Test]
        public void DestinationCodeCannotReferToAnotherCircuit()
        {
            ConfigureDestinations();
            route.destinationCode = "B";
            Assert.That(TrackStationInterlockingLogic.TryInitialize(context, definition, out string error), Is.False);
            Assert.That(error, Does.Contain("着点符号"));
        }

        [Test]
        public void DestinationRegistryRejectsDuplicateCodes()
        {
            ConfigureDestinations();
            definition.destinations[1].code = "A";
            Assert.That(TrackStationInterlockingLogic.TryInitialize(context, definition, out _), Is.False);
        }

        private void ConfigureDestinations()
        {
            string[] codes = { "A", "B", "C" };
            for (int i = 0; i < definition.routes.Count; i++)
            {
                var item = definition.routes[i];
                item.destinationCode = codes[i];
                definition.destinations.Add(new TrackStationInterlockingDestinationDefinition
                {
                    code = codes[i],
                    displayName = item.destinationTrackCircuitId,
                    trackCircuitId = item.destinationTrackCircuitId
                });
            }
        }
    }
}
