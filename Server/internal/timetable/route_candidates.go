package timetable

import (
	"errors"
	"fmt"
	"slices"

	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
)

func FindRouteCandidates(
	fromID, toID string,
	circuits map[string]ctc.TrackCircuitDefinition,
	routes []ctc.TrackRouteDefinition,
	kinds ...ctc.RouteKind,
) ([][]ctc.RouteKey, error) {
	visited := make(map[string]bool)
	candidates := [][]ctc.RouteKey{}
	if _, ok := circuits[toID]; !ok {
		return nil, fmt.Errorf("軌道回路が存在しません: %s", toID)
	}

	err := search(
		fromID,
		toID,
		visited,
		[]ctc.RouteKey{},
		&candidates,
		circuits,
		routes,
		kinds,
	)

	if err != nil {
		return nil, err
	}

	if len(candidates) == 0 {
		return nil, errors.New("使用進路の候補が見つかりません")
	}
	return candidates, nil
}

func search(
	currentTrackCircuitId string,
	destinationTrackCircuitId string,
	visited map[string]bool,
	usedRoutes []ctc.RouteKey,
	candidates *[][]ctc.RouteKey,

	// マスター
	circuits map[string]ctc.TrackCircuitDefinition,
	routes []ctc.TrackRouteDefinition,
	kinds []ctc.RouteKind,
) error {
	currentTrackCircuit, ok := circuits[currentTrackCircuitId]
	if !ok {
		return fmt.Errorf("軌道回路が存在しません: %s", currentTrackCircuitId)
	}

	if visited[currentTrackCircuitId] {
		return nil
	}

	if currentTrackCircuitId == destinationTrackCircuitId {
		*candidates = append(*candidates, append([]ctc.RouteKey{}, usedRoutes...))
		return nil
	}

	visited[currentTrackCircuitId] = true
	defer delete(visited, currentTrackCircuitId)

	switch currentTrackCircuit.ControlKind {
	case ctc.TrackControlBlock, ctc.TrackControlInterlocking:
		// 場内進路は、駅手前の閉塞回路を発点にする場合もある。
		// 定義済み進路を優先し、進路がない場所だけ固定接続で進む。
		hasRoutes := false
		for _, route := range routes {
			ids := route.TrackCircuitIDs
			if len(ids) < 2 || ids[0] != currentTrackCircuitId {
				continue
			}
			hasRoutes = true
			if len(kinds) > 0 && !slices.Contains(kinds, route.Kind) {
				continue
			}
			blocked := false
			for _, id := range ids[1:] {
				if _, ok := circuits[id]; !ok {
					return fmt.Errorf("軌道回路が存在しません: %s", id)
				}
				if visited[id] {
					blocked = true
					break
				}
			}
			if blocked {
				continue
			}

			middle := ids[1 : len(ids)-1]
			for _, id := range middle {
				visited[id] = true
			}
			key := ctc.RouteKey{
				StationID: route.InterlockingStationID,
				RouteID:   route.InterlockingRouteID,
			}

			err := search(ids[len(ids)-1], destinationTrackCircuitId, visited, append(usedRoutes, key), candidates, circuits, routes, kinds)
			for _, id := range middle {
				delete(visited, id)
			}
			if err != nil {
				return err
			}
		}
		if !hasRoutes && currentTrackCircuit.ToCircuitID != "" {
			return search(currentTrackCircuit.ToCircuitID, destinationTrackCircuitId, visited, usedRoutes, candidates, circuits, routes, kinds)
		}
	}

	return nil
}
