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

var errInvalidTimetableLinks = errors.New("invalid timetable links")

func (s *ApiServer) HandleListTimetables(w http.ResponseWriter, r *http.Request) {
	ctx, cancel := context.WithTimeout(r.Context(), 5*time.Second)
	defer cancel()
	result := make([]struct {
		ID          string `json:"id"`
		DisplayName string `json:"displayName"`
		Revision    int64  `json:"revision"`
	}, 0)
	if err := s.DB.WithContext(ctx).Model(&timetable.Timetable{}).Order("id ASC").Find(&result).Error; err != nil {
		http.Error(w, "ダイヤ一覧の取得に失敗しました", http.StatusInternalServerError)
		return
	}
	data, err := json.Marshal(result)
	if err != nil {
		http.Error(w, "ダイヤ一覧のJSON変換に失敗しました", http.StatusInternalServerError)
		return
	}
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.Write(data)
}

func (s *ApiServer) HandleGetTimetalbe(w http.ResponseWriter, r *http.Request) {
	id := r.PathValue("id")
	if id == "" {
		http.Error(w, "IDが不正です", http.StatusBadRequest)
		return
	}

	ctx, cancel := context.WithTimeout(r.Context(), 5*time.Second)
	defer cancel()

	result, err := timetable.LoadTimetable(ctx, s.DB, id)
	if errors.Is(err, gorm.ErrRecordNotFound) {
		http.Error(w, "ダイヤが見つかりません", http.StatusNotFound)
		return
	}
	if err != nil {
		http.Error(w, "ダイヤ取得時にエラーが発生しました", http.StatusInternalServerError)
		return
	}

	data, err := json.Marshal(result)
	if err != nil {
		http.Error(w, "ダイヤ取得時にエラーが発生しました", http.StatusInternalServerError)
		return
	}

	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.Write(data)

}

func (s *ApiServer) HandlePostTimeTable(w http.ResponseWriter, r *http.Request) {
	var input struct {
		ID                    string   `json:"id"`
		DisplayName           string   `json:"displayName"`
		Revision              int64    `json:"revision"`
		TemporaryTrainNumbers []string `json:"temporaryTrainNumbers"`
		OperationNumbers      []string `json:"operationNumbers"`
	}
	if err := decodeTimetableJSON(r, &input); err != nil || strings.TrimSpace(input.ID) == "" || input.Revision < 0 ||
		!validTimetableNumbers(input.TemporaryTrainNumbers) || !validTimetableNumbers(input.OperationNumbers) {
		http.Error(w, "ダイヤのJSONまたは入力値が不正です", http.StatusBadRequest)
		return
	}
	ctx, cancel := context.WithTimeout(r.Context(), 5*time.Second)
	defer cancel()
	result := timetable.Timetable{ID: input.ID, DisplayName: input.DisplayName, Revision: input.Revision}
	err := s.DB.WithContext(ctx).Transaction(func(tx *gorm.DB) error {
		if err := tx.Create(&result).Error; err != nil {
			return err
		}
		if err := replaceTimetableLinks(tx, &result, &input.TemporaryTrainNumbers, &input.OperationNumbers); err != nil {
			return err
		}
		var err error
		result, err = timetable.LoadTimetable(ctx, tx, result.ID)
		return err
	})
	if errors.Is(err, gorm.ErrDuplicatedKey) {
		http.Error(w, "登録済みのデータと重複しています", http.StatusConflict)
		return
	}
	if errors.Is(err, errInvalidTimetableLinks) || errors.Is(err, gorm.ErrForeignKeyViolated) {
		http.Error(w, "指定された臨時列車・運用が見つかりません", http.StatusBadRequest)
		return
	}
	if err != nil {
		http.Error(w, "ダイヤの保存に失敗しました", http.StatusInternalServerError)
		return
	}

	response, err := json.Marshal(result)
	if err != nil {
		http.Error(w, err.Error(), http.StatusInternalServerError)
		return
	}

	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(http.StatusCreated)
	w.Write(response)
}

func (s *ApiServer) HandlePatchTimeTable(w http.ResponseWriter, r *http.Request) {
	id := r.PathValue("id")
	if id == "" {
		http.Error(w, "IDが不正です", http.StatusBadRequest)
		return
	}
	var patch struct {
		DisplayName           *string   `json:"displayName"`
		Revision              *int64    `json:"revision"`
		TemporaryTrainNumbers *[]string `json:"temporaryTrainNumbers"`
		OperationNumbers      *[]string `json:"operationNumbers"`
	}
	if err := decodeTimetableJSON(r, &patch); err != nil {
		http.Error(w, "ダイヤのJSONが不正です", http.StatusBadRequest)
		return
	}
	if (patch.DisplayName == nil && patch.Revision == nil && patch.TemporaryTrainNumbers == nil && patch.OperationNumbers == nil) ||
		(patch.Revision != nil && *patch.Revision < 0) ||
		(patch.TemporaryTrainNumbers != nil && !validTimetableNumbers(*patch.TemporaryTrainNumbers)) ||
		(patch.OperationNumbers != nil && !validTimetableNumbers(*patch.OperationNumbers)) {
		http.Error(w, "更新項目が不正です", http.StatusBadRequest)
		return
	}
	updates := make(map[string]any)
	if patch.DisplayName != nil {
		updates["display_name"] = *patch.DisplayName
	}
	if patch.Revision != nil {
		updates["revision"] = *patch.Revision
	}
	ctx, cancel := context.WithTimeout(r.Context(), 5*time.Second)
	defer cancel()
	err := s.DB.WithContext(ctx).Transaction(func(tx *gorm.DB) error {
		var existing timetable.Timetable
		if err := tx.First(&existing, "id = ?", id).Error; err != nil {
			return err
		}
		if len(updates) != 0 {
			if err := tx.Model(&existing).Omit(clause.Associations).Updates(updates).Error; err != nil {
				return err
			}
		}
		if patch.TemporaryTrainNumbers != nil || patch.OperationNumbers != nil {
			return replaceTimetableLinks(tx, &existing, patch.TemporaryTrainNumbers, patch.OperationNumbers)
		}
		return nil
	})
	if errors.Is(err, gorm.ErrRecordNotFound) {
		http.Error(w, "ダイヤが見つかりません", http.StatusNotFound)
		return
	}
	if errors.Is(err, errInvalidTimetableLinks) || errors.Is(err, gorm.ErrForeignKeyViolated) {
		http.Error(w, "指定された臨時列車・運用が見つかりません", http.StatusBadRequest)
		return
	}
	if err != nil {
		http.Error(w, "ダイヤの更新に失敗しました", http.StatusInternalServerError)
		return
	}
	w.WriteHeader(http.StatusNoContent)
}

func validTimetableNumbers(numbers []string) bool {
	seen := make(map[string]bool)
	for _, number := range numbers {
		if strings.TrimSpace(number) == "" || seen[number] {
			return false
		}
		seen[number] = true
	}
	return true
}

func replaceTimetableLinks(tx *gorm.DB, plan *timetable.Timetable, temporaryTrains, operations *[]string) error {
	if temporaryTrains != nil {
		plan.Trains = []timetable.TimetableTrain{}
		if err := tx.Where("train_number IN ?", *temporaryTrains).Find(&plan.Trains).Error; err != nil {
			return err
		}
		if len(plan.Trains) != len(*temporaryTrains) {
			return errInvalidTimetableLinks
		}
	}
	if operations != nil {
		plan.Operations = []timetable.TimetableOperation{}
		if err := tx.Where("operation_number IN ?", *operations).Find(&plan.Operations).Error; err != nil {
			return err
		}
		if len(plan.Operations) != len(*operations) {
			return errInvalidTimetableLinks
		}
	}
	if temporaryTrains != nil {
		if err := tx.Model(plan).Omit("Trains.*").Association("Trains").Replace(plan.Trains); err != nil {
			return err
		}
	}
	if operations != nil {
		return tx.Model(plan).Omit("Operations.*").Association("Operations").Replace(plan.Operations)
	}
	return nil
}

func decodeTimetableJSON(r *http.Request, value any) error {
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

func (s *ApiServer) HandleDeleteTimeTable(w http.ResponseWriter, r *http.Request) {
	id := r.PathValue("id")
	if id == "" {
		http.Error(w, "IDが不正です", http.StatusBadRequest)
		return
	}
	ctx, cancel := context.WithTimeout(r.Context(), 5*time.Second)
	defer cancel()
	result := s.DB.WithContext(ctx).Where("id = ?", id).Delete(&timetable.Timetable{})
	if result.Error != nil {
		http.Error(w, "ダイヤの削除に失敗しました", http.StatusInternalServerError)
		return
	}
	if result.RowsAffected == 0 {
		http.Error(w, "ダイヤが見つかりません", http.StatusNotFound)
		return
	}
	w.WriteHeader(http.StatusNoContent)
}
