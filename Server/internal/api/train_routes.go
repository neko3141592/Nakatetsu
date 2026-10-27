package api

import (
	"errors"
	"fmt"
	"slices"

	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
	"github.com/neko3141592/Nakatetsu/Server/internal/timetable"
)

var errTrainRoutes = errors.New("使用進路を生成できません")

func (s *ApiServer) generateTrainRoutes(points []timetable.TimetablePoint) error {
	slices.SortFunc(points, func(a, b timetable.TimetablePoint) int {
		return a.SequenceIndex - b.SequenceIndex
	})
	for i := range points {
		if _, ok := s.CircuitDefinitions[points[i].TrackCircuitID]; !ok {
			return fmt.Errorf("%w: 地点%dの軌道回路が存在しません: %s", errTrainRoutes, i+1, points[i].TrackCircuitID)
		}
		points[i].Routes = []timetable.TimetablePointRoute{}
	}
	for i := 0; i+1 < len(points); i++ {
		from, to := points[i].TrackCircuitID, points[i+1].TrackCircuitID
		kinds := []ctc.RouteKind{ctc.RouteKindHome, ctc.RouteKindDeparture}
		if points[i].Section == timetable.PointSectionDepotExit || points[i].Section == timetable.PointSectionDepotEntry ||
			points[i+1].Section == timetable.PointSectionDepotExit || points[i+1].Section == timetable.PointSectionDepotEntry {
			kinds = []ctc.RouteKind{ctc.RouteKindShunting}
		}
		candidates, err := timetable.FindRouteCandidates(from, to, s.CircuitDefinitions, s.RouteDefinitions, kinds...)
		if err != nil {
			return fmt.Errorf("%w: 地点%d（%s）→地点%d（%s）: %v", errTrainRoutes, i+1, from, i+2, to, err)
		}
		if len(candidates) != 1 {
			return fmt.Errorf("%w: 地点%d（%s）→地点%d（%s）の候補が複数あります", errTrainRoutes, i+1, from, i+2, to)
		}
		for j, route := range candidates[0] {
			points[i].Routes = append(points[i].Routes, timetable.TimetablePointRoute{
				SequenceIndex: j, ControlStationID: route.StationID, RouteID: route.RouteID,
			})
		}
	}
	return nil
}
