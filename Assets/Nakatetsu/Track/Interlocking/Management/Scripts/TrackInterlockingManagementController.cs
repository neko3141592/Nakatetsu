using System;
using System.Collections.Generic;
using Nakatetsu.Contracts.Interlocking;
using Nakatetsu.Core.Simulation;
using UnityEngine;

namespace Nakatetsu.Track.Interlocking.Management
{
    [DisallowMultipleComponent]
    public sealed class TrackInterlockingManagementController : MonoBehaviour, ISimulationController
    {
        [SerializeField] private List<TrackInterlockingManagementStationRegistration> stations = new();

        private readonly TrackInterlockingManagementContext context = new();
        private readonly Dictionary<string, TrackStationInterlockingController> registeredStationsById = new();
        private readonly List<TrackStationInterlockingController> calculatedStations = new();

        public TrackInterlockingManagementContext Context => context;
        public bool IsInitialized => context.State.IsInitialized;

        private void Awake()
        {
            if (!TryInitialize(out string error))
            {
                Debug.LogError($"連動管理部の初期化に失敗しました: {error}", this);
            }
        }

        public bool TryInitialize(out string error)
        {
            TrackInterlockingManagementLogic.Reset(context);
            context.Input.StationsById.Clear();
            registeredStationsById.Clear();
            calculatedStations.Clear();

            if (!TryPrepareRegistrations(out var registrations, out error))
            {
                return false;
            }

            foreach (var pair in registrations)
            {
                registeredStationsById.Add(pair.Key, pair.Value);
            }
            TrackInterlockingManagementLogic.Initialize(context);
            CollectInput();
            return true;
        }

        public void CollectInput()
        {
            var input = context.Input;
            input.StationsById.Clear();
            if (!IsInitialized || !isActiveAndEnabled)
            {
                return;
            }

            foreach (var pair in registeredStationsById)
            {
                var station = pair.Value;
                bool isInitialized = station != null && station.IsInitialized;
                var output = station != null && station.isActiveAndEnabled ? station.Context.Output : null;
                input.StationsById.Add(pair.Key,
                    TrackInterlockingManagementInputAdapter.CreateStationInput(isInitialized, output));
            }
        }

        public void Calculate(float deltaTimeSeconds)
        {
            calculatedStations.Clear();
            if (IsInitialized && isActiveAndEnabled)
            {
                AdvanceStationInterlockingSimulation(deltaTimeSeconds);
            }
            UpdatePublishedOutput();
        }

        public void ApplyOutput(float deltaTimeSeconds)
        {
            try
            {
                for (int i = 0; IsInitialized && isActiveAndEnabled && i < calculatedStations.Count; i++)
                {
                    var station = calculatedStations[i];
                    if (CanAdvanceStation(station))
                    {
                        station.ApplyOutput(deltaTimeSeconds);
                    }
                }
            }
            finally
            {
                // 計算しなかった駅や、前回の転換要求を再適用しない。
                calculatedStations.Clear();
                UpdatePublishedOutput(retainOnlyPublishedStations: true);
            }
        }

        private void AdvanceStationInterlockingSimulation(float deltaTimeSeconds)
        {
            foreach (var pair in registeredStationsById)
            {
                var interlocking = pair.Value;
                if (!CanAdvanceStation(interlocking))
                {
                    continue;
                }
                interlocking.Calculate(deltaTimeSeconds);
                calculatedStations.Add(interlocking);
            }
        }

        private static bool CanAdvanceStation(TrackStationInterlockingController station)
        {
            return station != null && station.isActiveAndEnabled && station.IsInitialized;
        }

        private void UpdatePublishedOutput(bool retainOnlyPublishedStations = false)
        {
            var previousOutput = context.Output;
            CollectInput();
            if (retainOnlyPublishedStations)
            {
                foreach (var pair in registeredStationsById)
                {
                    // 計算をスキップした駅の古い公開値は、再有効化されても次のCalculateまで使わない。
                    if (!previousOutput.StationsById.ContainsKey(pair.Key))
                    {
                        context.Input.StationsById.Remove(pair.Key);
                    }
                }
            }
            TrackInterlockingManagementLogic.Calculate(context);
        }

        private void OnDisable()
        {
            calculatedStations.Clear();
            UpdatePublishedOutput();
        }

        private bool TryPrepareRegistrations(
            out Dictionary<string, TrackStationInterlockingController> registrations, out string error)
        {
            registrations = new Dictionary<string, TrackStationInterlockingController>();
            error = string.Empty;
            if (stations == null)
            {
                error = "駅の登録一覧を取得できません。";
                return false;
            }

            var registeredControllers = new HashSet<TrackStationInterlockingController>();
            foreach (var station in stations)
            {
                if (station == null || string.IsNullOrWhiteSpace(station.StationId))
                {
                    error = "駅IDを設定してください。";
                    return false;
                }
                if (registrations.ContainsKey(station.StationId))
                {
                    error = $"駅ID '{station.StationId}' が重複しています。";
                    return false;
                }
                if (station.Interlocking == null)
                {
                    error = $"駅 '{station.StationId}' の連動装置を設定してください。";
                    return false;
                }
                if (!registeredControllers.Add(station.Interlocking))
                {
                    error = $"駅 '{station.StationId}' の連動装置が重複登録されています。";
                    return false;
                }

                registrations.Add(station.StationId, station.Interlocking);
            }
            return true;
        }

        public InterlockingRouteRequestResult TryRouteRequest(InterlockingRouteRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.RequestId) ||
                string.IsNullOrWhiteSpace(request.StationId) || string.IsNullOrWhiteSpace(request.RouteId) ||
                (request.Operation != InterlockingRouteOperation.Set &&
                    request.Operation != InterlockingRouteOperation.Cancel))
            {
                return InterlockingRouteRequestResultUtility.CreateInvalidRequestError(request,
                    "要求ID、駅ID、進路IDと設定・取消の操作種別を指定してください。");
            }
            if (!IsInitialized)
            {
                return InterlockingRouteRequestResultUtility.CreateNotInitializedError(request,
                    "連動管理部が初期化されていない。");
            }
            if (!registeredStationsById.TryGetValue(request.StationId, out var interlocking))
            {
                return InterlockingRouteRequestResultUtility.CreateUnknownStationError(request,
                    $"駅 '{request.StationId}' が登録されていない。");
            }
            if (interlocking == null || !interlocking.isActiveAndEnabled)
            {
                return InterlockingRouteRequestResultUtility.CreateInputUnavailableError(request,
                    $"駅 '{request.StationId}' の連動装置を利用できない。");
            }

            return request.Operation == InterlockingRouteOperation.Set
                ? interlocking.TryRequestRoute(request)
                : interlocking.TryCancelRoute(request);
        }
    }

    [Serializable]
    public sealed class TrackInterlockingManagementStationRegistration
    {
        [SerializeField] private string stationId;
        [SerializeField] private TrackStationInterlockingController interlocking;

        public string StationId => stationId;
        public TrackStationInterlockingController Interlocking => interlocking;
    }
}
