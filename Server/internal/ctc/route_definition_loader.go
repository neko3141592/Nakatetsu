package ctc

import (
	"fmt"
	"path/filepath"
	"strings"
)

// LoadTrackRouteDefinitionsは駅ごとの連動JSONから、進行方向順の進路定義を読み込む。
func LoadTrackRouteDefinitions(manifestPath string, circuits map[string]TrackCircuitDefinition) ([]TrackRouteDefinition, error) {
	path, err := filepath.Abs(manifestPath)
	if err != nil {
		return nil, err
	}
	var manifest struct {
		SchemaVersion int `json:"schemaVersion"`
		Stations      []struct {
			StationID string `json:"stationId"`
			File      string `json:"file"`
		} `json:"stations"`
	}
	if err := readDefinitionJSON(path, &manifest); err != nil {
		return nil, err
	}
	if manifest.SchemaVersion != 1 || len(manifest.Stations) == 0 {
		return nil, fmt.Errorf("invalid interlocking manifest: %s", path)
	}

	var definitions []TrackRouteDefinition
	stationIDs, files := make(map[string]bool), make(map[string]bool)
	for _, entry := range manifest.Stations {
		if strings.TrimSpace(entry.StationID) == "" || stationIDs[entry.StationID] || strings.TrimSpace(entry.File) == "" {
			return nil, fmt.Errorf("empty or duplicate station entry in %s: %q", path, entry.StationID)
		}
		stationIDs[entry.StationID] = true
		stationPath := entry.File
		if !filepath.IsAbs(stationPath) {
			stationPath = filepath.Join(filepath.Dir(path), stationPath)
		}
		stationPath = filepath.Clean(stationPath)
		if files[stationPath] {
			return nil, fmt.Errorf("duplicate station file: %s", stationPath)
		}
		files[stationPath] = true
		var station struct {
			InterlockingID string `json:"interlockingId"`
			Routes         []struct {
				RouteID                   string   `json:"routeId"`
				StartTrackCircuitID       string   `json:"startTrackCircuitId"`
				DestinationTrackCircuitID string   `json:"destinationTrackCircuitId"`
				TrackCircuitIDs           []string `json:"trackCircuitIds"`
				RouteClearTrackCircuitIDs []string `json:"routeClearTrackCircuitIds"`
			} `json:"routes"`
		}
		if err := readDefinitionJSON(stationPath, &station); err != nil {
			return nil, err
		}
		if station.InterlockingID != entry.StationID || len(station.Routes) == 0 {
			return nil, fmt.Errorf("mismatched interlockingId or missing routes in %s", stationPath)
		}
		routeIDs := make(map[string]bool)
		for _, route := range station.Routes {
			if strings.TrimSpace(route.RouteID) == "" || routeIDs[route.RouteID] {
				return nil, fmt.Errorf("empty or duplicate route ID %q in %s", route.RouteID, stationPath)
			}
			routeIDs[route.RouteID] = true
			ids := route.TrackCircuitIDs
			if len(ids) == 0 || ids[0] != route.StartTrackCircuitID || ids[len(ids)-1] != route.DestinationTrackCircuitID {
				return nil, fmt.Errorf("route %s/%s: trackCircuitIds must run from start to destination", entry.StationID, route.RouteID)
			}
			seen := make(map[string]bool)
			for _, id := range ids {
				if _, exists := circuits[id]; !exists || strings.TrimSpace(id) == "" || seen[id] {
					return nil, fmt.Errorf("route %s/%s: unknown or duplicate circuit %q", entry.StationID, route.RouteID, id)
				}
				seen[id] = true
			}
			for _, id := range route.RouteClearTrackCircuitIDs {
				if _, exists := circuits[id]; !exists || strings.TrimSpace(id) == "" {
					return nil, fmt.Errorf("route %s/%s: unknown route-clear circuit %q", entry.StationID, route.RouteID, id)
				}
			}
			definitions = append(definitions, TrackRouteDefinition{
				InterlockingStationID:     entry.StationID,
				InterlockingRouteID:       route.RouteID,
				TrackCircuitIDs:           ids,
				RouteClearTrackCircuitIDs: route.RouteClearTrackCircuitIDs,
			})
		}
	}
	return definitions, nil
}
