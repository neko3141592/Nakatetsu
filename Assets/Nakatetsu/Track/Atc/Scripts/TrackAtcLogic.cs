using UnityEditor.Experimental.GraphView;

namespace Nakatetsu.Track.Atc
{
    public static class TrackAtcLogic
    {
        public const int guard = 512;
        public static void Calculate(TrackAtcContext context, float deltaTimeSeconds)
        {
            // 経路探索・停止限界の計算は次の実装で追加する。
        }

        public static bool TryUpdateNextEdges(
            TrackAtcContext context,
            float deltaTimeSeconds
        )
        {
            if (context.Graph == null || context.Graph.atcEdge == null)
            {
                return false;
            }

            context.State.visitedAtcEdgeIds.Clear();
            foreach (var atcEdge in context.Graph.atcEdge)
            {
                if (atcEdge.atcEdgeId == null)
                {
                    return false;
                }

                if (context.State.visitedAtcEdgeIds.Contains(atcEdge.atcEdgeId))
                {
                    continue;
                }

                CalculateNextEdge(
                    context,
                    atcEdge
                );
            }
        }

        public static void CalculateNextEdge(
            TrackAtcContext context,
            TrackAtcGraphEdge startAtcEdge
        )
        {
            TrackAtcGraphEdge currentEdge = startAtcEdge;

            for (int i = 0; i < guard; i++)
            {
                // 制御未設定のエッジであれば底を停止限界にする
                if (currentEdge.controlKind == TrackAtcEdgeControlKind.Unspecified)
                {
                    return;
                }

                


            }

        }
 

    }
}
