package communication

import (
	"context"

	"github.com/neko3141592/Nakatetsu/Server/gen/ctcv1"
	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/status"
	"google.golang.org/protobuf/types/known/emptypb"
)

type CtcServer struct {
	ctcv1.UnimplementedCtcServiceServer
	CTC *ctc.CTC
}

func (s *CtcServer) UpdateSnapshot(
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

	s.CTC.UpdateSnapshot(snapshot)

	return &emptypb.Empty{}, nil
}
