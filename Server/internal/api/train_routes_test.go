package api

import (
	"encoding/json"
	"net/http"
	"path/filepath"
	"reflect"
	"strings"
	"testing"

	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
	"github.com/neko3141592/Nakatetsu/Server/internal/timetable"
)

func TestTrainGeneratedRoutes(t *testing.T) {
	db, handler := trainTestServer(t)
	var input timetable.TimetableTrain
	if err := json.Unmarshal([]byte(trainBody), &input); err != nil {
		t.Fatal(err)
	}
	for i := range input.Points {
		input.Points[i].Routes = []timetable.TimetablePointRoute{{ControlStationID: "injected", RouteID: "not-a-route"}}
	}
	body, err := json.Marshal(input)
	if err != nil {
		t.Fatal(err)
	}
	trainTypeRequest(t, handler, http.MethodPost, "/api/trains", string(body), http.StatusCreated)
	before := getTrain(t, handler, "0001")
	if len(before.Points[0].Routes) != 2 || before.Points[0].Routes[0].RouteID != "R1" ||
		before.Points[0].Routes[1].RouteID != "R2" || len(before.Points[1].Routes) != 0 {
		t.Fatalf("routes were not generated in order: %+v", before.Points)
	}
	for _, points := range []string{
		`[{"sequenceIndex":0,"trackCircuitId":"B1T"},{"sequenceIndex":1,"trackCircuitId":"A1T"}]`,
		`[{"sequenceIndex":0,"trackCircuitId":"A1T"},{"sequenceIndex":1,"trackCircuitId":"B1T"},{"sequenceIndex":2,"trackCircuitId":"A1T"}]`,
	} {
		request := `{"timetableId":"test","trainNumber":"bad","trainTypeId":"local","originStationId":"A","destinationStationId":"B","points":` + points + `}`
		w := trainTypeRequest(t, handler, http.MethodPost, "/api/trains", request, http.StatusBadRequest)
		if !strings.Contains(w.Body.String(), "B1T") || !strings.Contains(w.Body.String(), "A1T") {
			t.Fatalf("missing failing section: %s", w.Body.String())
		}
		trainTypeRequest(t, handler, http.MethodGet, "/api/trains/"+trainID(t, handler, "bad"), "", http.StatusNotFound)
		trainTypeRequest(t, handler, http.MethodPatch, "/api/trains/"+trainID(t, handler, "0001"), `{"trainTypeId":"express","points":`+points+`}`, http.StatusBadRequest)
		if after := getTrain(t, handler, "0001"); !reflect.DeepEqual(after, before) {
			t.Fatal("failed route generation modified the saved train")
		}
	}
	for table, want := range map[string]int64{"timetable_trains": 1, "timetable_points": 2, "timetable_point_routes": 2} {
		var count int64
		if err := db.Table(table).Count(&count).Error; err != nil || count != want {
			t.Fatalf("%s count=%d, err=%v", table, count, err)
		}
	}
}

func TestTrainBlockOnlyAndAmbiguousRoutes(t *testing.T) {
	db, _ := trainTestServer(t)
	server := &ApiServer{DB: db, CircuitDefinitions: map[string]ctc.TrackCircuitDefinition{
		"A1T": {ControlKind: ctc.TrackControlBlock, ToCircuitID: "B1T"},
		"B1T": {ControlKind: ctc.TrackControlBlock},
	}}
	handler := server.Routes()
	trainTypeRequest(t, handler, http.MethodPost, "/api/trains", trainBody, http.StatusCreated)
	before := getTrain(t, handler, "0001")
	if len(before.Points) != 2 || len(before.Points[0].Routes) != 0 || len(before.Points[1].Routes) != 0 {
		t.Fatalf("block-only path should have no interlocking routes: %+v", before.Points)
	}
	server.CircuitDefinitions["A1T"] = ctc.TrackCircuitDefinition{ControlKind: ctc.TrackControlInterlocking}
	server.RouteDefinitions = []ctc.TrackRouteDefinition{
		{InterlockingStationID: "A", InterlockingRouteID: "R1", Kind: ctc.RouteKindDeparture, TrackCircuitIDs: []string{"A1T", "B1T"}},
		{InterlockingStationID: "A", InterlockingRouteID: "R2", Kind: ctc.RouteKindHome, TrackCircuitIDs: []string{"A1T", "B1T"}},
	}
	w := trainTypeRequest(t, handler, http.MethodPatch, "/api/trains/"+trainID(t, handler, "0001"), `{"points":[{"sequenceIndex":0,"trackCircuitId":"A1T"},{"sequenceIndex":1,"trackCircuitId":"B1T"}]}`, http.StatusBadRequest)
	if !strings.Contains(w.Body.String(), "複数") {
		t.Fatalf("unexpected ambiguity error: %s", w.Body.String())
	}
	if after := getTrain(t, handler, "0001"); !reflect.DeepEqual(after, before) {
		t.Fatal("ambiguous path modified the train")
	}
}

func TestTrainPointSections(t *testing.T) {
	_, handler := trainTestServer(t)
	trainTypeRequest(t, handler, http.MethodPost, "/api/trains", `{"timetableId":"test","trainNumber":"sections","trainTypeId":"local","originStationId":"A","destinationStationId":"B","points":[
		{"sequenceIndex":0,"section":"depot_exit","stationId":"A","trackCircuitId":"A1T"},
		{"sequenceIndex":1,"section":"service","stationId":"A","trackCircuitId":"A1T"},
		{"sequenceIndex":2,"section":"service","stationId":"B","trackCircuitId":"B1T"},
		{"sequenceIndex":3,"section":"depot_entry","stationId":"B","trackCircuitId":"B1T"}]}`, http.StatusCreated)
	before := getTrain(t, handler, "sections")
	want := []timetable.PointSection{timetable.PointSectionDepotExit, timetable.PointSectionService, timetable.PointSectionService, timetable.PointSectionDepotEntry}
	if len(before.Points) != len(want) {
		t.Fatalf("unexpected points: %+v", before.Points)
	}
	for i, point := range before.Points {
		if point.Section != want[i] || point.SequenceIndex != i {
			t.Fatalf("unexpected section order: %+v", before.Points)
		}
	}
	if len(before.Points[1].Routes) != 2 || len(before.Points[3].Routes) != 0 {
		t.Fatal("missing generated routes across sections")
	}
	trainTypeRequest(t, handler, http.MethodPatch, "/api/trains/"+trainID(t, handler, "sections"), `{"points":[{"section":"invalid","trackCircuitId":"A1T"}]}`, http.StatusBadRequest)
	if after := getTrain(t, handler, "sections"); !reflect.DeepEqual(after, before) {
		t.Fatal("invalid section modified the train")
	}
}

func TestTrainRouteKindsBySection(t *testing.T) {
	db, _ := trainTestServer(t)
	server := &ApiServer{DB: db, CircuitDefinitions: map[string]ctc.TrackCircuitDefinition{
		"A1T": {ControlKind: ctc.TrackControlInterlocking},
		"B1T": {ControlKind: ctc.TrackControlBlock},
	}}
	routes := []ctc.TrackRouteDefinition{
		{InterlockingStationID: "A", InterlockingRouteID: "R1", Kind: ctc.RouteKindHome, TrackCircuitIDs: []string{"A1T", "B1T"}},
		{InterlockingStationID: "A", InterlockingRouteID: "R2", Kind: ctc.RouteKindShunting, TrackCircuitIDs: []string{"A1T", "B1T"}},
	}
	handler := server.Routes()
	for _, tt := range []struct {
		name     string
		from, to timetable.PointSection
		want     int
	}{
		{"default", "", "", 0},
		{"service", timetable.PointSectionService, timetable.PointSectionService, 0},
		{"exit", timetable.PointSectionDepotExit, timetable.PointSectionDepotExit, 1},
		{"exit_to_service", timetable.PointSectionDepotExit, timetable.PointSectionService, 1},
		{"service_to_entry", timetable.PointSectionService, timetable.PointSectionDepotEntry, 1},
		{"entry", timetable.PointSectionDepotEntry, timetable.PointSectionDepotEntry, 1},
	} {
		t.Run(tt.name, func(t *testing.T) {
			server.RouteDefinitions = routes
			input := timetable.TimetableTrain{TimetableID: "test", TrainNumber: tt.name, TrainTypeID: "local", OriginStationID: "A", DestinationStationID: "B", Points: []timetable.TimetablePoint{
				{SequenceIndex: 0, Section: tt.from, TrackCircuitID: "A1T"},
				{SequenceIndex: 1, Section: tt.to, TrackCircuitID: "B1T"},
			}}
			body, err := json.Marshal(input)
			if err != nil {
				t.Fatal(err)
			}
			trainTypeRequest(t, handler, http.MethodPost, "/api/trains", string(body), http.StatusCreated)
			before := getTrain(t, handler, tt.name)
			if len(before.Points[0].Routes) != 1 || before.Points[0].Routes[0].RouteID != routes[tt.want].InterlockingRouteID {
				t.Fatalf("unexpected routes: %+v", before.Points)
			}
			server.RouteDefinitions = []ctc.TrackRouteDefinition{routes[1-tt.want]}
			trainTypeRequest(t, handler, http.MethodPatch, "/api/trains/"+trainID(t, handler, tt.name), string(body), http.StatusBadRequest)
			if after := getTrain(t, handler, tt.name); !reflect.DeepEqual(after, before) {
				t.Fatal("disallowed route modified the saved train")
			}
			input.TrainNumber = "bad_" + tt.name
			body, err = json.Marshal(input)
			if err != nil {
				t.Fatal(err)
			}
			trainTypeRequest(t, handler, http.MethodPost, "/api/trains", string(body), http.StatusBadRequest)
			trainTypeRequest(t, handler, http.MethodGet, "/api/trains/"+input.TrainNumber, "", http.StatusNotFound)
		})
	}
}

func TestTrainRoutesNakatetsuNetwork(t *testing.T) {
	db, _ := trainTypeTestServer(t)
	root := filepath.Join("..", "..", "..", "SourceAssets", "Track", "Network")
	graph := filepath.Join(root, "NakatetsuTrackGraph", "graph.json")
	circuits, err := ctc.LoadTrackCircuitDefinitions(graph)
	if err != nil {
		t.Fatal(err)
	}
	interlocking, err := ctc.LoadInterlockingDefinitions(filepath.Join(root, "NakatetsuInterlocking", "interlocking.json"), circuits)
	if err != nil {
		t.Fatal(err)
	}
	if err := timetable.SyncEquipmentMasters(t.Context(), db, graph, circuits, interlocking.Routes); err != nil {
		t.Fatal(err)
	}
	if err := db.Create(&timetable.TrainType{TrainTypeID: "local", DisplayName: "各停"}).Error; err != nil {
		t.Fatal(err)
	}
	if err := db.Create(&timetable.Timetable{ID: "test", DisplayName: "test"}).Error; err != nil {
		t.Fatal(err)
	}
	server := &ApiServer{DB: db, CircuitDefinitions: circuits, RouteDefinitions: interlocking.Routes}
	handler := server.Routes()
	trainTypeRequest(t, handler, http.MethodPost, "/api/trains", `{"timetableId":"test","trainNumber":"real","trainTypeId":"local","originStationId":"NT07","destinationStationId":"NT08","points":[{"sequenceIndex":0,"stationId":"NT07","trackCircuitId":"NT07_1RT"},{"sequenceIndex":1,"stationId":"NT08","trackCircuitId":"NT08_1RT"}]}`, http.StatusCreated)
	got := getTrain(t, handler, "real")
	if len(got.Points[0].Routes) != 1 || got.Points[0].Routes[0].ControlStationID != "NT07" || got.Points[0].Routes[0].RouteID != "4RI" {
		t.Fatalf("unexpected express line routes: %+v", got.Points)
	}
	trainTypeRequest(t, handler, http.MethodPatch, "/api/trains/"+trainID(t, handler, "real"), `{"points":[{"sequenceIndex":0,"stationId":"NT07","trackCircuitId":"NT07_1RT"},{"sequenceIndex":1,"stationId":"NT08","trackCircuitId":"NT08_2RT"}]}`, http.StatusNoContent)
	got = getTrain(t, handler, "real")
	if len(got.Points[0].Routes) != 1 || got.Points[0].Routes[0].RouteID != "4RJ" {
		t.Fatalf("unexpected local line routes: %+v", got.Points)
	}
	trainTypeRequest(t, handler, http.MethodPost, "/api/trains", `{"timetableId":"test","trainNumber":"depot","trainTypeId":"local","originStationId":"NT11","destinationStationId":"NT11","points":[{"sequenceIndex":0,"section":"depot_exit","stationId":"NT11","trackCircuitId":"NT11_DEPOT_T"},{"sequenceIndex":1,"section":"service","stationId":"NT11","trackCircuitId":"NT11_3LT"}]}`, http.StatusCreated)
	depot := getTrain(t, handler, "depot")
	if len(depot.Points[0].Routes) != 1 || depot.Points[0].Routes[0].RouteID != "11LC" || len(depot.Points[1].Routes) != 0 {
		t.Fatalf("unexpected depot exit routes: %+v", depot.Points)
	}
	trainTypeRequest(t, handler, http.MethodPost, "/api/trains", `{"timetableId":"test","trainNumber":"entry","trainTypeId":"local","originStationId":"NT11","destinationStationId":"NT11","points":[{"sequenceIndex":0,"section":"service","stationId":"NT11","trackCircuitId":"NT11_2RT"},{"sequenceIndex":1,"section":"depot_entry","stationId":"NT11","trackCircuitId":"NT11_DEPOT_T"}]}`, http.StatusCreated)
	entry := getTrain(t, handler, "entry")
	if len(entry.Points[0].Routes) != 1 || entry.Points[0].Routes[0].RouteID != "10R" || len(entry.Points[1].Routes) != 0 {
		t.Fatalf("unexpected depot entry routes: %+v", entry.Points)
	}
	trainTypeRequest(t, handler, http.MethodPost, "/api/trains", `{"timetableId":"test","trainNumber":"up","trainTypeId":"local","originStationId":"NT11","destinationStationId":"NT10","points":[{"sequenceIndex":0,"stationId":"NT11","trackCircuitId":"NT11_3LT"},{"sequenceIndex":1,"stationId":"NT10","trackCircuitId":"NT10_4LT"}]}`, http.StatusCreated)
	up := getTrain(t, handler, "up")
	if len(up.Points[0].Routes) != 1 || up.Points[0].Routes[0].RouteID != "2LJ" {
		t.Fatalf("unexpected up express line routes: %+v", up.Points)
	}
	trainTypeRequest(t, handler, http.MethodPost, "/api/trains", `{"timetableId":"test","trainNumber":"nakada","trainTypeId":"local","originStationId":"NT13","destinationStationId":"NT14","points":[{"sequenceIndex":0,"stationId":"NT13","trackCircuitId":"NT13_DT"},{"sequenceIndex":1,"stationId":"NT14","trackCircuitId":"NT14_2RT"}]}`, http.StatusCreated)
	nakada := getTrain(t, handler, "nakada")
	if len(nakada.Points[0].Routes) != 1 || nakada.Points[0].Routes[0].ControlStationID != "NT14" || nakada.Points[0].Routes[0].RouteID != "1RB" {
		t.Fatalf("unexpected Nakada home route from block section: %+v", nakada.Points)
	}
}
