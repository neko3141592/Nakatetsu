package api

import (
	"context"
	"encoding/json"
	"errors"
	"net/http"
	"strings"
	"time"

	"github.com/neko3141592/Nakatetsu/Server/internal/timetable"
	"gorm.io/gorm"
)

func (s *ApiServer) HandleGetStation(w http.ResponseWriter, r *http.Request) {
	id := r.PathValue("stationId")
	if strings.TrimSpace(id) == "" {
		http.Error(w, "駅IDが不正です", http.StatusBadRequest)
		return
	}
	ctx, cancel := context.WithTimeout(r.Context(), 5*time.Second)
	defer cancel()
	result := struct {
		timetable.Station
		Tracks []timetable.StationTrack `json:"tracks"`
	}{Tracks: []timetable.StationTrack{}}
	db := s.DB.WithContext(ctx)
	err := db.First(&result.Station, "station_id = ?", id).Error
	if errors.Is(err, gorm.ErrRecordNotFound) {
		http.Error(w, "駅が見つかりません", http.StatusNotFound)
		return
	}
	if err != nil {
		http.Error(w, "駅の取得に失敗しました", http.StatusInternalServerError)
		return
	}
	if err := db.Where("station_id = ?", id).Order("track_id ASC").Find(&result.Tracks).Error; err != nil {
		http.Error(w, "番線の取得に失敗しました", http.StatusInternalServerError)
		return
	}
	data, err := json.Marshal(result)
	if err != nil {
		http.Error(w, "駅情報のJSON変換に失敗しました", http.StatusInternalServerError)
		return
	}
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.Write(data)
}

func (s *ApiServer) HandleListStations(w http.ResponseWriter, r *http.Request) {
	s.handleMasterList(w, r, &[]timetable.Station{}, "station_id ASC")
}

func (s *ApiServer) HandleListTrackCircuits(w http.ResponseWriter, r *http.Request) {
	s.handleMasterList(w, r, &[]timetable.TrackCircuit{}, "track_circuit_id ASC")
}

func (s *ApiServer) HandleListRoutes(w http.ResponseWriter, r *http.Request) {
	s.handleMasterList(w, r, &[]timetable.Route{}, "control_station_id ASC, route_id ASC")
}

func (s *ApiServer) handleMasterList(w http.ResponseWriter, r *http.Request, result any, order string) {
	ctx, cancel := context.WithTimeout(r.Context(), 5*time.Second)
	defer cancel()
	if err := s.DB.WithContext(ctx).Order(order).Find(result).Error; err != nil {
		http.Error(w, "マスター一覧の取得に失敗しました", http.StatusInternalServerError)
		return
	}
	data, err := json.Marshal(result)
	if err != nil {
		http.Error(w, "マスター一覧のJSON変換に失敗しました", http.StatusInternalServerError)
		return
	}
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.Write(data)
}
