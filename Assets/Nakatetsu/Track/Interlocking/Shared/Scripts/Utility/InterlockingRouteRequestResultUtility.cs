#nullable enable

using Nakatetsu.Contracts.Interlocking;

namespace Nakatetsu.Track.Interlocking
{
    public static class InterlockingRouteRequestResultUtility
    {
        /// <summary>要求の形式が不正。</summary>
        public static InterlockingRouteRequestResult CreateInvalidRequestError(
            InterlockingRouteRequest? request, string? message = null)
        {
            return new InterlockingRouteRequestResult
            {
                RequestId = request?.RequestId ?? string.Empty,
                StationId = request?.StationId ?? string.Empty,
                RouteId = request?.RouteId ?? string.Empty,
                Accepted = false,
                ErrorCode = InterlockingRouteErrorCode.InvalidRequest,
                Message = message
            };
        }

        /// <summary>対象の駅が未登録。</summary>
        public static InterlockingRouteRequestResult CreateUnknownStationError(
            InterlockingRouteRequest? request, string? message = null)
        {
            return new InterlockingRouteRequestResult
            {
                RequestId = request?.RequestId ?? string.Empty,
                StationId = request?.StationId ?? string.Empty,
                RouteId = request?.RouteId ?? string.Empty,
                Accepted = false,
                ErrorCode = InterlockingRouteErrorCode.UnknownStation,
                Message = message
            };
        }

        /// <summary>対象の進路が未定義。</summary>
        public static InterlockingRouteRequestResult CreateUnknownRouteError(
            InterlockingRouteRequest? request, string? message = null)
        {
            return new InterlockingRouteRequestResult
            {
                RequestId = request?.RequestId ?? string.Empty,
                StationId = request?.StationId ?? string.Empty,
                RouteId = request?.RouteId ?? string.Empty,
                Accepted = false,
                ErrorCode = InterlockingRouteErrorCode.UnknownRoute,
                Message = message
            };
        }

        /// <summary>要求処理に必要な装置が未初期化。</summary>
        public static InterlockingRouteRequestResult CreateNotInitializedError(
            InterlockingRouteRequest? request, string? message = null)
        {
            return new InterlockingRouteRequestResult
            {
                RequestId = request?.RequestId ?? string.Empty,
                StationId = request?.StationId ?? string.Empty,
                RouteId = request?.RouteId ?? string.Empty,
                Accepted = false,
                ErrorCode = InterlockingRouteErrorCode.NotInitialized,
                Message = message
            };
        }

        /// <summary>必要な設備情報を取得できない。</summary>
        public static InterlockingRouteRequestResult CreateInputUnavailableError(
            InterlockingRouteRequest? request,
            string? message = null,
            string? relatedCircuitId = null,
            string? relatedTurnoutId = null)
        {
            return new InterlockingRouteRequestResult
            {
                RequestId = request?.RequestId ?? string.Empty,
                StationId = request?.StationId ?? string.Empty,
                RouteId = request?.RouteId ?? string.Empty,
                Accepted = false,
                ErrorCode = InterlockingRouteErrorCode.InputUnavailable,
                RelatedCircuitId = relatedCircuitId,
                RelatedTurnoutId = relatedTurnoutId,
                Message = message
            };
        }

        /// <summary>対象進路が設定済み。</summary>
        public static InterlockingRouteRequestResult CreateAlreadySetError(
            InterlockingRouteRequest? request, string? message = null)
        {
            return new InterlockingRouteRequestResult
            {
                RequestId = request?.RequestId ?? string.Empty,
                StationId = request?.StationId ?? string.Empty,
                RouteId = request?.RouteId ?? string.Empty,
                Accepted = false,
                ErrorCode = InterlockingRouteErrorCode.AlreadySet,
                Message = message
            };
        }

        /// <summary>取消対象の進路が未設定。</summary>
        public static InterlockingRouteRequestResult CreateNotSetError(
            InterlockingRouteRequest? request, string? message = null)
        {
            return new InterlockingRouteRequestResult
            {
                RequestId = request?.RequestId ?? string.Empty,
                StationId = request?.StationId ?? string.Empty,
                RouteId = request?.RouteId ?? string.Empty,
                Accepted = false,
                ErrorCode = InterlockingRouteErrorCode.NotSet,
                Message = message
            };
        }

        /// <summary>設定照査に必要な軌道回路が在線。</summary>
        public static InterlockingRouteRequestResult CreateCircuitOccupiedError(
            InterlockingRouteRequest? request,
            string? relatedCircuitId = null,
            string? message = null)
        {
            return new InterlockingRouteRequestResult
            {
                RequestId = request?.RequestId ?? string.Empty,
                StationId = request?.StationId ?? string.Empty,
                RouteId = request?.RouteId ?? string.Empty,
                Accepted = false,
                ErrorCode = InterlockingRouteErrorCode.CircuitOccupied,
                RelatedCircuitId = relatedCircuitId,
                Message = message
            };
        }

        /// <summary>必要な軌道回路を他進路が予約中。</summary>
        public static InterlockingRouteRequestResult CreateCircuitReservedError(
            InterlockingRouteRequest? request,
            string? relatedCircuitId = null,
            string? relatedRouteId = null,
            string? message = null)
        {
            return new InterlockingRouteRequestResult
            {
                RequestId = request?.RequestId ?? string.Empty,
                StationId = request?.StationId ?? string.Empty,
                RouteId = request?.RouteId ?? string.Empty,
                Accepted = false,
                ErrorCode = InterlockingRouteErrorCode.CircuitReserved,
                RelatedCircuitId = relatedCircuitId,
                RelatedRouteId = relatedRouteId,
                Message = message
            };
        }

        /// <summary>他進路と競合。</summary>
        public static InterlockingRouteRequestResult CreateRouteConflictError(
            InterlockingRouteRequest? request,
            string? relatedRouteId = null,
            string? message = null)
        {
            return new InterlockingRouteRequestResult
            {
                RequestId = request?.RequestId ?? string.Empty,
                StationId = request?.StationId ?? string.Empty,
                RouteId = request?.RouteId ?? string.Empty,
                Accepted = false,
                ErrorCode = InterlockingRouteErrorCode.RouteConflict,
                RelatedRouteId = relatedRouteId,
                Message = message
            };
        }

        /// <summary>転轍機の要求位置が他進路の保持位置と競合。</summary>
        public static InterlockingRouteRequestResult CreateTurnoutPositionConflictError(
            InterlockingRouteRequest? request,
            string? relatedTurnoutId = null,
            string? relatedRouteId = null,
            string? message = null)
        {
            return new InterlockingRouteRequestResult
            {
                RequestId = request?.RequestId ?? string.Empty,
                StationId = request?.StationId ?? string.Empty,
                RouteId = request?.RouteId ?? string.Empty,
                Accepted = false,
                ErrorCode = InterlockingRouteErrorCode.TurnoutPositionConflict,
                RelatedTurnoutId = relatedTurnoutId,
                RelatedRouteId = relatedRouteId,
                Message = message
            };
        }
    }
}
