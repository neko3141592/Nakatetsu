package api

import (
	"context"
	"encoding/json"
	"errors"
	"io"
	"net/http"
	"strings"

	"github.com/neko3141592/Nakatetsu/Server/internal/timetable"
	"gorm.io/gorm"
	"gorm.io/gorm/clause"
)

type operationTrain struct {
	TrainID       string `json:"trainId"`
	SequenceIndex int    `json:"sequenceIndex"`
}

func (s *ApiServer) HandleListOperations(w http.ResponseWriter, r *http.Request) {
	result := []timetable.TimetableOperation{}
	q := s.DB.WithContext(r.Context())
	if id := r.URL.Query().Get("operationPlanId"); id != "" {
		q = q.Where("operation_plan_id = ?", id)
	}
	if writeOperationError(w, q.Order("operation_number, id").Find(&result).Error) {
		return
	}
	writeOperationJSON(w, 200, result)
}
func loadOperation(ctx context.Context, db *gorm.DB, id string) (timetable.TimetableOperation, error) {
	var result timetable.TimetableOperation
	err := db.WithContext(ctx).Preload("Trains", func(db *gorm.DB) *gorm.DB { return db.Order("sequence_index") }).Preload("Trains.Train.TrainType").First(&result, "id = ?", id).Error
	if result.Trains == nil {
		result.Trains = []timetable.TimetableOperationTrain{}
	}
	return result, err
}
func (s *ApiServer) HandleGetOperation(w http.ResponseWriter, r *http.Request) {
	result, err := loadOperation(r.Context(), s.DB, r.PathValue("id"))
	if writeOperationError(w, err) {
		return
	}
	writeOperationJSON(w, 200, result)
}
func (s *ApiServer) HandlePostOperation(w http.ResponseWriter, r *http.Request) {
	var input struct {
		OperationPlanID string           `json:"operationPlanId"`
		OperationNumber string           `json:"operationNumber"`
		Trains          []operationTrain `json:"trains"`
	}
	if err := decodeOperationJSON(r, &input); err != nil || strings.TrimSpace(input.OperationPlanID) == "" || strings.TrimSpace(input.OperationNumber) == "" || !validOperationTrains(input.Trains) {
		http.Error(w, "運用の入力値が不正です", 400)
		return
	}
	var result timetable.TimetableOperation
	err := s.DB.WithContext(r.Context()).Transaction(func(tx *gorm.DB) error {
		var plan timetable.TimetableOperationPlan
		if err := tx.First(&plan, "id = ?", input.OperationPlanID).Error; err != nil {
			return err
		}
		result = timetable.TimetableOperation{OperationPlanID: plan.ID, TimetableID: plan.TimetableID, OperationNumber: input.OperationNumber}
		if err := tx.Omit(clause.Associations).Create(&result).Error; err != nil {
			return err
		}
		return replaceOperationTrains(tx, result, input.Trains)
	})
	if writeOperationError(w, err) {
		return
	}
	result, err = loadOperation(r.Context(), s.DB, result.ID)
	if !writeOperationError(w, err) {
		writeOperationJSON(w, 201, result)
	}
}
func (s *ApiServer) HandlePatchOperation(w http.ResponseWriter, r *http.Request) {
	var input struct {
		OperationNumber *string           `json:"operationNumber"`
		Trains          *[]operationTrain `json:"trains"`
	}
	if err := decodeOperationJSON(r, &input); err != nil || (input.OperationNumber == nil && input.Trains == nil) || (input.OperationNumber != nil && strings.TrimSpace(*input.OperationNumber) == "") || (input.Trains != nil && !validOperationTrains(*input.Trains)) {
		http.Error(w, "運用の入力値が不正です", 400)
		return
	}
	err := s.DB.WithContext(r.Context()).Transaction(func(tx *gorm.DB) error {
		var result timetable.TimetableOperation
		if err := tx.First(&result, "id = ?", r.PathValue("id")).Error; err != nil {
			return err
		}
		if input.OperationNumber != nil {
			if err := tx.Model(&result).Update("operation_number", *input.OperationNumber).Error; err != nil {
				return err
			}
		}
		if input.Trains != nil {
			return replaceOperationTrains(tx, result, *input.Trains)
		}
		return nil
	})
	if !writeOperationError(w, err) {
		w.WriteHeader(204)
	}
}
func (s *ApiServer) HandleDeleteOperation(w http.ResponseWriter, r *http.Request) {
	result := s.DB.WithContext(r.Context()).Delete(&timetable.TimetableOperation{}, "id = ?", r.PathValue("id"))
	if writeOperationError(w, result.Error) {
		return
	}
	if result.RowsAffected == 0 {
		writeOperationError(w, gorm.ErrRecordNotFound)
		return
	}
	w.WriteHeader(204)
}
func validOperationTrains(trains []operationTrain) bool {
	ids, indices := map[string]bool{}, map[int]bool{}
	for _, train := range trains {
		if strings.TrimSpace(train.TrainID) == "" || train.SequenceIndex < 0 || ids[train.TrainID] || indices[train.SequenceIndex] {
			return false
		}
		ids[train.TrainID], indices[train.SequenceIndex] = true, true
	}
	return true
}
func replaceOperationTrains(tx *gorm.DB, operation timetable.TimetableOperation, trains []operationTrain) error {
	if err := tx.Where("operation_id = ?", operation.ID).Delete(&timetable.TimetableOperationTrain{}).Error; err != nil {
		return err
	}
	for _, train := range trains {
		row := timetable.TimetableOperationTrain{OperationID: operation.ID, OperationPlanID: operation.OperationPlanID, TimetableID: operation.TimetableID, TrainID: train.TrainID, SequenceIndex: train.SequenceIndex}
		if err := tx.Omit(clause.Associations).Create(&row).Error; err != nil {
			return err
		}
	}
	return nil
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
		http.Error(w, "運用・運用計画が見つかりません", 404)
	case errors.Is(err, gorm.ErrDuplicatedKey):
		http.Error(w, "同じ計画内で運用番号が重複しているか、列車が他の運用に割り当て済みです", 409)
	case errors.Is(err, gorm.ErrForeignKeyViolated):
		http.Error(w, "対象ダイヤの列車・運用計画を指定してください", 400)
	default:
		http.Error(w, "運用の処理に失敗しました", 500)
	}
	return true
}
func writeOperationJSON(w http.ResponseWriter, status int, value any) {
	data, err := json.Marshal(value)
	if err != nil {
		http.Error(w, "JSON変換に失敗しました", 500)
		return
	}
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.WriteHeader(status)
	w.Write(data)
}
