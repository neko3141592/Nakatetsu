package prc

import (
	"cmp"
	"fmt"
	"slices"

	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
)

// BuildConflictPointsは2進路以上で共有する回路と、その進路の集合を返す。
// 設備定義は検証済みとし、進路は駅IDと進路IDの組で区別する。
func BuildConflictPoints(routes []ctc.TrackRouteDefinition) map[string]map[ctc.RouteKey]struct{} {
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

// buildConflictPointsByRouteは競合点 -> 進路の対応を逆引きにする。
func buildConflictPointsByRoute(points map[string]map[ctc.RouteKey]struct{}) map[ctc.RouteKey][]string {
	result := make(map[ctc.RouteKey][]string)
	for circuitID, routes := range points {
		for route := range routes {
			result[route] = append(result[route], circuitID)
		}
	}
	return result
}

// BuildConflictUsagesは挙上状態に関係なく、ダイヤ全体から競合点の使用予定を作る。
func BuildConflictUsages(plan TimetablePlan, points map[string]map[ctc.RouteKey]struct{}) (map[string][]ConflictUsage, error) {
	result := make(map[string][]ConflictUsage, len(points))
	for circuitID, routeSet := range points {
		var usages []ConflictUsage
		add := func(routes []RouteUsage, seconds *int) error {
			for _, route := range routes {
				key := ctc.RouteKey{StationID: route.ControlStationID, RouteID: route.RouteID}
				if _, exists := routeSet[key]; !exists {
					continue
				}
				if seconds == nil {
					return fmt.Errorf("conflict point %s: train %s route %s/%s has no scheduled time", circuitID, route.TrainNumber, key.StationID, key.RouteID)
				}
				usages = append(usages, ConflictUsage{Usage: route, ScheduledSeconds: *seconds})
			}
			return nil
		}
		for _, train := range plan.TrainsByNumber {
			for _, point := range train.Points {
				// 場内は停車なら着時刻、通過なら発時刻を使う。
				arrival := point.ArrivalSeconds
				if point.Handling == HandlingPass {
					arrival = point.DepartureSeconds
				}
				if err := add(point.ArrivalRoutes, arrival); err != nil {
					return nil, err
				}
				if err := add(point.DepartureRoutes, point.DepartureSeconds); err != nil {
					return nil, err
				}
				if err := add(point.ShuntingRoutes, point.DepartureSeconds); err != nil {
					return nil, err
				}
			}
		}
		slices.SortFunc(usages, func(a, b ConflictUsage) int {
			if order := cmp.Compare(a.ScheduledSeconds, b.ScheduledSeconds); order != 0 {
				return order
			}
			// 初版の同時刻は、地点IDと区間内順序で優先順を固定する。
			if order := cmp.Compare(a.Usage.ID.TimetablePointID, b.Usage.ID.TimetablePointID); order != 0 {
				return order
			}
			return cmp.Compare(a.Usage.ID.RouteSequenceIndex, b.Usage.ID.RouteSequenceIndex)
		})
		result[circuitID] = usages
	}
	return result, nil
}
