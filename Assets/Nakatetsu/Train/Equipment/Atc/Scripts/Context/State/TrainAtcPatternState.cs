using System.Collections.Generic;
using Nakatetsu.Track.Simulation.Circuit;

namespace Nakatetsu.Train.Equipment.Atc
{
    public sealed class TrainAtcPatternState
    {
        // 今回の現在位置で、パターンを使用できるか。
        public bool isValid;

        // 常用・非常・ORPで共通のサンプル間隔と経路長[m]。
        public float samplingIntervalM;
        public float pathLengthM;
        // 現在の受電器位置を、採用済み経路の始点から測った距離[m]。
        public float distanceOnPathM;

        // 採用済みの経路。Edge IDを進行順に保持する。
        public List<string> atcEdgePath = new();
        // 経路を採用した時点の始点Edgeでの進行方向。Edgeを越えても変更しない。
        public TrackAtcTravelDirection pathStartTravelDirection;

        public TrainAtcPattern normalPattern = new();
        public TrainAtcPattern emergencyPattern = new();
        public TrainAtcPattern orpPattern = new();
    }

    public class TrainAtcPattern
    {
        public float currentAllowSpeedMps;
        public float currentTargetSpeedMps;
        // 現在位置のサンプルに使った、勾配補正前の計算用減速度[m/s²]。
        public float currentDecelerationMps2;

        public bool isDecelerationSection;
        public bool isApproachSection;

        public List<TrainAtcPatternSample> samples = new();
    }

    public class TrainAtcPatternSample
    {
        // この地点でのパターン上限速度[m/s]。
        public float allowSpeedMps;
        // 減速先の目標速度[m/s]。予告区間でも減速先の速度を保持する。
        public float targetSpeedMps;

        // 勾配補正前の計算用減速度[m/s²]。
        public float decelerationMps2;
        // パターン速度が下がる区間と、下がり切った速度で1秒間進む距離までの区間。
        public bool isDecelerationSection;
        // 降下区間（終了後1秒分を含む）と、設定時間分だけ手前の予告区間。
        public bool isApproachSection;
    }
}
