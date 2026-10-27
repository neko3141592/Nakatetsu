package api

import (
	"net/http"
)

func (s *ApiServer) Routes() http.Handler {
	mux := http.NewServeMux()

	// WebSocket
	mux.HandleFunc("/ws", s.HandleWebSocket)

	// RouteRequest
	mux.HandleFunc("POST /api/route-requests", s.HandleRouteRequest)

	// Timetable
	mux.HandleFunc("GET /api/timetable", s.HandleListTimetables)
	mux.HandleFunc("GET /api/timetable/{id}", s.HandleGetTimetalbe)
	mux.HandleFunc("POST /api/timetable/", s.HandlePostTimeTable)
	mux.HandleFunc("PATCH /api/timetable/{id}", s.HandlePatchTimeTable)
	mux.HandleFunc("DELETE /api/timetable/{id}", s.HandleDeleteTimeTable)

	// TrainType
	mux.HandleFunc("GET /api/train-types", s.HandleListTrainTypes)
	mux.HandleFunc("GET /api/train-types/{id}", s.HandleGetTrainType)
	mux.HandleFunc("POST /api/train-types", s.HandlePostTrainType)
	mux.HandleFunc("PATCH /api/train-types/{id}", s.HandlePatchTrainType)
	mux.HandleFunc("DELETE /api/train-types/{id}", s.HandleDeleteTrainType)

	mux.HandleFunc("GET /api/trains", s.HandleListTrains)
	mux.HandleFunc("GET /api/trains/{trainNumber}", s.HandleGetTrain)
	mux.HandleFunc("POST /api/trains", s.HandlePostTrain)
	mux.HandleFunc("PATCH /api/trains/{trainNumber}", s.HandlePatchTrain)
	mux.HandleFunc("DELETE /api/trains/{trainNumber}", s.HandleDeleteTrain)

	mux.HandleFunc("GET /api/operations", s.HandleListOperations)
	mux.HandleFunc("GET /api/operations/{operationNumber}", s.HandleGetOperation)
	mux.HandleFunc("POST /api/operations", s.HandlePostOperation)
	mux.HandleFunc("PATCH /api/operations/{operationNumber}", s.HandlePatchOperation)
	mux.HandleFunc("DELETE /api/operations/{operationNumber}", s.HandleDeleteOperation)

	mux.HandleFunc("GET /api/stations", s.HandleListStations)
	mux.HandleFunc("GET /api/stations/{stationId}", s.HandleGetStation)
	mux.HandleFunc("GET /api/track-circuits", s.HandleListTrackCircuits)
	mux.HandleFunc("GET /api/routes", s.HandleListRoutes)

	return mux
}
