package timetable

import (
	"context"
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"strings"

	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
	"gorm.io/gorm"
	"gorm.io/gorm/clause"
)

func SyncEquipmentMasters(ctx context.Context, db *gorm.DB, graphManifestPath string, circuits map[string]ctc.TrackCircuitDefinition, routes []ctc.TrackRouteDefinition) error {
	data, err := os.ReadFile(graphManifestPath)
	if err != nil {
		return err
	}
	var manifest struct {
		Stations []Station `json:"stations"`
		Files    []string  `json:"files"`
	}
	if err := json.Unmarshal(data, &manifest); err != nil {
		return err
	}
	if len(manifest.Stations) == 0 || len(circuits) == 0 {
		return fmt.Errorf("station or track circuit definitions are empty")
	}
	stationIDs := make(map[string]bool)
	for _, station := range manifest.Stations {
		if strings.TrimSpace(station.StationID) == "" || strings.TrimSpace(station.DisplayName) == "" || stationIDs[station.StationID] {
			return fmt.Errorf("invalid or duplicate station: %q", station.StationID)
		}
		stationIDs[station.StationID] = true
	}
	var tracks []StationTrack
	trackIDs := make(map[[2]string]bool)
	for _, file := range manifest.Files {
		path := file
		if !filepath.IsAbs(path) {
			path = filepath.Join(filepath.Dir(graphManifestPath), path)
		}
		data, err := os.ReadFile(path)
		if err != nil {
			return err
		}
		var fragment struct {
			StationID     string         `json:"stationId"`
			StationTracks []StationTrack `json:"stationTracks"`
		}
		if err := json.Unmarshal(data, &fragment); err != nil {
			return fmt.Errorf("decode %s: %w", path, err)
		}
		for _, track := range fragment.StationTracks {
			key := [2]string{fragment.StationID, track.TrackID}
			if !stationIDs[fragment.StationID] || strings.TrimSpace(track.TrackID) == "" || strings.TrimSpace(track.DisplayName) == "" || trackIDs[key] {
				return fmt.Errorf("invalid or duplicate station track %s/%s in %s", fragment.StationID, track.TrackID, path)
			}
			if _, ok := circuits[track.TrackCircuitID]; !ok {
				return fmt.Errorf("station track %s/%s references unknown circuit %q", fragment.StationID, track.TrackID, track.TrackCircuitID)
			}
			track.StationID = fragment.StationID
			trackIDs[key] = true
			tracks = append(tracks, track)
		}
	}
	circuitRows := make([]TrackCircuit, 0, len(circuits))
	for id, circuit := range circuits {
		circuitRows = append(circuitRows, TrackCircuit{TrackCircuitID: id, DisplayName: circuit.DisplayName})
	}
	routeRows := make([]Route, 0, len(routes))
	for _, route := range routes {
		if !stationIDs[route.InterlockingStationID] {
			return fmt.Errorf("route %s/%s references an unknown station", route.InterlockingStationID, route.InterlockingRouteID)
		}
		routeRows = append(routeRows, Route{ControlStationID: route.InterlockingStationID, RouteID: route.InterlockingRouteID})
	}
	return db.WithContext(ctx).Transaction(func(tx *gorm.DB) error {
		upsert := clause.OnConflict{UpdateAll: true}
		if err := tx.Clauses(upsert).Create(&manifest.Stations).Error; err != nil {
			return err
		}
		if err := tx.Clauses(upsert).Create(&circuitRows).Error; err != nil {
			return err
		}
		if len(tracks) > 0 {
			if err := tx.Omit(clause.Associations).Clauses(upsert).Create(&tracks).Error; err != nil {
				return err
			}
		}
		if len(routeRows) == 0 {
			return nil
		}
		return tx.Clauses(upsert).Create(&routeRows).Error
	})
}
