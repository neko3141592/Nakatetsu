using System;
using System.Collections.Generic;

namespace Nakatetsu.Track.Simulation.Circuit
{
    public enum TrackAtcTravelDirection
    {
        Unspecified = 0,
        AtoB,
        BtoA
    }

    public enum OverrunProtectionMode
    {
        None,
        Normal,
        Restricted
    }

    [Serializable]
    public sealed class TrackCircuitAtcRouteInfomation
    {
        // 電文の対象Edgeから停止限界までの進行順。
        public List<string> atcEdgePath = new();
        // 停止限界はこのEdgeの進行方向の終端。距離は車上で求める。
        public string stopAtcEdgeId;
        public OverrunProtectionMode overrunProtectionMode;
    }


    [Serializable]
    public sealed class TrackCircuitAtcTelegram
    {
        public double issuedAtSeconds;

        public bool isValid;

        public readonly Dictionary<(string atcEdgeId, TrackAtcTravelDirection direction), TrackCircuitAtcRouteInfomation> atcRouteInfomation = new();
    }
}
