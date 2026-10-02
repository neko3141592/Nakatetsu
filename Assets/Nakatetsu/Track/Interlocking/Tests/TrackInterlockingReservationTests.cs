using System;
using System.Collections.Generic;
using System.Reflection;
using Nakatetsu.Track.Simulation.Circuit;
using Nakatetsu.Track.Simulation.Connection;
using NUnit.Framework;

namespace Nakatetsu.Track.Interlocking.Tests
{
    public sealed class TrackInterlockingReservationTests
    {
        private TrackInterlockingContext context;
        private TrackCircuitSimulationState circuits;
        private TrackConnectionContext connections;
        private InterlockingRoute existingRoute;
        private InterlockingRoute requestedRoute;

        [SetUp]
        public void SetUp()
        {
            context = new TrackInterlockingContext();
            circuits = new TrackCircuitSimulationState();
            connections = new TrackConnectionContext();
            typeof(TrackConnectionContext).GetProperty(nameof(TrackConnectionContext.IsInitialized))
                .SetValue(connections, true);
            foreach (string circuitId in new[] { "ExistingMain", "RequestedMain", "Common", "Additional", "Other" })
                SetOccupied(circuitId, false);

            existingRoute = CreateRoute("Existing", "ExistingMain");
            requestedRoute = CreateRoute("Requested", "RequestedMain");
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void RequestRejectsCircuitHeldByExistingOverrun(bool additional, bool requestedOverrun)
        {
            existingRoute.overrunProtection = CreateProtection("Common", "Additional");
            string conflictingCircuit = additional ? "Additional" : "Common";
            if (requestedOverrun)
                requestedRoute.overrunProtection = CreateProtection(conflictingCircuit);
            else
                requestedRoute.routeLockTrackCircuitIds = new List<string> { conflictingCircuit };
            InitializeAndReserveExisting();

            string error = AssertRequestedRouteRejected();
            Assert.That(error, Does.Contain(conflictingCircuit).And.Contain("Existing"));
        }

        [TestCase(OverrunProtectionPhase.Setting)]
        [TestCase(OverrunProtectionPhase.Established)]
        [TestCase(OverrunProtectionPhase.ReleaseTiming)]
        public void RequestRejectsCommonCircuitInEveryHoldingPhase(OverrunProtectionPhase phase)
        {
            existingRoute.overrunProtection = CreateProtection("Common");
            requestedRoute.routeLockTrackCircuitIds = new List<string> { "Common" };
            InitializeAndReserveExisting();
            RouteStates["Existing"].overrunProtectionPhase = phase;

            AssertRequestedRouteRejected();
        }

        [TestCase(OverrunProtectionPhase.None, false)]
        [TestCase(OverrunProtectionPhase.None, true)]
        [TestCase(OverrunProtectionPhase.Released, false)]
        [TestCase(OverrunProtectionPhase.Released, true)]
        public void RequestIgnoresOverrunCircuitsThatAreNotHeld(OverrunProtectionPhase phase, bool requestedOverrun)
        {
            existingRoute.overrunProtection = CreateProtection("Common", "Additional");
            if (requestedOverrun)
                requestedRoute.overrunProtection = CreateProtection("Common", "Additional");
            else
                requestedRoute.routeLockTrackCircuitIds = new List<string> { "Common", "Additional" };
            InitializeAndReserveExisting();
            RouteStates["Existing"].overrunProtectionPhase = phase;

            AssertRequestedRouteAccepted();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExistingRouteWithoutOverrunDoesNotThrow(bool requestedOverrun)
        {
            if (requestedOverrun)
                requestedRoute.overrunProtection = CreateProtection("Common", "Additional");
            InitializeAndReserveExisting();

            AssertRequestedRouteAccepted();
            Assert.That(RouteStates["Requested"].overrunProtectionMode,
                Is.EqualTo(requestedOverrun ? OverrunProtectionMode.Normal : OverrunProtectionMode.None));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExistingRestrictedProtectionHoldsOnlyCommonCircuits(bool requestAdditional)
        {
            existingRoute.overrunProtection = CreateProtection("Common", "Additional");
            requestedRoute.routeLockTrackCircuitIds = new List<string> { requestAdditional ? "Additional" : "Common" };
            InitializeAndReserveExisting();
            // 既存のRestricted状態に対する照査。新規Restricted受付は追加制御の実装まで禁止。
            RouteStates["Existing"].overrunProtectionMode = OverrunProtectionMode.Restricted;

            if (requestAdditional)
                AssertRequestedRouteAccepted();
            else
                AssertRequestedRouteRejected();
        }

        [TestCase("definition")]
        [TestCase("commonCircuits")]
        [TestCase("additionalCircuits")]
        public void IncompleteHeldProtectionRejectsWithoutThrowing(string missingPart)
        {
            existingRoute.overrunProtection = CreateProtection("Common", "Additional");
            InitializeAndReserveExisting();
            switch (missingPart)
            {
                case "definition": existingRoute.overrunProtection = null; break;
                case "commonCircuits": existingRoute.overrunProtection.common.clearTrackCircuitIds = null; break;
                case "additionalCircuits": existingRoute.overrunProtection.normalAdditional.clearTrackCircuitIds = null; break;
            }

            Assert.That(AssertRequestedRouteRejected(), Does.Contain("incomplete"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExistingMainReservationRejectsRequestedMainOrOverrun(bool requestedOverrun)
        {
            if (requestedOverrun)
                requestedRoute.overrunProtection = CreateProtection("ExistingMain");
            else
                requestedRoute.routeLockTrackCircuitIds = new List<string> { "ExistingMain" };
            InitializeAndReserveExisting();

            Assert.That(AssertRequestedRouteRejected(), Does.Contain("ExistingMain"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnavailableNormalProtectionDoesNotFallBackOrLeavePartialReservation(bool occupied)
        {
            string additionalCircuit = occupied ? "Additional" : "UnknownCircuit";
            requestedRoute.overrunProtection = CreateProtection("Common", additionalCircuit);
            InitializeAndReserveExisting();
            if (occupied) SetOccupied(additionalCircuit, true);

            string error = AssertRequestedRouteRejected();
            Assert.That(error, Does.Contain(additionalCircuit).And.Contain("Restricted"));

            SetOccupied(additionalCircuit, false);
            AssertRequestedRouteAccepted();
            Assert.That(RouteStates["Requested"].overrunProtectionMode, Is.EqualTo(OverrunProtectionMode.Normal));
        }

        [Test]
        public void ReservedNormalAdditionalCircuitDoesNotFallBackToRestricted()
        {
            requestedRoute.overrunProtection = CreateProtection("Common", "ExistingMain");
            InitializeAndReserveExisting();

            Assert.That(AssertRequestedRouteRejected(), Does.Contain("ExistingMain").And.Contain("Restricted"));
        }

        [TestCase(false, false, false)]
        [TestCase(false, false, true)]
        [TestCase(false, true, false)]
        [TestCase(false, true, true)]
        [TestCase(true, false, false)]
        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        [TestCase(true, true, true)]
        public void ExistingOverrunTurnoutRejectsOppositePositionOnly(bool additional, bool requestedOverrun, bool oppositePosition)
        {
            existingRoute.overrunProtection = CreateProtection("Common", "Additional");
            var existingResources = additional ? existingRoute.overrunProtection.normalAdditional : existingRoute.overrunProtection.common;
            existingResources.requiredTurnouts.Add(new TurnoutRequirement
            {
                connectionId = "Switch", requiredPosition = TrackSwitchPosition.Normal
            });
            var requirement = new TurnoutRequirement
            {
                connectionId = "Switch",
                requiredPosition = oppositePosition ? TrackSwitchPosition.Reverse : TrackSwitchPosition.Normal
            };
            if (requestedOverrun)
            {
                requestedRoute.overrunProtection = CreateProtection("Other");
                requestedRoute.overrunProtection.common.requiredTurnouts.Add(requirement);
            }
            else
                requestedRoute.requiredTurnouts.Add(requirement);
            AddSwitch();
            InitializeAndReserveExisting();

            if (oppositePosition)
                Assert.That(AssertRequestedRouteRejected(), Does.Contain("Switch"));
            else
                AssertRequestedRouteAccepted();
        }

        private void InitializeAndReserveExisting()
        {
            var definition = new TrackInterlockingDefinition();
            definition.routes.Add(existingRoute);
            definition.routes.Add(requestedRoute);
            definition.turnoutTrackCircuitLocks.Add(new TurnoutTrackCircuitLock
            {
                connectionId = "Switch", trackCircuitIds = new List<string> { "Other" }
            });
            Assert.That(TrackInterlockingLogic.TryInitialize(context, definition, out string error), Is.True, error);
            Assert.That(TrackInterlockingLogic.TryRequestRoute(context, circuits, connections, "Existing", out error), Is.True, error);
        }

        private string AssertRequestedRouteRejected()
        {
            var existingState = RouteStates["Existing"];
            Assert.That(TrackInterlockingLogic.TryRequestRoute(context, circuits, connections, "Requested", out string error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(RouteStates.Count, Is.EqualTo(1), "失敗した要求の予約を残さないこと");
            Assert.That(RouteStates["Existing"], Is.SameAs(existingState), "既存予約を維持すること");
            return error;
        }

        private void AssertRequestedRouteAccepted()
        {
            Assert.That(TrackInterlockingLogic.TryRequestRoute(context, circuits, connections, "Requested", out string error), Is.True, error);
            Assert.That(error, Is.Null);
            Assert.That(RouteStates.Count, Is.EqualTo(2));
        }

        private static InterlockingRoute CreateRoute(string routeId, string circuitId) => new()
        {
            routeId = routeId, routeLockTrackCircuitIds = new List<string> { circuitId }
        };

        private static OverrunProtectionDefinition CreateProtection(string common, string additional = null)
        {
            var protection = new OverrunProtectionDefinition();
            protection.common.clearTrackCircuitIds.Add(common);
            if (additional != null) protection.normalAdditional.clearTrackCircuitIds.Add(additional);
            return protection;
        }

        // 外部公開されていないシミュレーション入力・予約状態だけをテスト用に取得する。
        private Dictionary<string, TrackInterlockingRouteState> RouteStates =>
            (Dictionary<string, TrackInterlockingRouteState>)typeof(TrackInterlockingContext)
                .GetField("RouteStatesById", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(context);

        private void SetOccupied(string circuitId, bool occupied) =>
            typeof(TrackCircuitSimulationState).GetMethod("SetOccupied", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(circuits, new object[] { circuitId, occupied });

        private void AddSwitch()
        {
            var states = (Dictionary<string, TrackConnectionState>)typeof(TrackConnectionContext)
                .GetField("StatesById", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(connections);
            states.Add("Switch", (TrackConnectionState)Activator.CreateInstance(typeof(TrackConnectionState),
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { TrackSwitchPosition.Normal, TrackSwitchPosition.Normal, false }, null));
        }
    }
}
