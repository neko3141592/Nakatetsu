using Nakatetsu.Track.Simulation.Circuit;

namespace Nakatetsu.Track.NewAtc
{
    internal static class TrackAtcProtectionModeLogic
    {
        internal static void UpdateProtectionMode(TrackAtcContext context)
        {
            var protectionMode = context.State.protectionMode;
            protectionMode.resultsByKey.Clear();

            foreach (var pair in context.State.path.resultsByKey)
            {
                protectionMode.resultsByKey.Add(pair.Key, DetermineProtectionMode(context, pair.Value));
            }
        }

        private static TrackAtcProtectionModeResult DetermineProtectionMode(
            TrackAtcContext context,
            TrackAtcPathResult path)
        {
            var result = new TrackAtcProtectionModeResult();
            if (path == null || !path.isPathValid)
            {
                result.failureReason = path?.failureReason ?? "Path result is unavailable.";
                return result;
            }

            if (string.IsNullOrEmpty(path.terminalAtcRouteId))
            {
                // 進路未設定・占有境界など、停止限界に終端進路が対応しない
                // 正常停止は、過走防護なしのNoneとして確定する。
                result.overrunProtectionMode = OverrunProtectionMode.None;
                result.isProtectionModeKnown = true;
                return result;
            }

            if (!TryGetTerminalProtectionMode(context, path.terminalAtcRouteId,
                out var mode, out var failureReason))
            {
                result.failureReason = failureReason;
                return result;
            }

            result.overrunProtectionMode = mode;
            result.isProtectionModeKnown = true;
            return result;
        }

        private static bool TryGetTerminalProtectionMode(
            TrackAtcContext context,
            string terminalAtcRouteId,
            out OverrunProtectionMode mode,
            out string failureReason)
        {
            mode = default;
            failureReason = string.Empty;
            var validation = context.State.validation;
            if (!validation.atcRoutesById.TryGetValue(terminalAtcRouteId, out var route) || route == null)
            {
                failureReason = $"Terminal ATC route '{terminalAtcRouteId}' is unavailable.";
                return false;
            }

            var interlockingRouteId = route.interlockingRouteId;
            if (string.IsNullOrEmpty(interlockingRouteId) ||
                !validation.hasRouteInputById.TryGetValue(interlockingRouteId, out var hasInput) ||
                !hasInput ||
                !context.Input.RoutesById.TryGetValue(interlockingRouteId, out var input) || input == null)
            {
                failureReason = $"Terminal route input '{interlockingRouteId}' is unavailable.";
                return false;
            }

            if (!input.IsRouteSet)
            {
                failureReason = $"Terminal route '{interlockingRouteId}' is not set.";
                return false;
            }

            if (input.OverrunMode != OverrunProtectionMode.None &&
                input.OverrunMode != OverrunProtectionMode.Normal &&
                input.OverrunMode != OverrunProtectionMode.Restricted)
            {
                failureReason = $"Terminal route '{interlockingRouteId}' has an invalid protection mode.";
                return false;
            }

            mode = input.OverrunMode;
            return true;
        }
    }
}
