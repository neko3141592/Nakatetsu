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
		ID          string `json:"id"`
		DisplayName string `json:"displayName"`
		Revision    int64  `json:"revision"`
	}
	if err := decodeTimetableJSON(r, &input); err != nil || strings.TrimSpace(input.ID) == "" || strings.TrimSpace(input.DisplayName) == "" || input.Revision < 0 {
		http.Error(w, "ダイヤのJSONまたは入力値が不正です", http.StatusBadRequest)
		return
	}
	result := timetable.Timetable{ID: input.ID, DisplayName: input.DisplayName, Revision: input.Revision, Trains: []timetable.TimetableTrain{}}
	if err := s.DB.WithContext(r.Context()).Omit(clause.Associations).Create(&result).Error; err != nil {
		if errors.Is(err, gorm.ErrDuplicatedKey) {
			http.Error(w, "登録済みのダイヤです", http.StatusConflict)
		} else {
			http.Error(w, "ダイヤの保存に失敗しました", 500)
		}
		return
	}
	writeTrainJSON(w, http.StatusCreated, result)
}

func (s *ApiServer) HandlePatchTimeTable(w http.ResponseWriter, r *http.Request) {
	var input struct {
		DisplayName *string `json:"displayName"`
	}
	if err := decodeTimetableJSON(r, &input); err != nil || input.DisplayName == nil || strings.TrimSpace(*input.DisplayName) == "" {
		http.Error(w, "ダイヤ名が不正です", 400)
		return
	}
	result := s.DB.WithContext(r.Context()).Model(&timetable.Timetable{}).Where("id = ?", r.PathValue("id")).Updates(map[string]any{"display_name": *input.DisplayName, "revision": gorm.Expr("revision + 1")})
	if result.Error != nil {
		http.Error(w, "ダイヤの更新に失敗しました", 500)
		return
	}
	if result.RowsAffected == 0 {
		http.Error(w, "ダイヤが見つかりません", 404)
		return
	}
	w.WriteHeader(http.StatusNoContent)
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
