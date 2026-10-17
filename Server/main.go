package main

import (
	"log"
	"net"

	"github.com/neko3141592/Nakatetsu/Server/gen/ctcv1"
	"google.golang.org/grpc"

	grpcserver "github.com/neko3141592/Nakatetsu/Server/internal/communication"
	ctc "github.com/neko3141592/Nakatetsu/Server/internal/ctc"
)

func main() {
	listener, err := net.Listen("tcp", "127.0.0.1:50051")
	if err != nil {
		log.Fatal(err)
	}
	defer listener.Close()

	ctcCore := &ctc.CTC{}

	server := grpc.NewServer()
	ctcv1.RegisterCtcServiceServer(server, &grpcserver.CtcServer{
		CTC: ctcCore,
	})

	log.Println("CTCサーバー起動: 127.0.0.1:50051")
	if err := server.Serve(listener); err != nil {
		log.Fatal(err)
	}

}
