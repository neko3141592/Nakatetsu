using System;
using System.Collections.Generic;
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
            if (!IsInitialized)
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
            CollectInput();
            TrackInterlockingManagementLogic.Calculate(context);
        }

        public void ApplyOutput(float deltaTimeSeconds)
        {
            // 集約結果はContext.Outputから公開する。
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
