package ctc

import (
	"sync"

	"github.com/neko3141592/Nakatetsu/Server/gen/ctcv1"
	"google.golang.org/protobuf/proto"
)

type CTC struct {
	mu       sync.RWMutex
	snapshot *ctcv1.UnitySnapshot
	state    TrackingState
}

func (c *CTC) UpdateSnapshot(snapshot *ctcv1.UnitySnapshot) {
	copied := proto.Clone(snapshot).(*ctcv1.UnitySnapshot)

	c.mu.Lock()
	defer c.mu.Unlock()

	c.snapshot = copied
}

func (c *CTC) GetSnapshot() *ctcv1.UnitySnapshot {
	c.mu.RLock()
	defer c.mu.RUnlock()

	if c.snapshot == nil {
		return nil
	}

	return proto.Clone(c.snapshot).(*ctcv1.UnitySnapshot)
}
