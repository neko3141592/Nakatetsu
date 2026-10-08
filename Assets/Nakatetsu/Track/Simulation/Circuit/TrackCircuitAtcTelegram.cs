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

        // 地上で選択したEdgeを起点とする、各進行方向の経路。
        public TrackCircuitAtcRouteInfomation routeAtoB;
        public TrackCircuitAtcRouteInfomation routeBtoA;

        public TrackCircuitAtcRouteInfomation GetRoute(TrackAtcTravelDirection direction)
        {
            return direction switch
            {
                TrackAtcTravelDirection.AtoB => routeAtoB,
                TrackAtcTravelDirection.BtoA => routeBtoA,
                _ => null
            };
        }

        // 受信・転送時点の内容を保持するため、進路のリストも複製する。
        public TrackCircuitAtcTelegram Clone()
        {
            var copy = new TrackCircuitAtcTelegram
            {
                issuedAtSeconds = issuedAtSeconds,
                isValid = isValid,
                routeAtoB = CopyRoute(routeAtoB),
                routeBtoA = CopyRoute(routeBtoA)
            };
            return copy;
        }

        private static TrackCircuitAtcRouteInfomation CopyRoute(TrackCircuitAtcRouteInfomation route)
        {
            if (route == null)
            {
                return null;
            }

            return new TrackCircuitAtcRouteInfomation
            {
                atcEdgePath = route.atcEdgePath == null ? null : new List<string>(route.atcEdgePath),
                stopAtcEdgeId = route.stopAtcEdgeId,
                overrunProtectionMode = route.overrunProtectionMode
            };
        }
    }
}
