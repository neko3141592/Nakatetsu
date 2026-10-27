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
)

type trainTypeRunningTime struct {
	FromStationID  string `json:"fromStationId"`
	ToStationID    string `json:"toStationId"`
	RunningSeconds int    `json:"runningSeconds"`
}

type trainTypeDetail struct {
	timetable.TrainType
	RunningTimes []trainTypeRunningTime `json:"runningTimes"`
}

func (s *ApiServer) HandleListTrainTypes(w http.ResponseWriter, r *http.Request) {
	ctx, cancel := context.WithTimeout(r.Context(), 5*time.Second)
	defer cancel()
	result := []timetable.TrainType{}
	if err := s.DB.WithContext(ctx).Order("train_type_id ASC").Find(&result).Error; err != nil {
		http.Error(w, "列車種別一覧の取得に失敗しました", http.StatusInternalServerError)
		return
	}
	writeTrainTypeJSON(w, http.StatusOK, result)
}

func (s *ApiServer) HandleGetTrainType(w http.ResponseWriter, r *http.Request) {
	id := r.PathValue("id")
	if strings.TrimSpace(id) == "" {
		http.Error(w, "IDが不正です", http.StatusBadRequest)
		return
	}
	ctx, cancel := context.WithTimeout(r.Context(), 5*time.Second)
	defer cancel()
	result := trainTypeDetail{RunningTimes: []trainTypeRunningTime{}}
	db := s.DB.WithContext(ctx)
	err := db.First(&result.TrainType, "train_type_id = ?", id).Error
	if errors.Is(err, gorm.ErrRecordNotFound) {
		http.Error(w, "列車種別が見つかりません", http.StatusNotFound)
		return
	}
	if err != nil {
		http.Error(w, "列車種別の取得に失敗しました", http.StatusInternalServerError)
		return
	}
	if err := db.Model(&timetable.StationRunningTime{}).Where("train_type_id = ?", id).
		Order("from_station_id ASC, to_station_id ASC").Find(&result.RunningTimes).Error; err != nil {
		http.Error(w, "標準運転時分の取得に失敗しました", http.StatusInternalServerError)
		return
	}
	writeTrainTypeJSON(w, http.StatusOK, result)
}

func (s *ApiServer) HandlePostTrainType(w http.ResponseWriter, r *http.Request) {
	result := trainTypeDetail{RunningTimes: []trainTypeRunningTime{}}
	if err := decodeTrainTypeJSON(r, &result); err != nil || strings.TrimSpace(result.TrainTypeID) == "" || strings.TrimSpace(result.DisplayName) == "" || !validTrainTypeRunningTimes(result.RunningTimes) {
		http.Error(w, "列車種別のJSONまたは入力値が不正です", http.StatusBadRequest)
		return
	}
	ctx, cancel := context.WithTimeout(r.Context(), 5*time.Second)
	defer cancel()
	err := s.DB.WithContext(ctx).Transaction(func(tx *gorm.DB) error {
		if err := tx.Create(&result.TrainType).Error; err != nil {
			return err
		}
		return replaceTrainTypeRunningTimes(tx, result.TrainTypeID, result.RunningTimes)
	})
	if errors.Is(err, gorm.ErrDuplicatedKey) {
		http.Error(w, "列車種別IDが重複しています", http.StatusConflict)
		return
	}
	if errors.Is(err, gorm.ErrForeignKeyViolated) {
		http.Error(w, "指定された駅が見つかりません", http.StatusBadRequest)
		return
	}
	if err != nil {
		http.Error(w, "列車種別の保存に失敗しました", http.StatusInternalServerError)
		return
	}
	if result.RunningTimes == nil {
		result.RunningTimes = []trainTypeRunningTime{}
	}
	writeTrainTypeJSON(w, http.StatusCreated, result)
}

func (s *ApiServer) HandlePatchTrainType(w http.ResponseWriter, r *http.Request) {
	id := r.PathValue("id")
	if strings.TrimSpace(id) == "" {
		http.Error(w, "IDが不正です", http.StatusBadRequest)
		return
	}
	var patch struct {
		DisplayName  *string                 `json:"displayName"`
		RunningTimes *[]trainTypeRunningTime `json:"runningTimes"`
	}
	if err := decodeTrainTypeJSON(r, &patch); err != nil || (patch.DisplayName == nil && patch.RunningTimes == nil) ||
		(patch.DisplayName != nil && strings.TrimSpace(*patch.DisplayName) == "") ||
		(patch.RunningTimes != nil && !validTrainTypeRunningTimes(*patch.RunningTimes)) {
		http.Error(w, "列車種別のJSONまたは入力値が不正です", http.StatusBadRequest)
		return
	}
	ctx, cancel := context.WithTimeout(r.Context(), 5*time.Second)
	defer cancel()
	err := s.DB.WithContext(ctx).Transaction(func(tx *gorm.DB) error {
		var existing timetable.TrainType
		if err := tx.First(&existing, "train_type_id = ?", id).Error; err != nil {
			return err
		}
		if patch.DisplayName != nil {
			if err := tx.Model(&existing).Update("display_name", *patch.DisplayName).Error; err != nil {
				return err
			}
		}
		if patch.RunningTimes != nil {
			return replaceTrainTypeRunningTimes(tx, id, *patch.RunningTimes)
		}
		return nil
	})
	if errors.Is(err, gorm.ErrRecordNotFound) {
		http.Error(w, "列車種別が見つかりません", http.StatusNotFound)
		return
	}
	if errors.Is(err, gorm.ErrForeignKeyViolated) {
		http.Error(w, "指定された駅が見つかりません", http.StatusBadRequest)
		return
	}
	if err != nil {
		http.Error(w, "列車種別の更新に失敗しました", http.StatusInternalServerError)
		return
	}
	w.WriteHeader(http.StatusNoContent)
}

func (s *ApiServer) HandleDeleteTrainType(w http.ResponseWriter, r *http.Request) {
	id := r.PathValue("id")
	if strings.TrimSpace(id) == "" {
		http.Error(w, "IDが不正です", http.StatusBadRequest)
		return
	}
	ctx, cancel := context.WithTimeout(r.Context(), 5*time.Second)
	defer cancel()
	result := s.DB.WithContext(ctx).Where("train_type_id = ?", id).Delete(&timetable.TrainType{})
	// SQLiteのRESTRICTはTranslateErrorで変換されない場合がある。
	var sqliteErr sqlite3.Error
	foreignKeyViolation := errors.Is(result.Error, gorm.ErrForeignKeyViolated) ||
		(errors.As(result.Error, &sqliteErr) && sqliteErr.ExtendedCode == sqlite3.ErrConstraintTrigger && sqliteErr.Error() == "FOREIGN KEY constraint failed")
	if foreignKeyViolation {
		http.Error(w, "列車から参照中の種別は削除できません", http.StatusConflict)
		return
	}
	if result.Error != nil {
		http.Error(w, "列車種別の削除に失敗しました", http.StatusInternalServerError)
		return
	}
	if result.RowsAffected == 0 {
		http.Error(w, "列車種別が見つかりません", http.StatusNotFound)
		return
	}
	w.WriteHeader(http.StatusNoContent)
}

func validTrainTypeRunningTimes(times []trainTypeRunningTime) bool {
	seen := make(map[[2]string]bool)
	for _, item := range times {
		key := [2]string{item.FromStationID, item.ToStationID}
		if strings.TrimSpace(item.FromStationID) == "" || strings.TrimSpace(item.ToStationID) == "" || item.RunningSeconds <= 0 || seen[key] {
			return false
		}
		seen[key] = true
	}
	return true
}

func replaceTrainTypeRunningTimes(tx *gorm.DB, id string, times []trainTypeRunningTime) error {
	if err := tx.Where("train_type_id = ?", id).Delete(&timetable.StationRunningTime{}).Error; err != nil {
		return err
	}
	rows := make([]timetable.StationRunningTime, 0, len(times))
	for _, item := range times {
		rows = append(rows, timetable.StationRunningTime{
			TrainTypeID: id, FromStationID: item.FromStationID, ToStationID: item.ToStationID, RunningSeconds: item.RunningSeconds,
		})
	}
	if len(rows) == 0 {
		return nil
	}
	return tx.Create(&rows).Error
}

func decodeTrainTypeJSON(r *http.Request, value any) error {
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

func writeTrainTypeJSON(w http.ResponseWriter, status int, value any) {
	data, err := json.Marshal(value)
	if err != nil {
		http.Error(w, "列車種別のJSON変換に失敗しました", http.StatusInternalServerError)
		return
	}
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.WriteHeader(status)
	w.Write(data)
}
