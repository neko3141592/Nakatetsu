package main

import (
	"context"
	"flag"
	"log"
	"net"
	"net/http"
	"time"

	"github.com/neko3141592/Nakatetsu/Server/gen/ctcv1"
	"github.com/neko3141592/Nakatetsu/Server/internal/api"
	"google.golang.org/grpc"

	grpcserver "github.com/neko3141592/Nakatetsu/Server/internal/communication"
	ctc "github.com/neko3141592/Nakatetsu/Server/internal/ctc"
	"github.com/neko3141592/Nakatetsu/Server/internal/prc"
	timetable "github.com/neko3141592/Nakatetsu/Server/internal/timetable"
)

func main() {

	manifestPath := flag.String("track-graph", "../SourceAssets/Track/Network/NakatetsuTrackGraph/graph.json", "TrackGraph JSON manifest path")
	interlockingPath := flag.String("interlocking", "../SourceAssets/Track/Network/NakatetsuInterlocking/interlocking.json", "Interlocking JSON manifest path")

	flag.Parse()
	definitions, err := ctc.LoadTrackCircuitDefinitions(*manifestPath)
	if err != nil {
		log.Fatalf("軌道回路定義の読み込みに失敗: %v", err)
	}

	routes, err := ctc.LoadTrackRouteDefinitions(*interlockingPath, definitions)
	if err != nil {
		log.Fatalf("進路定義の読み込みに失敗: %v", err)
	}

	// ダイヤDBとの接続を取得
	db, err := timetable.OpenDatabase("timetable.db")
	if err != nil {
		log.Fatalf("DBの初期化に失敗: %v", err)
	}

	sqlDB, err := db.DB()
	if err != nil {
		log.Fatalf("DB接続の取得に失敗: %v", err)
	}
	defer sqlDB.Close()

	// CTC・PRCインスタンスを作成
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	plan, err := timetable.LoadTimetablePlan(ctx, db, "weekday")
	if err != nil {
		log.Fatalf("ダイヤの読み込みに失敗: %v", err)
	}
	ctcCore := ctc.New(definitions, routes)
	prcCore := prc.New(ctcCore, plan)
	ctcCore.SetPRC(prcCore)
	log.Printf("軌道回路定義を読み込み: %d回路", len(definitions))
	log.Printf("進路定義を読み込み: %d進路", len(routes))

	communicationServer := &grpcserver.CommunicationServer{
		CTC: ctcCore,
	}

	// Web API向けサーバーを起動
	apiServer := &api.ApiServer{
		CTC:          ctcCore,
		RouteControl: communicationServer,
	}
	mux := http.NewServeMux()
	mux.HandleFunc("/ws", apiServer.HandleWebSocket)
	mux.HandleFunc("POST /api/route-requests", apiServer.HandleRouteRequest)
	go func() {
		log.Println("Web配信サーバー起動: 0.0.0.0:8080")
		if err := http.ListenAndServe("0.0.0.0:8080", mux); err != nil {
			log.Fatal(err)
		}
	}()

	// Goサーバーを起動
	listener, err := net.Listen("tcp", "127.0.0.1:50051")
	if err != nil {
		log.Fatal(err)
	}
	defer listener.Close()

	server := grpc.NewServer()
	ctcv1.RegisterCtcServiceServer(server, communicationServer)

	log.Println("運行管理サーバー起動: 127.0.0.1:50051")
	if err := server.Serve(listener); err != nil {
		log.Fatal(err)
	}

}
