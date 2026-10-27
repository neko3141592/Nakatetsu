package ctc

import (
	"errors"
	"fmt"
	"maps"
	"strings"
	"sync"

	"github.com/neko3141592/Nakatetsu/Server/gen/ctcv1"
	"google.golang.org/protobuf/proto"
)

var ErrTrainInitialization = errors.New("train initialization failed")

// PRCはCTC側が利用するPRCのインターフェース。
type PRC interface {
	Initialize(tracking TrackingState) error
	UpdateRouteRequests()
}

type CTC struct {
	prc                  PRC
	mu                   sync.RWMutex
	snapshot             *ctcv1.UnitySnapshot
	state                TrackingState
	definitions          map[string]TrackCircuitDefinition
	routeDefinitions     []TrackRouteDefinition
	routeRefs            map[string][]RouteCircuitRef
	stationStatesByID    map[string]StationState
	updateMu             sync.Mutex
	prcInitializationErr error
}

func New(definitions map[string]TrackCircuitDefinition, routes []TrackRouteDefinition) *CTC {
	stations := make(map[string]StationState)
	for _, route := range routes {
		stations[route.InterlockingStationID] = StationState{ControlMode: StationControlManual}
	}
	return &CTC{
		state: TrackingState{
			CircuitsByID: make(map[string]TrackCircuitState),
		},
		definitions:       definitions,
		routeDefinitions:  routes,
		routeRefs:         BuildRouteCircuitRefs(routes),
		stationStatesByID: stations,
	}
}

// SetPRCはサーバー起動前にPRCの参照を接続する。
func (c *CTC) SetPRC(prc PRC) {
	c.prc = prc
}

func (c *CTC) UpdateSnapshot(snapshot *ctcv1.UnitySnapshot) (err error) {
	c.updateMu.Lock()
	defer c.updateMu.Unlock()

	copied := proto.Clone(snapshot).(*ctcv1.UnitySnapshot)
	var prc PRC
	initialized := false

	c.mu.Lock()
	defer func() {
		c.mu.Unlock()
		if prc != nil {
			if initialized {
				c.prcInitializationErr = prc.Initialize(c.GetTrackingState())
			}
			if c.prcInitializationErr != nil {
				err = c.prcInitializationErr
				return
			}
			prc.UpdateRouteRequests()
		}
	}()

	c.snapshot = copied

	occupied, routeStates := BuildTrackingInput(copied)

	// 未取得・欠落・定義外の回路があるsnapshotでは追跡状態を更新しない。
	valid := copied.GetWorldTime() != nil && copied.GetInterlocking() != nil && occupied != nil && len(occupied) == len(c.definitions)
	if valid {
		for circuitID := range c.definitions {
			if _, exists := occupied[circuitID]; !exists {
				valid = false
				break
			}
		}
	}

	if valid && !c.state.IsInitialized && copied.GetTrainInitialization() != nil {
		if err := c.initializeTrains(copied.GetTrainInitialization(), occupied); err != nil {
			return err
		}
		initialized = true
	}
	if valid && c.state.IsInitialized {
		UpdateTracking(
			&c.state,
			occupied,
			c.definitions,
			c.routeDefinitions,
			c.routeRefs,
			routeStates,
		)

		// TrainIDを残したまま、次回の比較用の在線状態を更新する。
		for circuitID, isOccupied := range occupied {
			circuit := c.state.CircuitsByID[circuitID]
			circuit.IsOccupied = isOccupied
			c.state.CircuitsByID[circuitID] = circuit
		}

		prc = c.prc
	}

	return nil
}

func (c *CTC) GetUnitySnapshot() *ctcv1.UnitySnapshot {
	c.mu.RLock()
	defer c.mu.RUnlock()

	if c.snapshot == nil {
		return nil
	}

	return proto.Clone(c.snapshot).(*ctcv1.UnitySnapshot)
}

func (c *CTC) GetWebSnapshot() *ctcv1.CtcSnapshot {
	c.mu.RLock()
	defer c.mu.RUnlock()

	snapshot := &ctcv1.CtcSnapshot{
		IsInitialized: c.state.IsInitialized,
		NeedsReset:    c.state.NeedsReset,
		CircuitsById:  make(map[string]*ctcv1.CtcCircuitState),
		TrainsById:    make(map[string]*ctcv1.CtcTrainState),
		TurnoutsById:  make(map[string]*ctcv1.TurnoutState),
	}

	// 受信した駅・進路状態をそのまま公開し、保持中のsnapshotとはコピーを分ける。
	if interlocking := c.snapshot.GetInterlocking(); interlocking != nil {
		snapshot.Interlocking = proto.Clone(interlocking).(*ctcv1.InterlockingSnapshot)
	}
	if worldTime := c.snapshot.GetWorldTime(); worldTime != nil {
		snapshot.WorldTime = proto.Clone(worldTime).(*ctcv1.WorldTimeInformation)
	}

	// 転轍機は追跡の初期登録とは独立し、最新snapshotの実位置をそのまま公開する。
	// mapと各値を作り直し、呼び出し側から保持中のsnapshotを変更できないようにする。
	for turnoutID, state := range c.snapshot.GetTurnouts().GetTurnoutsById() {
		snapshot.TurnoutsById[turnoutID] = &ctcv1.TurnoutState{
			ActualPosition: state.GetActualPosition(),
			IsMoving:       state.GetIsMoving(),
		}
	}

	for circuitID, state := range c.state.CircuitsByID {
		displayName := c.definitions[circuitID].DisplayName
		if displayName == "" {
			displayName = circuitID
		}
		snapshot.CircuitsById[circuitID] = &ctcv1.CtcCircuitState{
			TrainId:     state.TrainID,
			IsOccupied:  state.IsOccupied,
			DisplayName: displayName,
		}
	}

	for trainID, state := range c.state.TrainsByID {
		train := &ctcv1.CtcTrainState{
			ConsistNumber:   state.ConsistNumber,
			TrainNumber:     state.TrainNumber,
			OperationNumber: state.OperationNumber,
		}
		if state.ConsistID != nil {
			train.ConsistId = proto.String(*state.ConsistID)
		}
		snapshot.TrainsById[trainID] = train
	}

	return snapshot
}

// IsInitializedは最初の有効なsnapshotから列車の初期登録を完了したか返す。
func (c *CTC) IsInitialized() bool {
	c.mu.RLock()
	defer c.mu.RUnlock()
	return c.state.IsInitialized
}

// func (c *CTC) RegisterTrain(trainID string)

// 全件を検証してから登録する。呼び出し側でc.muをロックしておく。
func (c *CTC) initializeTrains(initial *ctcv1.TrainInitializationSnapshot, occupied map[string]bool) error {
	if initial == nil {
		return fmt.Errorf("%w: train information is missing", ErrTrainInitialization)
	}

	trains := make(map[string]TrainState)
	circuits := make(map[string]TrackCircuitState, len(occupied))
	consistIDs := make(map[string]bool)

	for _, info := range initial.GetTrains() {
		consistID, circuitID := info.GetConsistId(), info.GetReferenceCircuitId()
		if strings.TrimSpace(consistID) == "" || consistIDs[consistID] {
			return fmt.Errorf("%w: empty or duplicate consist ID %q", ErrTrainInitialization, consistID)
		}
		if strings.TrimSpace(circuitID) == "" {
			return fmt.Errorf("%w: consist=%q: initial circuit ID is missing", ErrTrainInitialization, consistID)
		}
		if _, exists := c.definitions[circuitID]; !exists {
			return fmt.Errorf("%w: consist=%q: unknown initial circuit %q", ErrTrainInitialization, consistID, circuitID)
		}
		if !occupied[circuitID] {
			return fmt.Errorf("%w: consist=%q: initial circuit %q is not occupied", ErrTrainInitialization, consistID, circuitID)
		}
		if circuits[circuitID].TrainID != "" {
			return fmt.Errorf("%w: multiple trains assigned to circuit %q", ErrTrainInitialization, circuitID)
		}
		consistIDs[consistID] = true
		trainID := fmt.Sprintf("train-%06d", len(trains)+1)
		trains[trainID] = TrainState{
			ConsistID: &consistID, ConsistNumber: info.GetConsistNumber(),
			TrainNumber: info.GetTrainNumber(), OperationNumber: info.GetOperationNumber(),
		}
		// IsOccupiedはまだfalseにし、直後のUpdateTrackingで進入リンクを作る。
		circuits[circuitID] = TrackCircuitState{TrainID: trainID}
	}
	for circuitID, isOccupied := range occupied {
		if isOccupied && circuits[circuitID].TrainID == "" {
			return fmt.Errorf("%w: occupied circuit %q has no assigned train", ErrTrainInitialization, circuitID)
		}
	}
	c.state.TrainsByID = trains
	c.state.CircuitsByID = circuits
	c.state.IsInitialized = true
	return nil
}

// GetTrackingStateは表示や確認に使う追跡状態のコピーを返す。
func (c *CTC) GetTrackingState() TrackingState {
	c.mu.RLock()
	defer c.mu.RUnlock()
	state := c.state
	state.CircuitsByID = maps.Clone(c.state.CircuitsByID)
	state.TrainsByID = maps.Clone(c.state.TrainsByID)
	state.DynamicLinks = maps.Clone(c.state.DynamicLinks)
	for trainID, train := range state.TrainsByID {
		if train.ConsistID != nil {
			id := *train.ConsistID
			train.ConsistID = &id
			state.TrainsByID[trainID] = train
		}
	}
	return state
}

// BuildTrackingInputはsnapshotを追跡処理の入力に変換する。
func BuildTrackingInput(snapshot *ctcv1.UnitySnapshot) (map[string]bool, map[RouteKey]InterlockingRouteState) {
	var occupied map[string]bool
	if circuits := snapshot.GetTrackCircuits(); circuits != nil {
		occupied = make(map[string]bool, len(circuits.GetOccupiedByCircuitId()))
		maps.Copy(occupied, circuits.GetOccupiedByCircuitId())
	}

	routeStates := make(map[RouteKey]InterlockingRouteState)
	for stationID, station := range snapshot.GetInterlocking().GetStationsById() {
		if !station.GetIsInitialized() || !station.GetHasOutput() {
			continue
		}
		for routeID, route := range station.GetRoutesById() {
			if route == nil {
				continue
			}
			key := RouteKey{StationID: stationID, RouteID: routeID}
			routeStates[key] = InterlockingRouteState{
				IsAvailable:     route.GetIsAvailable(),
				IsRouteSet:      route.GetIsRouteSet(),
				PathEstablished: route.GetPathEstablished(),
				ProceedAllowed:  route.GetProceedAllowed(),
				RouteLocked:     route.GetRouteLocked(),
				CancelPending:   route.GetCancelPending(),
			}
		}
	}

	return occupied, routeStates
}
