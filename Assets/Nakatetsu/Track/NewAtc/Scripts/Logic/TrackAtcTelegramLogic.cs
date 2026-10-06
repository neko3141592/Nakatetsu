namespace Nakatetsu.Track.NewAtc
{
    internal static class TrackAtcTelegramLogic
    {
        internal static void UpdateTelegram(TrackAtcContext context)
        {
            var telegram = context.State.telegram;
            var validation = context.State.validation;
            telegram.circuitsById.Clear();
            if (!validation.isGraphValid)
            {
                return;
            }

            CreateCircuitTargets(context);
            AggregateRouteResults(context);
            FinalizeCircuitValidity(context);
        }

        private static void CreateCircuitTargets(TrackAtcContext context)
        {
            foreach (var edge in context.State.validation.atcEdgesById.Values)
            {
                GetOrCreateCircuit(context.State.telegram, edge.trackCircuitId);
            }
        }

        private static void AggregateRouteResults(TrackAtcContext context)
        {
            var validation = context.State.validation;
            var telegram = context.State.telegram;
            foreach (var pair in context.State.path.resultsByKey)
            {
                if (!validation.atcEdgesById.TryGetValue(pair.Key.atcEdgeId, out var edge))
                {
                    // 確認済みGraphにない起点は工程間の結果不整合。
                    // 所属回路を特定できないため、今回の全送信対象を無効にする。
                    InvalidateAllCircuits(telegram, $"Path root '{pair.Key.atcEdgeId}' is unavailable.");
                    continue;
                }

                var circuit = GetOrCreateCircuit(telegram, edge.trackCircuitId);
                var path = pair.Value;
                if (path == null || !path.isPathValid)
                {
                    var reason = GetFailureReason(path?.failureReason, "Path calculation failed.");
                    InvalidateCircuit(circuit, reason);
                    InvalidateAffectedCircuits(telegram, path, reason);
                    continue;
                }

                if (!context.State.protectionMode.resultsByKey.TryGetValue(pair.Key, out var protection) ||
                    protection == null || !protection.isProtectionModeKnown)
                {
                    var reason = GetFailureReason(protection?.failureReason, "Protection mode is unavailable.");
                    InvalidateCircuit(circuit, reason);
                    InvalidateAffectedCircuits(telegram, path, reason);
                    continue;
                }

                circuit.routeKeys.Add(pair.Key);
            }
        }

        private static void FinalizeCircuitValidity(TrackAtcContext context)
        {
            if (!context.State.validation.isSimulationTimeValid)
            {
                InvalidateAllCircuits(context.State.telegram, "Simulation time is invalid.");
            }

            foreach (var circuit in context.State.telegram.circuitsById.Values)
            {
                if (circuit.routeKeys.Count == 0)
                {
                    InvalidateCircuit(circuit, "No usable route results are available.");
                }
            }
        }

        private static TrackAtcTelegramCircuitResult GetOrCreateCircuit(
            TrackAtcTelegramState telegram,
            string circuitId)
        {
            if (!telegram.circuitsById.TryGetValue(circuitId, out var result))
            {
                result = new TrackAtcTelegramCircuitResult { isValid = true };
                telegram.circuitsById.Add(circuitId, result);
            }

            return result;
        }

        private static void InvalidateAffectedCircuits(
            TrackAtcTelegramState telegram,
            TrackAtcPathResult path,
            string reason)
        {
            if (path == null || path.affectedCircuitIds == null)
            {
                return;
            }

            foreach (var circuitId in path.affectedCircuitIds)
            {
                var circuit = GetOrCreateCircuit(telegram, circuitId);
                InvalidateCircuit(circuit, reason);
            }
        }

        private static void InvalidateAllCircuits(TrackAtcTelegramState telegram, string reason)
        {
            foreach (var circuit in telegram.circuitsById.Values)
            {
                InvalidateCircuit(circuit, reason);
            }
        }

        private static void InvalidateCircuit(TrackAtcTelegramCircuitResult circuit, string reason)
        {
            circuit.isValid = false;
            circuit.failureReasons.Add(reason);
        }

        private static string GetFailureReason(string reason, string fallback)
        {
            return string.IsNullOrEmpty(reason) ? fallback : reason;
        }
    }
}
