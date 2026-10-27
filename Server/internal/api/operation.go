package api

import (
	"context"
	"encoding/json"
	"errors"
	"io"
	"net/http"
	"strings"
	"time"

	"github.com/neko3141592/Nakatetsu/Server/internal/timetable"
	"gorm.io/gorm"
	"gorm.io/gorm/clause"
)

type operationTrain struct {
	TrainNumber   string `json:"trainNumber"`
	SequenceIndex int    `json:"sequenceIndex"`
}

type operationDetail struct {
	OperationNumber string           `json:"operationNumber"`
	Trains          []operationTrain `json:"trains"`
}

func (s *ApiServer) HandleListOperations(w http.ResponseWriter, r *http.Request) {
	ctx, cancel := context.WithTimeout(r.Context(), 5*time.Second)
	defer cancel()
	result := []struct {
		OperationNumber string `json:"operationNumber"`
	}{}
	err := s.DB.WithContext(ctx).Model(&timetable.TimetableOperation{}).
		Order("operation_number ASC").Find(&result).Error
	if writeOperationError(w, err) {
		return
	}
	writeOperationJSON(w, http.StatusOK, result)
}

func (s *ApiServer) HandleGetOperation(w http.ResponseWriter, r *http.Request) {
	number := r.PathValue("operationNumber")
	if strings.TrimSpace(number) == "" {
		http.Error(w, "運用番号が不正です", http.StatusBadRequest)
		return
	}
	ctx, cancel := context.WithTimeout(r.Context(), 5*time.Second)
	defer cancel()
	var existing timetable.TimetableOperation
	err := s.DB.WithContext(ctx).
		Preload("Trains", func(db *gorm.DB) *gorm.DB { return db.Order("sequence_index ASC") }).
		First(&existing, "operation_number = ?", number).Error
	if writeOperationError(w, err) {
		return
	}
	result := operationDetail{OperationNumber: existing.OperationNumber, Trains: []operationTrain{}}
	for _, train := range existing.Trains {
		result.Trains = append(result.Trains, operationTrain{TrainNumber: train.TrainNumber, SequenceIndex: train.SequenceIndex})
	}
	writeOperationJSON(w, http.StatusOK, result)
}

func (s *ApiServer) HandlePostOperation(w http.ResponseWriter, r *http.Request) {
	result := operationDetail{Trains: []operationTrain{}}
	if err := decodeOperationJSON(r, &result); err != nil || strings.TrimSpace(result.OperationNumber) == "" || !validOperationTrains(result.Trains) {
		http.Error(w, "運用のJSONまたは入力値が不正です", http.StatusBadRequest)
		return
	}
	ctx, cancel := context.WithTimeout(r.Context(), 5*time.Second)
	defer cancel()
	err := s.DB.WithContext(ctx).Transaction(func(tx *gorm.DB) error {
		if err := tx.Create(&timetable.TimetableOperation{OperationNumber: result.OperationNumber}).Error; err != nil {
			return err
		}
		return replaceOperationTrains(tx, result.OperationNumber, result.Trains)
	})
	if writeOperationError(w, err) {
		return
	}
	if result.Trains == nil {
		result.Trains = []operationTrain{}
	}
	writeOperationJSON(w, http.StatusCreated, result)
}

func (s *ApiServer) HandlePatchOperation(w http.ResponseWriter, r *http.Request) {
	number := r.PathValue("operationNumber")
	if strings.TrimSpace(number) == "" {
		http.Error(w, "運用番号が不正です", http.StatusBadRequest)
		return
	}
	var patch struct {
		Trains *[]operationTrain `json:"trains"`
	}
	if err := decodeOperationJSON(r, &patch); err != nil || patch.Trains == nil || !validOperationTrains(*patch.Trains) {
		http.Error(w, "運用のJSONまたは入力値が不正です", http.StatusBadRequest)
		return
	}
	ctx, cancel := context.WithTimeout(r.Context(), 5*time.Second)
	defer cancel()
	err := s.DB.WithContext(ctx).Transaction(func(tx *gorm.DB) error {
		var existing timetable.TimetableOperation
		if err := tx.First(&existing, "operation_number = ?", number).Error; err != nil {
			return err
		}
		return replaceOperationTrains(tx, number, *patch.Trains)
	})
	if writeOperationError(w, err) {
		return
	}
	w.WriteHeader(http.StatusNoContent)
}

func (s *ApiServer) HandleDeleteOperation(w http.ResponseWriter, r *http.Request) {
	number := r.PathValue("operationNumber")
	if strings.TrimSpace(number) == "" {
		http.Error(w, "運用番号が不正です", http.StatusBadRequest)
		return
	}
	ctx, cancel := context.WithTimeout(r.Context(), 5*time.Second)
	defer cancel()
	result := s.DB.WithContext(ctx).Where("operation_number = ?", number).Delete(&timetable.TimetableOperation{})
	if writeOperationError(w, result.Error) {
		return
	}
	if result.RowsAffected == 0 {
		http.Error(w, "運用が見つかりません", http.StatusNotFound)
		return
	}
	w.WriteHeader(http.StatusNoContent)
}

func validOperationTrains(trains []operationTrain) bool {
	numbers := make(map[string]bool)
	indices := make(map[int]bool)
	for _, train := range trains {
		if strings.TrimSpace(train.TrainNumber) == "" || train.SequenceIndex < 0 || numbers[train.TrainNumber] || indices[train.SequenceIndex] {
			return false
		}
		numbers[train.TrainNumber], indices[train.SequenceIndex] = true, true
	}
	return true
}

func replaceOperationTrains(tx *gorm.DB, number string, trains []operationTrain) error {
	if err := tx.Where("operation_number = ?", number).Delete(&timetable.TimetableOperationTrain{}).Error; err != nil {
		return err
	}
	rows := make([]timetable.TimetableOperationTrain, 0, len(trains))
	for _, train := range trains {
		rows = append(rows, timetable.TimetableOperationTrain{
			OperationNumber: number, TrainNumber: train.TrainNumber, SequenceIndex: train.SequenceIndex,
		})
	}
	if len(rows) == 0 {
		return nil
	}
	return tx.Omit(clause.Associations).Create(&rows).Error
}

func decodeOperationJSON(r *http.Request, value any) error {
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

func writeOperationError(w http.ResponseWriter, err error) bool {
	switch {
	case err == nil:
		return false
	case errors.Is(err, gorm.ErrRecordNotFound):
		http.Error(w, "運用が見つかりません", http.StatusNotFound)
	case errors.Is(err, gorm.ErrDuplicatedKey):
		http.Error(w, "運用番号が重複しています", http.StatusConflict)
	case errors.Is(err, gorm.ErrForeignKeyViolated):
		http.Error(w, "指定された列車が見つかりません", http.StatusBadRequest)
	default:
		http.Error(w, "運用の処理に失敗しました", http.StatusInternalServerError)
	}
	return true
}

func writeOperationJSON(w http.ResponseWriter, status int, value any) {
	data, err := json.Marshal(value)
	if err != nil {
		http.Error(w, "運用のJSON変換に失敗しました", http.StatusInternalServerError)
		return
	}
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.WriteHeader(status)
	w.Write(data)
}
