using System;
using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Connection;
using UnityEngine;

namespace Nakatetsu.Track.Interlocking
{
    [Serializable]
    public sealed class TurnoutRequirement
    {
        public string connectionId;
        public TrackSwitchPosition requiredPosition;
    }

    [Serializable]
    public sealed class ApproachLockDefinition
    {
        public List<string> trackCircuitIds = new();

        // 解錠時素
        [Min(0f)] public float releaseSeconds = 60f;
    }

    [Serializable]
    public sealed class TrackStationInterlockingRouteDefinition
    {
        public string routeId;

        // 発点・着点
        public string startTrackCircuitId;
        public string destinationTrackCircuitId;

        // 進路確保時に鎖錠する転轍機
        public List<TurnoutRequirement> requiredTurnouts = new();

        // 進路設定時に空きを照査し、予約する軌道回路ID
        public List<string> routeClearTrackCircuitIds = new();

        // 全対象の後端通過を確認して本進路を解錠する軌道回路ID
        public List<string> routeReleaseTrackCircuitIds = new();

        // 接近鎖錠に使う軌道回路ID
        public ApproachLockDefinition approachLock = new();

        // 競合進路定義
        public List<string> conflictRouteIds = new();

        public TrackStationInterlockingOverrunProtectionDefinition overrunProtection = new();

    }
}
