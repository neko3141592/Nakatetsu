package api

import (
	"context"
	"encoding/json"
	"errors"
	"io"
	"net/http"
	"strings"
	"time"

	"github.com/mattn/go-sqlite3"
	"github.com/neko3141592/Nakatetsu/Server/internal/timetable"
	"gorm.io/gorm"
	"gorm.io/gorm/clause"
)

func (s *ApiServer) HandleListTrains(w http.ResponseWriter, r *http.Request) {
	ctx, cancel := context.WithTimeout(r.Context(), 5*time.Second)
	defer cancel()
	result := []struct {
		TrainNumber string `json:"trainNumber"`
		TrainTypeID string `json:"trainTypeId"`
	}{}
	err := s.DB.WithContext(ctx).Model(&timetable.TimetableTrain{}).
		Order("train_number ASC").Find(&result).Error
	if writeTrainError(w, err) {
		return
	}
	writeTrainJSON(w, http.StatusOK, result)
}

func (s *ApiServer) HandleGetTrain(w http.ResponseWriter, r *http.Request) {
	number := r.PathValue("trainNumber")
	if strings.TrimSpace(number) == "" {
		http.Error(w, "列番が不正です", http.StatusBadRequest)
		return
	}
	ctx, cancel := context.WithTimeout(r.Context(), 5*time.Second)
	defer cancel()
	var result timetable.TimetableTrain
	err := s.DB.WithContext(ctx).
		Preload("TrainType").
		Preload("Points", func(db *gorm.DB) *gorm.DB { return db.Order("sequence_index ASC") }).
		Preload("Points.Routes", func(db *gorm.DB) *gorm.DB { return db.Order("sequence_index ASC") }).
		First(&result, "train_number = ?", number).Error
	if writeTrainError(w, err) {
		return
	}
	writeTrainJSON(w, http.StatusOK, result)
}

func (s *ApiServer) HandlePostTrain(w http.ResponseWriter, r *http.Request) {
	var result timetable.TimetableTrain
	if err := decodeTrainJSON(r, &result); err != nil || !validTrainBasics(result) || !validTrainPoints(result.Points) {
		http.Error(w, "列車のJSONまたは入力値が不正です", http.StatusBadRequest)
		return
	}
	result.TrainType = timetable.TrainType{}
	ctx, cancel := context.WithTimeout(r.Context(), 5*time.Second)
	defer cancel()
	err := s.DB.WithContext(ctx).Transaction(func(tx *gorm.DB) error {
		if err := tx.Omit(clause.Associations).Create(&result).Error; err != nil {
			return err
		}
		return createTrainPoints(tx, result.TrainNumber, &result.Points)
	})
	if writeTrainError(w, err) {
		return
	}
	writeTrainJSON(w, http.StatusCreated, result)
}

func (s *ApiServer) HandlePatchTrain(w http.ResponseWriter, r *http.Request) {
	number := r.PathValue("trainNumber")
	if strings.TrimSpace(number) == "" {
		http.Error(w, "列番が不正です", http.StatusBadRequest)
		return
	}
	var patch struct {
		TrainTypeID          *string                     `json:"trainTypeId"`
		OriginStationID      *string                     `json:"originStationId"`
		DestinationStationID *string                     `json:"destinationStationId"`
		Points               *[]timetable.TimetablePoint `json:"points"`
	}
	if err := decodeTrainJSON(r, &patch); err != nil || (patch.TrainTypeID == nil && patch.OriginStationID == nil && patch.DestinationStationID == nil && patch.Points == nil) ||
		(patch.Points != nil && !validTrainPoints(*patch.Points)) {
		http.Error(w, "列車のJSONまたは入力値が不正です", http.StatusBadRequest)
		return
	}
	ctx, cancel := context.WithTimeout(r.Context(), 5*time.Second)
	defer cancel()
	err := s.DB.WithContext(ctx).Transaction(func(tx *gorm.DB) error {
		var existing timetable.TimetableTrain
		if err := tx.First(&existing, "train_number = ?", number).Error; err != nil {
			return err
		}
		updates := make(map[string]any)
		if patch.TrainTypeID != nil {
			existing.TrainTypeID = *patch.TrainTypeID
			updates["train_type_id"] = *patch.TrainTypeID
		}
		if patch.OriginStationID != nil {
			existing.OriginStationID = *patch.OriginStationID
			updates["origin_station_id"] = *patch.OriginStationID
		}
		if patch.DestinationStationID != nil {
			existing.DestinationStationID = *patch.DestinationStationID
			updates["destination_station_id"] = *patch.DestinationStationID
		}
		if !validTrainBasics(existing) {
			return gorm.ErrInvalidData
		}
		if len(updates) > 0 {
			if err := tx.Model(&existing).Updates(updates).Error; err != nil {
				return err
			}
		}
		if patch.Points != nil {
			if err := tx.Where("train_number = ?", number).Delete(&timetable.TimetablePoint{}).Error; err != nil {
				return err
			}
			return createTrainPoints(tx, number, patch.Points)
		}
		return nil
	})
	if writeTrainError(w, err) {
		return
	}
	w.WriteHeader(http.StatusNoContent)
}

func (s *ApiServer) HandleDeleteTrain(w http.ResponseWriter, r *http.Request) {
	number := r.PathValue("trainNumber")
	if strings.TrimSpace(number) == "" {
		http.Error(w, "列番が不正です", http.StatusBadRequest)
		return
	}
	ctx, cancel := context.WithTimeout(r.Context(), 5*time.Second)
	defer cancel()
	result := s.DB.WithContext(ctx).Where("train_number = ?", number).Delete(&timetable.TimetableTrain{})
	var sqliteErr sqlite3.Error
	if errors.Is(result.Error, gorm.ErrForeignKeyViolated) ||
		(errors.As(result.Error, &sqliteErr) && sqliteErr.ExtendedCode == sqlite3.ErrConstraintTrigger && sqliteErr.Error() == "FOREIGN KEY constraint failed") {
		http.Error(w, "運用から参照中の列車は削除できません", http.StatusConflict)
		return
	}
	if writeTrainError(w, result.Error) {
		return
	}
	if result.RowsAffected == 0 {
		http.Error(w, "列車が見つかりません", http.StatusNotFound)
		return
	}
	w.WriteHeader(http.StatusNoContent)
}

func validTrainBasics(train timetable.TimetableTrain) bool {
	return strings.TrimSpace(train.TrainNumber) != "" && strings.TrimSpace(train.TrainTypeID) != "" && strings.TrimSpace(train.OriginStationID) != "" && strings.TrimSpace(train.DestinationStationID) != ""
}

func validTrainPoints(points []timetable.TimetablePoint) bool {
	seen := make(map[int]bool)
	for _, point := range points {
		if point.SequenceIndex < 0 || seen[point.SequenceIndex] || strings.TrimSpace(point.TrackCircuitID) == "" ||
			(point.StationID != nil && strings.TrimSpace(*point.StationID) == "") ||
			(point.ArrivalSeconds != nil && *point.ArrivalSeconds < 0) ||
			(point.DepartureSeconds != nil && *point.DepartureSeconds < 0) ||
			(point.ArrivalSeconds != nil && point.DepartureSeconds != nil && *point.ArrivalSeconds > *point.DepartureSeconds) {
			return false
		}
		switch point.Handling {
		case "", timetable.HandlingPass, timetable.HandlingStop, timetable.HandlingUnspecified:
		default:
			return false
		}
		seen[point.SequenceIndex] = true
		routes := make(map[int]bool)
		for _, route := range point.Routes {
			if route.SequenceIndex < 0 || routes[route.SequenceIndex] || strings.TrimSpace(route.ControlStationID) == "" || strings.TrimSpace(route.RouteID) == "" {
				return false
			}
			routes[route.SequenceIndex] = true
		}
	}
	return true
}

func createTrainPoints(tx *gorm.DB, number string, points *[]timetable.TimetablePoint) error {
	if *points == nil {
		*points = []timetable.TimetablePoint{}
	}
	for i := range *points {
		point := &(*points)[i]
		point.TimetablePointID, point.TrainNumber = 0, number
		point.Station, point.TrackCircuit = nil, timetable.TrackCircuit{}
		if point.Routes == nil {
			point.Routes = []timetable.TimetablePointRoute{}
		}
		for j := range point.Routes {
			point.Routes[j].TimetablePointID = 0
			point.Routes[j].Route = timetable.Route{}
		}
	}
	if len(*points) == 0 {
		return nil
	}
	return tx.Omit("Station", "TrackCircuit", "Routes.Route").Create(points).Error
}

func decodeTrainJSON(r *http.Request, value any) error {
	decoder := json.NewDecoder(r.Body)
	decoder.DisallowUnknownFields()
	if err := decoder.Decode(value); err != nil {
		return err
	}
	if err := decoder.Decode(new(any)); err != io.EOF {
		return errors.New("JSON must contain one value")
	}
	return nil
}

func writeTrainError(w http.ResponseWriter, err error) bool {
	switch {
	case err == nil:
		return false
	case errors.Is(err, gorm.ErrRecordNotFound):
		http.Error(w, "列車が見つかりません", http.StatusNotFound)
	case errors.Is(err, gorm.ErrDuplicatedKey):
		http.Error(w, "列番が重複しています", http.StatusConflict)
	case errors.Is(err, gorm.ErrInvalidData):
		http.Error(w, "列番・列車種別・発駅・終着駅は必須です", http.StatusBadRequest)
	case errors.Is(err, gorm.ErrForeignKeyViolated):
		http.Error(w, "指定された種別・駅・軌道回路・進路が見つかりません", http.StatusBadRequest)
	default:
		http.Error(w, "列車の処理に失敗しました", http.StatusInternalServerError)
	}
	return true
}

func writeTrainJSON(w http.ResponseWriter, status int, value any) {
	data, err := json.Marshal(value)
	if err != nil {
		http.Error(w, "列車のJSON変換に失敗しました", http.StatusInternalServerError)
		return
	}
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.WriteHeader(status)
	w.Write(data)
}
