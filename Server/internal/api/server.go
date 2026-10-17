package api


import (
    "context"
    "log"
    "net/http"
    "time"

    "github.com/coder/websocket"
    "github.com/neko3141592/Nakatetsu/Server/internal/ctc"
    "google.golang.org/protobuf/proto"
)

type Server struct {
    CTC *ctc.CTC
}

func (s *Server) HandleWebSocket(w http.ResponseWriter, r *http.Request) {
	conn, err := websocket.Accept(w, r, nil)

	if (err != nil) {
		log.Printf("WebSocket接続エラー: %v", err)
        return
	}

	defer conn.CloseNow()

	ctx := conn.CloseRead(context.Background())
	ticker := time.NewTicker(100 * time.Millisecond)
    defer ticker.Stop()

	for {

        data, err := proto.Marshal(s.CTC.GetWebSnapshot())
        if err != nil {
            log.Printf("CTC状態の変換エラー: %v", err)
            return
        }

        writeCtx, cancel := context.WithTimeout(ctx, 5*time.Second)

        err = conn.Write(writeCtx, websocket.MessageBinary, data)

        cancel()
        if err != nil {
            return
        }

        select {
			case <-ctx.Done():
				return
			case <-ticker.C:
        }
    }
}