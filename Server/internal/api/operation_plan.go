package api

import (
	"net/http"
	"strings"

	"github.com/neko3141592/Nakatetsu/Server/internal/timetable"
	"gorm.io/gorm"
	"gorm.io/gorm/clause"
)

func (s *ApiServer) HandleListOperationPlans(w http.ResponseWriter, r *http.Request) {
	result := []timetable.TimetableOperationPlan{}
	q := s.DB.WithContext(r.Context()).Omit("Operations")
	if id := r.URL.Query().Get("timetableId"); id != "" {
		q = q.Where("timetable_id = ?", id)
	}
	if writeOperationError(w, q.Order("display_name, id").Find(&result).Error) {
		return
	}
	writeOperationJSON(w, 200, result)
}
func (s *ApiServer) HandleGetOperationPlan(w http.ResponseWriter, r *http.Request) {
	result, err := timetable.LoadOperationPlan(r.Context(), s.DB, r.PathValue("id"))
	if writeOperationError(w, err) {
		return
	}
	writeOperationJSON(w, 200, result)
}
func (s *ApiServer) HandlePostOperationPlan(w http.ResponseWriter, r *http.Request) {
	var input struct {
		TimetableID string `json:"timetableId"`
		DisplayName string `json:"displayName"`
	}
	if err := decodeOperationJSON(r, &input); err != nil || strings.TrimSpace(input.TimetableID) == "" || strings.TrimSpace(input.DisplayName) == "" {
		http.Error(w, "対象ダイヤと運用計画名を入力してください", 400)
		return
	}
	result := timetable.TimetableOperationPlan{TimetableID: input.TimetableID, DisplayName: input.DisplayName, Operations: []timetable.TimetableOperation{}}
	if writeOperationError(w, s.DB.WithContext(r.Context()).Omit(clause.Associations).Create(&result).Error) {
		return
	}
	writeOperationJSON(w, 201, result)
}
func (s *ApiServer) HandlePatchOperationPlan(w http.ResponseWriter, r *http.Request) {
	var input struct {
		DisplayName string `json:"displayName"`
	}
	if err := decodeOperationJSON(r, &input); err != nil || strings.TrimSpace(input.DisplayName) == "" {
		http.Error(w, "運用計画名を入力してください", 400)
		return
	}
	result := s.DB.WithContext(r.Context()).Model(&timetable.TimetableOperationPlan{}).Where("id = ?", r.PathValue("id")).Update("display_name", input.DisplayName)
	if writeOperationError(w, result.Error) {
		return
	}
	if result.RowsAffected == 0 {
		writeOperationError(w, gorm.ErrRecordNotFound)
		return
	}
	w.WriteHeader(204)
}
func (s *ApiServer) HandleDeleteOperationPlan(w http.ResponseWriter, r *http.Request) {
	result := s.DB.WithContext(r.Context()).Delete(&timetable.TimetableOperationPlan{}, "id = ?", r.PathValue("id"))
	if writeOperationError(w, result.Error) {
		return
	}
	if result.RowsAffected == 0 {
		writeOperationError(w, gorm.ErrRecordNotFound)
		return
	}
	w.WriteHeader(204)
}
