using System;
using System.Collections.Generic;
using Nakatetsu.Track.Interlocking.Management;
using Nakatetsu.Track.Simulation.Circuit;
using NUnit.Framework;

namespace Nakatetsu.Track.Interlocking.Tests
{
    public sealed class TrackInterlockingManagementTests
    {
        [Test]
        public void InitializeKeepsInputAndClearsOutputWithoutPublishingBeforeInitialization()
        {
            var context = new TrackInterlockingManagementContext();
            var station = ReadyStation();
            context.Input.StationsById.Add("Station", station);

            TrackInterlockingManagementLogic.Calculate(context);
            Assert.That(context.State.IsInitialized, Is.False);
            Assert.That(context.Output.StationsById, Is.Empty);

            TrackInterlockingManagementLogic.Initialize(context);
            Assert.That(context.State.IsInitialized, Is.True);
            Assert.That(context.Output.StationsById, Is.Empty);
            Assert.That(context.Input.StationsById["Station"], Is.SameAs(station));

            TrackInterlockingManagementLogic.Calculate(context);
            Assert.That(context.Output.StationsById.ContainsKey("Station"), Is.True);

            TrackInterlockingManagementLogic.Initialize(context);
            Assert.That(context.State.IsInitialized, Is.True);
            Assert.That(context.Output.StationsById, Is.Empty);
            Assert.That(context.Input.StationsById["Station"], Is.SameAs(station));
        }

        [Test]
        public void CalculateClearsPublishedOutputWhenManagerIsNotInitialized()
        {
            var context = new TrackInterlockingManagementContext();
            context.Input.StationsById.Add("Station", ReadyStation());
            TrackInterlockingManagementLogic.Initialize(context);
            TrackInterlockingManagementLogic.Calculate(context);
            Assert.That(context.Output.StationsById.ContainsKey("Station"), Is.True);

            context.State.IsInitialized = false;
            TrackInterlockingManagementLogic.Calculate(context);

            Assert.That(context.Output.StationsById, Is.Empty);
            Assert.That(context.State.IsInitialized, Is.False);
        }

        [Test]
        public void CalculatePublishesReadyStationsIncludingEmptyStationAndSkipsInvalidEntries()
        {
            var context = new TrackInterlockingManagementContext();
            var station = ReadyStation();
            station.RoutesById.Add("Route", new TrackInterlockingManagementRouteInput());
            station.RoutesById.Add("NullRoute", null);
            station.RoutesById.Add(string.Empty, new TrackInterlockingManagementRouteInput());
            station.RoutesById.Add(" \t", new TrackInterlockingManagementRouteInput());
            context.Input.StationsById.Add("Station", station);
            context.Input.StationsById.Add("EmptyStation", ReadyStation());
            context.Input.StationsById.Add("Uninitialized", new TrackInterlockingManagementStationInput
            {
                HasOutput = true
            });
            context.Input.StationsById.Add("MissingOutput", new TrackInterlockingManagementStationInput
            {
                IsInitialized = true
            });
            context.Input.StationsById.Add("Neither", new TrackInterlockingManagementStationInput());
            context.Input.StationsById.Add("NullStation", null);
            context.Input.StationsById.Add(string.Empty, ReadyStation());
            context.Input.StationsById.Add(" \t", ReadyStation());

            TrackInterlockingManagementLogic.Initialize(context);
            TrackInterlockingManagementLogic.Calculate(context);

            Assert.That(context.Output.StationsById.Keys, Is.EquivalentTo(new[] { "Station", "EmptyStation" }));
            Assert.That(context.Output.StationsById["EmptyStation"].RoutesById, Is.Empty);
            var publishedStation = context.Output.StationsById["Station"];
            Assert.That(publishedStation.IsInitialized, Is.True);
            Assert.That(publishedStation.HasOutput, Is.True);
            Assert.That(publishedStation.RoutesById.Keys, Is.EquivalentTo(new[] { "Route" }));
            AssertDefaultRoute(publishedStation.RoutesById["Route"]);
        }

        [TestCase(false, true, false)]
        [TestCase(true, false, false)]
        [TestCase(true, true, true)]
        public void CalculateRemovesPreviousStationAfterFailureOrDeletion(
            bool isInitialized, bool hasOutput, bool removeStation)
        {
            var context = new TrackInterlockingManagementContext();
            var station = ReadyStation();
            station.RoutesById.Add("Route", ActiveRoute());
            context.Input.StationsById.Add("Station", station);
            TrackInterlockingManagementLogic.Initialize(context);
            TrackInterlockingManagementLogic.Calculate(context);
            var previousOutput = context.Output;
            Assert.That(previousOutput.StationsById.ContainsKey("Station"), Is.True);

            station.IsInitialized = isInitialized;
            station.HasOutput = hasOutput;
            if (removeStation)
            {
                context.Input.StationsById.Remove("Station");
            }

            TrackInterlockingManagementLogic.Calculate(context);

            Assert.That(context.Output.StationsById, Is.Empty);
            Assert.That(previousOutput.StationsById.ContainsKey("Station"), Is.True);
            AssertActiveRoute(previousOutput.StationsById["Station"].RoutesById["Route"]);
        }

        [Test]
        public void PublishedSnapshotsKeepValuesAcrossInputChangesAndLaterCalculations()
        {
            var context = new TrackInterlockingManagementContext();
            var station = ReadyStation();
            var route = ActiveRoute();
            station.RoutesById.Add("Route", route);
            context.Input.StationsById.Add("Station", station);
            TrackInterlockingManagementLogic.Initialize(context);
            TrackInterlockingManagementLogic.Calculate(context);
            var firstOutput = context.Output;

            route.IsAvailable = false;
            route.IsRouteSet = false;
            route.PathEstablished = false;
            route.ProceedAllowed = false;
            route.RouteLocked = false;
            route.CancelPending = false;
            route.ApproachLocked = false;
            route.ApproachReleaseRemainingSeconds = 0f;
            route.OverrunMode = OverrunProtectionMode.None;
            route.OverrunPhase = OverrunProtectionPhase.None;
            route.OverrunReleaseRemainingSeconds = 0f;
            AssertActiveRoute(firstOutput.StationsById["Station"].RoutesById["Route"]);

            TrackInterlockingManagementLogic.Calculate(context);
            var secondOutput = context.Output;
            Assert.That(secondOutput, Is.Not.SameAs(firstOutput));
            AssertDefaultRoute(secondOutput.StationsById["Station"].RoutesById["Route"]);

            station.IsInitialized = false;
            station.HasOutput = false;
            station.RoutesById.Clear();
            context.Input.StationsById.Clear();
            TrackInterlockingManagementLogic.Calculate(context);

            Assert.That(context.Output.StationsById, Is.Empty);
            Assert.That(firstOutput.StationsById["Station"].IsInitialized, Is.True);
            Assert.That(firstOutput.StationsById["Station"].HasOutput, Is.True);
            AssertActiveRoute(firstOutput.StationsById["Station"].RoutesById["Route"]);
            AssertDefaultRoute(secondOutput.StationsById["Station"].RoutesById["Route"]);
        }

        [Test]
        public void PublishedDictionariesRejectMutationThroughCollectionInterfaces()
        {
            var context = new TrackInterlockingManagementContext();
            var station = ReadyStation();
            station.RoutesById.Add("Route", ActiveRoute());
            context.Input.StationsById.Add("Station", station);
            TrackInterlockingManagementLogic.Initialize(context);
            TrackInterlockingManagementLogic.Calculate(context);

            var stations = context.Output.StationsById as IDictionary<string, TrackInterlockingManagementStationStatus>;
            if (stations != null)
            {
                Assert.That(stations.IsReadOnly, Is.True);
                Assert.Throws<NotSupportedException>(() => stations.Clear());
            }

            var routes = context.Output.StationsById["Station"].RoutesById as
                IDictionary<string, TrackInterlockingManagementRouteStatus>;
            if (routes != null)
            {
                Assert.That(routes.IsReadOnly, Is.True);
                Assert.Throws<NotSupportedException>(() => routes.Clear());
            }

            Assert.That(context.Output.StationsById.ContainsKey("Station"), Is.True);
            AssertActiveRoute(context.Output.StationsById["Station"].RoutesById["Route"]);
        }

        private static TrackInterlockingManagementStationInput ReadyStation()
        {
            return new TrackInterlockingManagementStationInput
            {
                IsInitialized = true,
                HasOutput = true
            };
        }

        private static TrackInterlockingManagementRouteInput ActiveRoute()
        {
            return new TrackInterlockingManagementRouteInput
            {
                IsAvailable = true,
                IsRouteSet = true,
                PathEstablished = true,
                ProceedAllowed = true,
                RouteLocked = true,
                CancelPending = true,
                ApproachLocked = true,
                ApproachReleaseRemainingSeconds = 7f,
                OverrunMode = OverrunProtectionMode.Restricted,
                OverrunPhase = OverrunProtectionPhase.ReleaseTiming,
                OverrunReleaseRemainingSeconds = 11f
            };
        }

        private static void AssertActiveRoute(TrackInterlockingManagementRouteStatus route)
        {
            Assert.That(route.IsAvailable, Is.True);
            Assert.That(route.IsRouteSet, Is.True);
            Assert.That(route.PathEstablished, Is.True);
            Assert.That(route.ProceedAllowed, Is.True);
            Assert.That(route.RouteLocked, Is.True);
            Assert.That(route.CancelPending, Is.True);
            Assert.That(route.ApproachLocked, Is.True);
            Assert.That(route.ApproachReleaseRemainingSeconds, Is.EqualTo(7f));
            Assert.That(route.OverrunMode, Is.EqualTo(OverrunProtectionMode.Restricted));
            Assert.That(route.OverrunPhase, Is.EqualTo(OverrunProtectionPhase.ReleaseTiming));
            Assert.That(route.OverrunReleaseRemainingSeconds, Is.EqualTo(11f));
        }

        private static void AssertDefaultRoute(TrackInterlockingManagementRouteStatus route)
        {
            Assert.That(route.IsAvailable, Is.False);
            Assert.That(route.IsRouteSet, Is.False);
            Assert.That(route.PathEstablished, Is.False);
            Assert.That(route.ProceedAllowed, Is.False);
            Assert.That(route.RouteLocked, Is.False);
            Assert.That(route.CancelPending, Is.False);
            Assert.That(route.ApproachLocked, Is.False);
            Assert.That(route.ApproachReleaseRemainingSeconds, Is.Zero);
            Assert.That(route.OverrunMode, Is.EqualTo(OverrunProtectionMode.None));
            Assert.That(route.OverrunPhase, Is.EqualTo(OverrunProtectionPhase.None));
            Assert.That(route.OverrunReleaseRemainingSeconds, Is.Zero);
        }
    }
}
