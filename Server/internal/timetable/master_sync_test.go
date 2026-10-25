package timetable_test

import (
	"context"
	"encoding/json"
	"os"
	"path/filepath"
	"testing"

	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
	"github.com/neko3141592/Nakatetsu/Server/internal/timetable"
)

func writeMasterStations(t *testing.T, path string, stations []timetable.Station) {
	t.Helper()
	data, err := json.Marshal(map[string]any{"stations": stations})
	if err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(path, data, 0600); err != nil {
		t.Fatal(err)
	}
}

func TestSyncEquipmentMasters(t *testing.T) {
	db := openDatabase(t, filepath.Join(t.TempDir(), "masters.db"))
	path := filepath.Join(t.TempDir(), "graph.json")
	writeMasterStations(t, path, []timetable.Station{{StationID: "S1", DisplayName: "旧駅名"}, {StationID: "S2", DisplayName: "保持する駅"}})
	circuits := map[string]ctc.TrackCircuitDefinition{"A": {DisplayName: "旧番線名"}, "B": {DisplayName: "保持する回路"}}
	routes := []ctc.TrackRouteDefinition{
		{InterlockingStationID: "S1", InterlockingRouteID: "R"},
		{InterlockingStationID: "S2", InterlockingRouteID: "R"},
	}
	for range 2 {
		if err := timetable.SyncEquipmentMasters(context.Background(), db, path, circuits, routes); err != nil {
			t.Fatal(err)
		}
	}
	create(t, db, &timetable.TrainType{TrainTypeID: "local", DisplayName: "普通"})
	train := timetable.TimetableTrain{
		TrainNumber: "0001", TrainTypeID: "local",
		Points: []timetable.TimetablePoint{{StationID: stringPointer("S1"), TrackCircuitID: "A", Routes: []timetable.TimetablePointRoute{{ControlStationID: "S1", RouteID: "R"}}}},
	}
	create(t, db, &train)
	writeMasterStations(t, path, []timetable.Station{{StationID: "S1", DisplayName: "新駅名"}, {StationID: "S3", DisplayName: "追加駅"}})
	circuits = map[string]ctc.TrackCircuitDefinition{"A": {DisplayName: "新番線名"}, "C": {DisplayName: "追加回路"}}
	routes = []ctc.TrackRouteDefinition{
		{InterlockingStationID: "S1", InterlockingRouteID: "R"},
		{InterlockingStationID: "S3", InterlockingRouteID: "R"},
	}
	for range 2 {
		if err := timetable.SyncEquipmentMasters(context.Background(), db, path, circuits, routes); err != nil {
			t.Fatal(err)
		}
	}
	count(t, db, &timetable.Station{}, 3)
	count(t, db, &timetable.TrackCircuit{}, 3)
	count(t, db, &timetable.Route{}, 3)
	count(t, db, &timetable.TimetablePoint{}, 1)
	count(t, db, &timetable.TimetablePointRoute{}, 1)
	var station timetable.Station
	var circuit timetable.TrackCircuit
	if err := db.First(&station, "station_id = ?", "S1").Error; err != nil || station.DisplayName != "新駅名" {
		t.Fatalf("station name was not updated: %+v, error=%v", station, err)
	}
	if err := db.First(&circuit, "track_circuit_id = ?", "A").Error; err != nil || circuit.DisplayName != "新番線名" {
		t.Fatalf("circuit name was not updated: %+v, error=%v", circuit, err)
	}
	if err := db.First(&timetable.Route{}, "control_station_id = ? AND route_id = ?", "S2", "R").Error; err != nil {
		t.Fatalf("removed JSON route was deleted from DB: %v", err)
	}
	var point timetable.TimetablePoint
	if err := db.First(&point, train.Points[0].TimetablePointID).Error; err != nil || point.TrackCircuitID != "A" || point.StationID == nil || *point.StationID != "S1" {
		t.Fatalf("timetable references changed: %+v, error=%v", point, err)
	}
}

func TestSyncEquipmentMastersRollback(t *testing.T) {
	db := openDatabase(t, filepath.Join(t.TempDir(), "masters.db"))
	path := filepath.Join(t.TempDir(), "graph.json")
	writeMasterStations(t, path, []timetable.Station{{StationID: "S1", DisplayName: "変更前"}})
	circuits := map[string]ctc.TrackCircuitDefinition{"A": {DisplayName: "変更前"}}
	routes := []ctc.TrackRouteDefinition{{InterlockingStationID: "S1", InterlockingRouteID: "R"}}
	if err := timetable.SyncEquipmentMasters(context.Background(), db, path, circuits, routes); err != nil {
		t.Fatal(err)
	}
	writeMasterStations(t, path, []timetable.Station{{StationID: "S1", DisplayName: "変更後"}, {StationID: "S2", DisplayName: "追加"}})
	circuits = map[string]ctc.TrackCircuitDefinition{"A": {DisplayName: "変更後"}, "B": {DisplayName: "追加"}}
	invalidRoutes := []ctc.TrackRouteDefinition{{InterlockingStationID: "S1", InterlockingRouteID: " "}}
	if err := timetable.SyncEquipmentMasters(context.Background(), db, path, circuits, invalidRoutes); err == nil {
		t.Fatal("invalid route was accepted")
	}
	ctx, cancel := context.WithCancel(context.Background())
	cancel()
	if err := timetable.SyncEquipmentMasters(ctx, db, path, circuits, routes); err == nil {
		t.Fatal("canceled synchronization was accepted")
	}
	count(t, db, &timetable.Station{}, 1)
	count(t, db, &timetable.TrackCircuit{}, 1)
	count(t, db, &timetable.Route{}, 1)
	var station timetable.Station
	var circuit timetable.TrackCircuit
	if err := db.First(&station).Error; err != nil || station.DisplayName != "変更前" {
		t.Fatalf("station update was not rolled back: %+v, error=%v", station, err)
	}
	if err := db.First(&circuit).Error; err != nil || circuit.DisplayName != "変更前" {
		t.Fatalf("circuit update was not rolled back: %+v, error=%v", circuit, err)
	}
}

func TestSyncEquipmentMastersRejectsInvalidDefinitions(t *testing.T) {
	db := openDatabase(t, filepath.Join(t.TempDir(), "masters.db"))
	path := filepath.Join(t.TempDir(), "graph.json")
	circuits := map[string]ctc.TrackCircuitDefinition{"A": {}}
	for _, body := range []string{
		`{`, `null`, `{}`, `{"stations":[]}`,
		`{"stations":[{"stationId":" ","displayName":"駅"}]}`,
		`{"stations":[{"stationId":"S1","displayName":" "}]}`,
		`{"stations":[{"stationId":"S1","displayName":"駅"},{"stationId":"S1","displayName":"重複"}]}`,
	} {
		if err := os.WriteFile(path, []byte(body), 0600); err != nil {
			t.Fatal(err)
		}
		if err := timetable.SyncEquipmentMasters(context.Background(), db, path, circuits, nil); err == nil {
			t.Fatalf("invalid manifest was accepted: %s", body)
		}
	}
	writeMasterStations(t, path, []timetable.Station{{StationID: "S1", DisplayName: "駅"}})
	if err := timetable.SyncEquipmentMasters(context.Background(), db, path, nil, nil); err == nil {
		t.Fatal("empty circuit definitions were accepted")
	}
	if err := timetable.SyncEquipmentMasters(context.Background(), db, path, circuits, []ctc.TrackRouteDefinition{{InterlockingStationID: "missing", InterlockingRouteID: "R"}}); err == nil {
		t.Fatal("unknown station was accepted")
	}
	if err := timetable.SyncEquipmentMasters(context.Background(), db, path+".missing", circuits, nil); err == nil {
		t.Fatal("missing manifest was accepted")
	}
	count(t, db, &timetable.Station{}, 0)
	count(t, db, &timetable.TrackCircuit{}, 0)
	count(t, db, &timetable.Route{}, 0)
}

func TestSyncNakatetsuEquipmentMasters(t *testing.T) {
	db := openDatabase(t, filepath.Join(t.TempDir(), "masters.db"))
	root := filepath.Join("..", "..", "..", "SourceAssets", "Track", "Network")
	path := filepath.Join(root, "NakatetsuTrackGraph", "graph.json")
	circuits, err := ctc.LoadTrackCircuitDefinitions(path)
	if err != nil {
		t.Fatal(err)
	}
	interlocking, err := ctc.LoadInterlockingDefinitions(filepath.Join(root, "NakatetsuInterlocking", "interlocking.json"), circuits)
	if err != nil {
		t.Fatal(err)
	}
	for range 2 {
		if err := timetable.SyncEquipmentMasters(context.Background(), db, path, circuits, interlocking.Routes); err != nil {
			t.Fatal(err)
		}
	}
	count(t, db, &timetable.Station{}, 11)
	count(t, db, &timetable.StationTrack{}, 40)
	count(t, db, &timetable.TrackCircuit{}, int64(len(circuits)))
	count(t, db, &timetable.Route{}, int64(len(interlocking.Routes)))
	var station timetable.Station
	for _, expected := range []timetable.StationTrack{
		{StationID: "NT01", TrackID: "1", DisplayName: "1番線", TrackCircuitID: "NT01_1RT"},
		{StationID: "NT02", TrackID: "1", DisplayName: "1番線", TrackCircuitID: "NT02_DT"},
		{StationID: "NT07", TrackID: "DEPOT_1", DisplayName: "電留1線", TrackCircuitID: "NT07_DEPOT_1T"},
		{StationID: "NT11", TrackID: "4_STUB", DisplayName: "4引線", TrackCircuitID: "NT11_4_STUB_T"},
	} {
		var track timetable.StationTrack
		if err := db.First(&track, "station_id = ? AND track_id = ?", expected.StationID, expected.TrackID).Error; err != nil || track != expected {
			t.Fatalf("unexpected station track: %+v, error=%v", track, err)
		}
	}
	if err := db.First(&station, "station_id = ?", "NT11").Error; err != nil || station.DisplayName != "猿沼" {
		t.Fatalf("unexpected station: %+v, error=%v", station, err)
	}
	var circuit timetable.TrackCircuit
	if err := db.First(&circuit, "track_circuit_id = ?", "NT01_1RT").Error; err != nil || circuit.DisplayName != circuits["NT01_1RT"].DisplayName {
		t.Fatalf("unexpected circuit: %+v, error=%v", circuit, err)
	}
	create(t, db, &timetable.TrainType{TrainTypeID: "local", DisplayName: "普通"})
	create(t, db, &timetable.TimetableTrain{TrainNumber: "0001", TrainTypeID: "local", Points: []timetable.TimetablePoint{{
		StationID: stringPointer("NT01"), TrackCircuitID: "NT01_1RT",
		Routes: []timetable.TimetablePointRoute{{ControlStationID: "NT01", RouteID: "1R"}},
	}}})
}

func TestSyncStationTracks(t *testing.T) {
	db := openDatabase(t, filepath.Join(t.TempDir(), "masters.db"))
	path := filepath.Join(t.TempDir(), "graph.json")
	manifest := map[string]any{
		"stations": []timetable.Station{{StationID: "S1", DisplayName: "駅1"}, {StationID: "S2", DisplayName: "駅2"}},
		"files":    []string{"station.json"},
	}
	data, err := json.Marshal(manifest)
	if err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(path, data, 0600); err != nil {
		t.Fatal(err)
	}
	write := func(stationID string, tracks []timetable.StationTrack) {
		t.Helper()
		data, err := json.Marshal(map[string]any{"stationId": stationID, "stationTracks": tracks})
		if err != nil {
			t.Fatal(err)
		}
		if err := os.WriteFile(filepath.Join(filepath.Dir(path), "station.json"), data, 0600); err != nil {
			t.Fatal(err)
		}
	}
	circuits := map[string]ctc.TrackCircuitDefinition{"A": {}, "B": {}}
	track := timetable.StationTrack{TrackID: "1", DisplayName: "1番線", TrackCircuitID: "A"}
	for _, stationID := range []string{"S1", "S2"} {
		write(stationID, []timetable.StationTrack{track})
		for range 2 {
			if err := timetable.SyncEquipmentMasters(context.Background(), db, path, circuits, nil); err != nil {
				t.Fatal(err)
			}
		}
	}
	count(t, db, &timetable.StationTrack{}, 2)
	track.DisplayName, track.TrackCircuitID = "変更後", "B"
	write("S1", []timetable.StationTrack{track})
	invalidRoutes := []ctc.TrackRouteDefinition{{InterlockingStationID: "S1", InterlockingRouteID: " "}}
	if err := timetable.SyncEquipmentMasters(context.Background(), db, path, circuits, invalidRoutes); err == nil {
		t.Fatal("invalid route did not roll back station track changes")
	}
	var saved timetable.StationTrack
	if err := db.First(&saved, "station_id = ? AND track_id = ?", "S1", "1").Error; err != nil || saved.DisplayName != "1番線" || saved.TrackCircuitID != "A" {
		t.Fatalf("station track update was not rolled back: %+v, error=%v", saved, err)
	}
	if err := timetable.SyncEquipmentMasters(context.Background(), db, path, circuits, nil); err != nil {
		t.Fatal(err)
	}
	var updated timetable.StationTrack
	if err := db.First(&updated, "station_id = ? AND track_id = ?", "S1", "1").Error; err != nil || updated.DisplayName != "変更後" || updated.TrackCircuitID != "B" {
		t.Fatalf("station track was not updated: %+v, error=%v", updated, err)
	}
	for _, invalid := range []timetable.StationTrack{
		{TrackID: " ", DisplayName: "番線", TrackCircuitID: "A"},
		{TrackID: "1", DisplayName: " ", TrackCircuitID: "A"},
		{TrackID: "1", DisplayName: "番線", TrackCircuitID: "missing"},
	} {
		write("S1", []timetable.StationTrack{invalid})
		if err := timetable.SyncEquipmentMasters(context.Background(), db, path, circuits, nil); err == nil {
			t.Fatalf("invalid station track accepted: %+v", invalid)
		}
	}
	for _, stationID := range []string{"missing", ""} {
		write(stationID, []timetable.StationTrack{track})
		if err := timetable.SyncEquipmentMasters(context.Background(), db, path, circuits, nil); err == nil {
			t.Fatal("unknown station was accepted")
		}
	}
	write("S1", []timetable.StationTrack{track, track})
	if err := timetable.SyncEquipmentMasters(context.Background(), db, path, circuits, nil); err == nil {
		t.Fatal("duplicate station track was accepted")
	}
	for _, invalid := range []timetable.StationTrack{
		{StationID: "missing", TrackID: "1", DisplayName: "番線", TrackCircuitID: "A"},
		{StationID: "S1", TrackID: "2", DisplayName: "番線", TrackCircuitID: "missing"},
		{StationID: "S1", TrackID: "1", DisplayName: "番線", TrackCircuitID: "A"},
	} {
		if err := db.Create(&invalid).Error; err == nil {
			t.Fatalf("DB accepted an invalid station track: %+v", invalid)
		}
	}
	count(t, db, &timetable.StationTrack{}, 2)
}
