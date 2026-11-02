package timetable

import (
	"fmt"

	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
	"github.com/neko3141592/Nakatetsu/Server/internal/prc"
)

// assignRouteUsagesはDBの区間ごとの使用進路を、PRCの地点ごとの用途へ振り分ける。
// IDは元の区間の地点IDと順序を維持し、所属先の変更では採番し直さない。
func assignRouteUsages(plan *prc.TrainPlan, source []TimetablePoint, definitions map[ctc.RouteKey]ctc.TrackRouteDefinition) error {
	for i, point := range source {
		for _, route := range point.Routes {
			key := ctc.RouteKey{StationID: route.ControlStationID, RouteID: route.RouteID}
			definition, exists := definitions[key]
			if !exists {
				return fmt.Errorf("train %s point %d: unknown route %s/%s", plan.TrainNumber, point.TimetablePointID, key.StationID, key.RouteID)
			}
			usage := prc.RouteUsage{
				ID: prc.RouteUsageID{
					TimetablePointID:   point.TimetablePointID,
					RouteSequenceIndex: route.SequenceIndex,
				},
				TrainNumber:      plan.TrainNumber,
				ControlStationID: route.ControlStationID,
				RouteID:          route.RouteID,
			}

			switch definition.Kind {
			case ctc.RouteKindDeparture:
				index := findStationPoint(plan.Points, i, -1, key.StationID)
				if index < 0 {
					return fmt.Errorf("train %s point %d: departure route %s/%s has no preceding station point", plan.TrainNumber, point.TimetablePointID, key.StationID, key.RouteID)
				}
				plan.Points[index].DepartureRoutes = append(plan.Points[index].DepartureRoutes, usage)
			case ctc.RouteKindHome:
				index := findStationPoint(plan.Points, i+1, 1, key.StationID)
				if index < 0 {
					return fmt.Errorf("train %s point %d: home route %s/%s has no following station point", plan.TrainNumber, point.TimetablePointID, key.StationID, key.RouteID)
				}
				plan.Points[index].ArrivalRoutes = append(plan.Points[index].ArrivalRoutes, usage)
			case ctc.RouteKindShunting:
				plan.Points[i].ShuntingRoutes = append(plan.Points[i].ShuntingRoutes, usage)
			default:
				return fmt.Errorf("train %s point %d: route %s/%s has unknown kind %q", plan.TrainNumber, point.TimetablePointID, key.StationID, key.RouteID, definition.Kind)
			}
		}
	}
	return nil
}

// 駅以外の地点を挟んでも、ダイヤ順に最も近い同駅の地点へ対応付ける。
func findStationPoint(points []prc.PointPlan, start, step int, stationID string) int {
	for i := start; i >= 0 && i < len(points); i += step {
		if points[i].StationID != nil && *points[i].StationID == stationID {
			return i
		}
	}
	return -1
}

// validateRouteTimesは順序判定と挙上に必要な時刻を、運転開始前に確認する。
func validateRouteTimes(train prc.TrainPlan) error {
	for _, point := range train.Points {
		if len(point.ArrivalRoutes)+len(point.DepartureRoutes)+len(point.ShuntingRoutes) == 0 {
			continue
		}
		if point.Handling != prc.HandlingStop && point.Handling != prc.HandlingPass {
			return fmt.Errorf("train %s point %d: stop/pass handling is required", train.TrainNumber, point.TimetablePointID)
		}
		needsDeparture := len(point.DepartureRoutes)+len(point.ShuntingRoutes) > 0 || point.Handling == prc.HandlingPass
		if needsDeparture && point.DepartureSeconds == nil {
			return fmt.Errorf("train %s point %d: departure/passing time is required", train.TrainNumber, point.TimetablePointID)
		}
		if len(point.ArrivalRoutes) > 0 && point.Handling == prc.HandlingStop && point.ArrivalSeconds == nil {
			return fmt.Errorf("train %s point %d: arrival time is required", train.TrainNumber, point.TimetablePointID)
		}
		if len(point.ShuntingRoutes) > 0 && point.Handling != prc.HandlingStop {
			return fmt.Errorf("train %s point %d: shunting must start at a stop", train.TrainNumber, point.TimetablePointID)
		}
	}
	return nil
}
