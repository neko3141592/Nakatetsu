package communication

import (
	"context"
	"errors"
	"io"
	"log"
	"sync"

	"github.com/neko3141592/Nakatetsu/Server/gen/ctcv1"
	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/status"
	"google.golang.org/protobuf/types/known/emptypb"
)

type CommunicationServer struct {
	ctcv1.UnimplementedCtcServiceServer
	CTC *ctc.CTC

	streamMu sync.Mutex
	stream   ctcv1.CtcService_ConnectRouteControlServer

	pending map[string]chan *ctcv1.InterlockingRouteRequestResult
}

func (s *CommunicationServer) UpdateSnapshot(
	ctx context.Context,
	msg *ctcv1.UnitySnapshotMessage,
) (*emptypb.Empty, error) {
	snapshot := msg.GetPayload()

	if msg.GetHeader() == nil || snapshot == nil || snapshot.GetWorldTime() == nil || snapshot.GetInterlocking() == nil {
		return nil, status.Error(
			codes.InvalidArgument,
			"snapshot fields are missing",
		)
	}

	if err := s.CTC.UpdateSnapshot(snapshot); err != nil {
		if errors.Is(err, ctc.ErrTrainInitialization) {
			return nil, status.Error(codes.FailedPrecondition, err.Error())
		}
		return nil, status.Error(codes.Internal, err.Error())
	}

	return &emptypb.Empty{}, nil
}

func (s *CommunicationServer) ConnectRouteControl(
	stream ctcv1.CtcService_ConnectRouteControlServer,
) error {
	s.streamMu.Lock()
	if s.stream != nil {
		s.streamMu.Unlock()
		return status.Error(codes.AlreadyExists, "Unityは接続済みです")
	}
	s.stream = stream
	s.streamMu.Unlock()

	defer func() {
		s.streamMu.Lock()
		s.stream = nil
		s.streamMu.Unlock()
	}()

	for {
		result, err := stream.Recv()
		if errors.Is(err, io.EOF) {
			return nil
		}
		if err != nil {
			return err
		}

		if requestID := result.GetRequestId(); requestID == "" {
			log.Print("進路要求結果にrequest_idがありません")
			continue
		}

		s.streamMu.Lock()
		reply, exists := s.pending[result.RequestId]
		s.streamMu.Unlock()

		if !exists {
			continue
		}

		select {
		case reply <- result:
		default:
			log.Printf("重複した進路要求結果を無視: id=%s", result.GetRequestId())
		}

	}
}

func (s *CommunicationServer) SendRouteRequest(
	ctx context.Context,
	request *ctcv1.InterlockingRouteRequest,
) (*ctcv1.InterlockingRouteRequestResult, error) {

	if err := ctx.Err(); err != nil {
		return nil, err
	}

	if request == nil || request.GetRequestId() == "" {
		return nil, status.Error(codes.InvalidArgument, "request_idが必要です")
	}

	requestID := request.GetRequestId()
	// Unityからの結果を待ち受けるためのチャンネルを作成
	reply := make(chan *ctcv1.InterlockingRouteRequestResult, 1)

	s.streamMu.Lock()

	// Unityとstreamがあるか確認
	if s.stream == nil {
		s.streamMu.Unlock()
		return nil, status.Error(codes.Unavailable, "Unityが接続されていません")
	}

	if s.pending == nil {
		s.pending = make(map[string]chan *ctcv1.InterlockingRouteRequestResult)
	}

	if _, exits := s.pending[requestID]; exits {
		s.streamMu.Unlock()
		return nil, status.Error(codes.AlreadyExists, "リクエストIDが重複しています")
	}

	s.pending[requestID] = reply
	connectionContext := s.stream.Context()

	err := s.stream.Send(request)
	s.streamMu.Unlock()

	defer func() {
		s.streamMu.Lock()
		delete(s.pending, requestID)
		s.streamMu.Unlock()
	}()

	if err != nil {
		return nil, err
	}

	select {
	case result := <-reply:
		return result, nil
	case <-ctx.Done():
		return nil, ctx.Err()
	case <-connectionContext.Done():
		return nil, status.Error(codes.Unavailable, "Unityとの接続が切れました")
	}

}
