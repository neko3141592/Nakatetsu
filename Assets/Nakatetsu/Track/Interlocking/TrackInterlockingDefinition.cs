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
        [Min(0f)] public float releaseSeconds = 60f;
    }

    [Serializable]
    public sealed class InterlockingRoute
    {
        public string routeId;

        // 発点・着点
        public string startTrackCircuitId;
        public string destinationTrackCircuitId;

        // 転轍機鎖錠
        public List<TurnoutRequirement> requiredTurnouts = new();

        // 進路内の回路。占有判定と列車通過後の解錠判定に使う。
        public List<string> routeLockTrackCircuitIds = new();

        // 進路入口の手前にある接近回路と、取消時の解錠待ち時間
        public ApproachLockDefinition approachLock = new();

        // 競合進路
        public List<string> conflictRouteIds = new();
    }

    [Serializable]
    public sealed class  TurnoutTrackCircuitLock
    {
        public string connectionId;
        public List<string> trackCircuitIds = new();
    }

    [Serializable]
    public sealed class TrackInterlockingDefinition
    {
        public string interlockingId;

        public List<string> memberTrackCircuitIds = new();
        public List<string> memberConnectionIds = new();
        public List<InterlockingRoute> routes = new();

        public List<TurnoutTrackCircuitLock> turnoutTrackCircuitLocks = new(); 


    }
}
