package prc

import "github.com/neko3141592/Nakatetsu/Server/internal/ctc"

// buildConflictPointsは2進路以上で共有する回路と、その進路の集合を返す。
// 設備定義は検証済みとし、進路は駅IDと進路IDの組で区別する。
func buildConflictPoints(routes []ctc.TrackRouteDefinition) map[string]map[ctc.RouteKey]struct{} {
	points := make(map[string]map[ctc.RouteKey]struct{})
	for _, route := range routes {
		key := ctc.RouteKey{StationID: route.InterlockingStationID, RouteID: route.InterlockingRouteID}
		for _, circuitID := range route.RouteClearTrackCircuitIDs {
			if points[circuitID] == nil {
				points[circuitID] = make(map[ctc.RouteKey]struct{})
			}
			points[circuitID][key] = struct{}{}
		}
	}
	for circuitID, routeSet := range points {
		if len(routeSet) < 2 {
			delete(points, circuitID)
		}
	}
	return points
}
