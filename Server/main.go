package main

import (
	"flag"
	"log"
	"net"

	"github.com/neko3141592/Nakatetsu/Server/gen/ctcv1"
	"google.golang.org/grpc"

	grpcserver "github.com/neko3141592/Nakatetsu/Server/internal/communication"
	ctc "github.com/neko3141592/Nakatetsu/Server/internal/ctc"
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
	
	ctcCore := ctc.New(definitions, routes)
	log.Printf("軌道回路定義を読み込み: %d回路", len(definitions))
	log.Printf("進路定義を読み込み: %d進路", len(routes))

	listener, err := net.Listen("tcp", "127.0.0.1:50051")
	if err != nil {
		log.Fatal(err)
	}
	defer listener.Close()

	server := grpc.NewServer()
	ctcv1.RegisterCtcServiceServer(server, &grpcserver.CtcServer{
		CTC: ctcCore,
	})

	log.Println("CTCサーバー起動: 127.0.0.1:50051")
	if err := server.Serve(listener); err != nil {
		log.Fatal(err)
	}

}
