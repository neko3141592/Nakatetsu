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

            context.Workspace.visitedAtcEdgeIds.Clear();
            foreach (var atcEdge in context.Graph.atcEdge)
            {
                if (atcEdge.atcEdgeId == null)
                {
                    return false;
                }

                if (context.Workspace.visitedAtcEdgeIds.Contains(atcEdge.atcEdgeId))
                {
                    continue;
                }

                CalculateNextEdge(
                    context,
                    atcEdge
                );
            }

            return true;
        }

        public static void CalculateNextEdge(
            TrackAtcContext context,
            TrackAtcGraphEdge startAtcEdge
        )
        {
            TrackAtcGraphEdge currentEdge = startAtcEdge;

            for (int i = 0; i < guard; i++)
            {
                context.State.NextEdgeById[currentEdge.atcEdgeId] = null;

                // 制御未設定のエッジであればそこを停止限界にする
                if (currentEdge.controlKind == TrackAtcEdgeControlKind.Unspecified)
                {
                    return;
                }
                else if (currentEdge.controlKind == TrackAtcEdgeControlKind.Block)
                {
                    if (!TryResolveNextEdgeOnBlock(context, currentEdge, out var nextAtcEdge))
                    {
                        return;
                    }

                    if (nextAtcEdge == null)
                    {
                        return;
                    }

                    context.State.NextEdgeById[currentEdge.atcEdgeId] = nextAtcEdge.atcEdgeId;
                }
                else if (currentEdge.controlKind == TrackAtcEdgeControlKind.Interlocking)
                {

                }

                else
                {
                    // TODO: 構内運転は後で実装
                }




            }

        }

        public static bool TryResolveNextEdgeOnBlock(
            TrackAtcContext context,
            TrackAtcGraphEdge currentEdge,
            out TrackAtcGraphEdge nextAtcEdge
        )
        {
            nextAtcEdge = null;

            if (currentEdge.direction == TrackAtcTravelDirection.Unspecified)
                return false;

            string exitAtcNodeId = currentEdge.direction == TrackAtcTravelDirection.AtoB
                ? currentEdge.atcNodeBId
                : currentEdge.atcNodeAId;

            if (!context.Workspace.atcNodesById.TryGetValue(exitAtcNodeId, out var exitAtcNode) ||
                exitAtcNode.connectedAtcEdgeIds == null ||
                !exitAtcNode.connectedAtcEdgeIds.Contains(currentEdge.atcEdgeId))
                return false;

            int count = exitAtcNode.connectedAtcEdgeIds.Count;

            // 正常な行き止まり。
            if (count == 1) 
            {
                return true; 
            }
            if (count != 2 && count != 3)  
            {
                return false;
            }

            // 2本なら残りの1本、3本なら有効な連動進路の先頭を選ぶ。
            foreach (string edgeId in exitAtcNode.connectedAtcEdgeIds)
            {
                // 自分自身を選ばないようにする
                if (edgeId == currentEdge.atcEdgeId)
                { 
                    continue;
                }
                   
                if (!context.Workspace.atcEdgesById.TryGetValue(edgeId, out var candidate))
                {
                    return false;
                }
                    

                if (count == 3 && candidate.controlKind != TrackAtcEdgeControlKind.Interlocking)
                {
                    continue;
                }  

                if (!CanEnterFromBlock(context, candidate, exitAtcNodeId))
                {
                    continue;
                }

                // 複数の枝が有効なら、列挙順で選ばない。
                if (nextAtcEdge != null)
                {
                    nextAtcEdge = null;
                    return false;
                }

                nextAtcEdge = candidate;
            }

            // 経路を決めてから占有を照査する。同じ回路の占有は無視する。
            if (
                nextAtcEdge != null && 
                nextAtcEdge.trackCircuitId != currentEdge.trackCircuitId && (
                   !context.Input.OccupiedByCircuitId.TryGetValue(nextAtcEdge.trackCircuitId, out bool occupied) || 
                    occupied
                )
            )
            {
                nextAtcEdge = null;
            }
               

            return true;
        }

        private static bool CanEnterFromBlock(
            TrackAtcContext context,
            TrackAtcGraphEdge edge,
            string entryNodeId)
        {
            if (edge.controlKind == TrackAtcEdgeControlKind.Block)
            {
                return edge.direction == TrackAtcTravelDirection.AtoB && edge.atcNodeAId == entryNodeId ||
                    edge.direction == TrackAtcTravelDirection.BtoA && edge.atcNodeBId == entryNodeId;
            }
            if (edge.controlKind == TrackAtcEdgeControlKind.Yard)
            {
                // TODO: 後で構内運転を実装
                return false;
            }
                

            string exitNodeId;

            if (edge.atcNodeAId == entryNodeId)
            {
                exitNodeId = edge.atcNodeBId;
            }   
            else if (edge.atcNodeBId == entryNodeId)
            {
                exitNodeId = edge.atcNodeAId;
            }
                
            else
            {
                return false;
            }
                

            foreach (var route in context.Workspace.atcRoutesById.Values)
            {
                if (
                    route.atcEdgeIds.Count < 2 || 
                    route.atcEdgeIds[0] != edge.atcEdgeId ||
                    !context.Input.RoutesById.TryGetValue(route.interlockingRouteId, out var state) ||
                    !state.ProceedAllowed || state.CancelPending
                )
                {
                    continue;
                }
                    

                // 進路の2本目が退出側につながることを確認する。(逆向きの進路を許可しない)
                if (
                    context.Workspace.atcEdgesById.TryGetValue(route.atcEdgeIds[1], out var secondEdge) && (
                        secondEdge.atcNodeAId == exitNodeId 
                        || secondEdge.atcNodeBId == exitNodeId
                    )
                )
                {
                    return true;
                }
                    
            }

            return false;
        }
    }
}
