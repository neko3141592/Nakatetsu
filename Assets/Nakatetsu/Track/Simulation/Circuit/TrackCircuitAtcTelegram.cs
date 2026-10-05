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

        // 受信・転送時点の内容を保持するため、進路のリストも複製する。
        public TrackCircuitAtcTelegram Clone()
        {
            var copy = new TrackCircuitAtcTelegram
            {
                issuedAtSeconds = issuedAtSeconds,
                isValid = isValid
            };
            foreach (var pair in atcRouteInfomation)
            {
                if (pair.Value == null)
                {
                    copy.atcRouteInfomation.Add(pair.Key, null);
                    continue;
                }

                copy.atcRouteInfomation.Add(pair.Key, new TrackCircuitAtcRouteInfomation
                {
                    atcEdgePath = pair.Value.atcEdgePath == null ? null : new List<string>(pair.Value.atcEdgePath),
                    stopAtcEdgeId = pair.Value.stopAtcEdgeId,
                    overrunProtectionMode = pair.Value.overrunProtectionMode
                });
            }
            return copy;
        }
    }
}
