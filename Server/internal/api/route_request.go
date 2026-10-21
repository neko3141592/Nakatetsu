package api

import (
	"context"
	"google.golang.org/protobuf/encoding/protojson"
	"net/http"
	"time"

	"github.com/google/uuid"
	"github.com/neko3141592/Nakatetsu/Server/gen/ctcv1"
	"io"
)

func (s *ApiServer) HandleRouteRequest(w http.ResponseWriter, r *http.Request) {

	data, err := io.ReadAll(r.Body)
	if err != nil {
		http.Error(w, "Bodyを読み込めません", http.StatusBadRequest)
		return
	}

	var request ctcv1.InterlockingRouteRequest

	if err := protojson.Unmarshal(data, &request); err != nil {
		http.Error(w, "進路要求のJSONが不正です", http.StatusBadRequest)
		return
	}

	request.RequestId = uuid.NewString()

	ctx, cancel := context.WithTimeout(context.Background(), 3*time.Second)
	defer cancel()

	result, err := s.RouteControl.SendRouteRequest(ctx, &request)

	if err != nil {
		http.Error(w, err.Error(), http.StatusBadGateway)
		return
	}

	options := protojson.MarshalOptions{
		EmitDefaultValues: true,
	}

	response, err := options.Marshal(result)
	if err != nil {
		http.Error(w, "結果をJSONに変換できません", http.StatusInternalServerError)
		return
	}

	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(http.StatusOK)
	w.Write(response)

}
