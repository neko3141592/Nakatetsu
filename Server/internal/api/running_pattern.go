package api

import (
	"context"
	"errors"
	"net/http"
	"slices"
	"strings"
	"time"

	"github.com/neko3141592/Nakatetsu/Server/internal/timetable"
	"gorm.io/gorm"
	"gorm.io/gorm/clause"
)

func (s *ApiServer) HandleListRunningPatterns(w http.ResponseWriter, r *http.Request) {
	ctx, cancel := context.WithTimeout(r.Context(), 5*time.Second)
	defer cancel()
	result := []struct {
		ID          string `json:"id"`
		TrainTypeID string `json:"trainTypeId"`
		DisplayName string `json:"displayName"`
	}{}
	query := s.DB.WithContext(ctx).Model(&timetable.RunningPattern{})
	if id := r.URL.Query().Get("trainTypeId"); id != "" {
		query = query.Where("train_type_id = ?", id)
	}
	if writeRunningPatternError(w, query.Order("display_name ASC, id ASC").Find(&result).Error) {
		return
	}
	writeTrainTypeJSON(w, http.StatusOK, result)
}

func (s *ApiServer) HandleGetRunningPattern(w http.ResponseWriter, r *http.Request) {
	ctx, cancel := context.WithTimeout(r.Context(), 5*time.Second)
	defer cancel()
	var result timetable.RunningPattern
	err := s.DB.WithContext(ctx).Preload("Points", func(db *gorm.DB) *gorm.DB { return db.Order("sequence_index ASC") }).First(&result, "id = ?", r.PathValue("id")).Error
	if writeRunningPatternError(w, err) {
		return
	}
	writeTrainTypeJSON(w, http.StatusOK, result)
}

func (s *ApiServer) HandlePostRunningPattern(w http.ResponseWriter, r *http.Request) {
	var result timetable.RunningPattern
	if err := decodeTrainJSON(r, &result); err != nil || strings.TrimSpace(result.ID) == "" || strings.TrimSpace(result.TrainTypeID) == "" || strings.TrimSpace(result.DisplayName) == "" {
		http.Error(w, "走行パターンのJSONまたは入力値が不正です", http.StatusBadRequest)
		return
	}
	if writeRunningPatternError(w, s.validateRunningPatternPoints(result.Points)) {
		return
	}
	ctx, cancel := context.WithTimeout(r.Context(), 5*time.Second)
	defer cancel()
	err := s.DB.WithContext(ctx).Transaction(func(tx *gorm.DB) error {
		if err := tx.Omit(clause.Associations).Create(&result).Error; err != nil {
			return err
		}
		return replaceRunningPatternPoints(tx, result.ID, result.Points)
	})
	if writeRunningPatternError(w, err) {
		return
	}
	writeTrainTypeJSON(w, http.StatusCreated, result)
}

func (s *ApiServer) HandlePatchRunningPattern(w http.ResponseWriter, r *http.Request) {
	var patch struct {
		DisplayName *string                          `json:"displayName"`
		Points      *[]timetable.RunningPatternPoint `json:"points"`
	}
	if err := decodeTrainJSON(r, &patch); err != nil || (patch.DisplayName == nil && patch.Points == nil) || (patch.DisplayName != nil && strings.TrimSpace(*patch.DisplayName) == "") {
		http.Error(w, "走行パターンのJSONまたは入力値が不正です", http.StatusBadRequest)
		return
	}
	ctx, cancel := context.WithTimeout(r.Context(), 5*time.Second)
	defer cancel()
	id := r.PathValue("id")
	err := s.DB.WithContext(ctx).Transaction(func(tx *gorm.DB) error {
		var existing timetable.RunningPattern
		if err := tx.First(&existing, "id = ?", id).Error; err != nil {
			return err
		}
		if patch.Points != nil {
			if err := s.validateRunningPatternPoints(*patch.Points); err != nil {
				return err
			}
		}
		if patch.DisplayName != nil {
			if err := tx.Model(&existing).Update("display_name", *patch.DisplayName).Error; err != nil {
				return err
			}
		}
		if patch.Points != nil {
			return replaceRunningPatternPoints(tx, id, *patch.Points)
		}
		return nil
	})
	if writeRunningPatternError(w, err) {
		return
	}
	w.WriteHeader(http.StatusNoContent)
}

func (s *ApiServer) HandleDeleteRunningPattern(w http.ResponseWriter, r *http.Request) {
	ctx, cancel := context.WithTimeout(r.Context(), 5*time.Second)
	defer cancel()
	result := s.DB.WithContext(ctx).Where("id = ?", r.PathValue("id")).Delete(&timetable.RunningPattern{})
	if writeRunningPatternError(w, result.Error) {
		return
	}
	if result.RowsAffected == 0 {
		http.Error(w, "走行パターンが見つかりません", http.StatusNotFound)
		return
	}
	w.WriteHeader(http.StatusNoContent)
}

func (s *ApiServer) validateRunningPatternPoints(points []timetable.RunningPatternPoint) error {
	if len(points) == 0 {
		return gorm.ErrInvalidData
	}
	slices.SortFunc(points, func(a, b timetable.RunningPatternPoint) int { return a.SequenceIndex - b.SequenceIndex })
	first, last := points[0], points[len(points)-1]
	if first.DepartureOffsetSeconds == nil || *first.DepartureOffsetSeconds != 0 || first.ArrivalOffsetSeconds != nil || first.Handling != timetable.HandlingStop || last.Handling != timetable.HandlingStop || (len(points) > 1 && last.DepartureOffsetSeconds != nil) {
		return gorm.ErrInvalidData
	}
	previous := -1
	routePoints := make([]timetable.TimetablePoint, len(points))
	for i, p := range points {
		if p.SequenceIndex != i || strings.TrimSpace(p.StationID) == "" || strings.TrimSpace(p.TrackCircuitID) == "" || (p.Handling != timetable.HandlingStop && p.Handling != timetable.HandlingPass) || (p.Handling == timetable.HandlingPass && p.ArrivalOffsetSeconds != nil) {
			return gorm.ErrInvalidData
		}
		for _, seconds := range []*int{p.ArrivalOffsetSeconds, p.DepartureOffsetSeconds} {
			if seconds != nil {
				if *seconds < 0 || *seconds < previous {
					return gorm.ErrInvalidData
				}
				previous = *seconds
			}
		}
		routePoints[i] = timetable.TimetablePoint{SequenceIndex: i, Section: timetable.PointSectionService, TrackCircuitID: p.TrackCircuitID}
	}
	return s.generateTrainRoutes(routePoints)
}

func replaceRunningPatternPoints(tx *gorm.DB, id string, points []timetable.RunningPatternPoint) error {
	if err := tx.Where("running_pattern_id = ?", id).Delete(&timetable.RunningPatternPoint{}).Error; err != nil {
		return err
	}
	for i := range points {
		points[i].RunningPatternID = id
	}
	return tx.Omit(clause.Associations).Create(&points).Error
}

func writeRunningPatternError(w http.ResponseWriter, err error) bool {
	switch {
	case err == nil:
		return false
	case errors.Is(err, gorm.ErrRecordNotFound):
		http.Error(w, "走行パターンが見つかりません", http.StatusNotFound)
	case errors.Is(err, gorm.ErrDuplicatedKey):
		http.Error(w, "走行パターンIDが重複しています", http.StatusConflict)
	case errors.Is(err, gorm.ErrInvalidData):
		http.Error(w, "始発を0秒とし、駅・番線・停車／通過・相対時刻の順序を確認してください", http.StatusBadRequest)
	case errors.Is(err, gorm.ErrForeignKeyViolated):
		http.Error(w, "指定された種別・駅・軌道回路が見つかりません", http.StatusBadRequest)
	case errors.Is(err, errTrainRoutes):
		http.Error(w, err.Error(), http.StatusBadRequest)
	default:
		http.Error(w, "走行パターンの処理に失敗しました", http.StatusInternalServerError)
	}
	return true
}
