using System;
using System.Collections.Generic;

namespace Nakatetsu.Track.Atc
{
    public enum TrackAtcTravelDirection
    {
        Unspecified = 0,
        AtoB,
        BtoA
    }

    // 閉塞区分
    public enum TrackAtcEdgeControlKind
    {
        Unspecified = 0,

        // 自動閉塞
        AutomaticBlock,

        // 非自動閉塞
        NonAutomaticBlock,

        // 構内
        YardOperation
    }


    [Serializable]
    public sealed class TrackAtcGraphDefinition
    {
        public string atcGraphId;
        public List<TrackAtcGraphEdge> atcEdge = new();
        public List<TrackAtcGraphNode> atcNode = new();
        public List<TrackAtcRouteDefinition> routes = new();
    }

    [Serializable]
    public sealed class TrackAtcGradientProfile
    {
        public float distanceOnAtcEdgeM;
        public float gradientPermille;

    }

    [Serializable]
    public sealed class TrackAtcSpeedLimitSection
    {
        public float startDistanceOnAtcEdgeM;
        public float endDistanceOnAtcEdgeM;
        public float speedLimitKmh;
        public float SpeedLimitMS => speedLimitKmh / 3.6f;
    }

    [Serializable]
    public sealed class TrackAtcGraphEdge
    {
        public string atcEdgeId;
        public string trackCircuitId;

        public TrackAtcEdgeControlKind controlKind;

        // 非連動エッジの通行方向。連動進路の方向は進路定義から求める。
        public TrackAtcTravelDirection direction;


        public float lengthM;
        public List<TrackAtcSpeedLimitSection> speedLimitSections = new();

        // ATC EdgeのA→B方向。逆走時は距離を反転し、勾配の符号を反転する。
        public List<TrackAtcGradientProfile> gradientProfiles = new();

        public string atcNodeAId;
        public string atcNodeBId;


        // シミュレーター上の情報(本来ATCからは読めない)
        public string trackEdgeId;
        public float startDistanceOnEdgeM;
        public float endDistanceOnEdgeM;

    }

    [Serializable]
    public sealed class TrackAtcGraphNode
    {
        public string atcNodeId;
        public List<string> connectedAtcEdgeIds = new();
    }

    [Serializable]
    public sealed class TrackAtcRouteDefinition
    {
        public string atcRouteId;
        public string interlockingRouteId;

        // 順序付き
        public List<string> atcEdgeIds = new();


    }
}
