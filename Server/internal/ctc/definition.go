package ctc

type TrackControlKind int

const (
	TrackControlUnknown TrackControlKind = iota
	TrackControlBlock
	TrackControlInterlocking
)

type TrackCircuitDefinition struct {
	// 非連動・連動
	ControlKind TrackControlKind

	// 所定方向の固定の次軌道回路。進路情報があればそちらを優先する。
	ToCircuitID string
}

type RouteKey struct {
	StationID string
	RouteID   string
}

type TrackRouteDefinition struct {
	InterlockingStationID string
	InterlockingRouteID   string

	// 進入元の軌道回路を含めた、進行方向順の回路ID。
	TrackCircuitIDs []string
}

type RouteCircuitRef struct {
	RouteIndex   int // routeDefinitions内の位置
	CircuitIndex int // TrackCircuitIDs内の位置
}

// 進路定義を読み込んだときに作り、定義を変更したら作り直す。
func buildRouteCircuitRefs(routeDefinitions []TrackRouteDefinition) map[string][]RouteCircuitRef {
	refs := make(map[string][]RouteCircuitRef)
	for routeIndex, route := range routeDefinitions {
		for circuitIndex, circuitID := range route.TrackCircuitIDs {
			refs[circuitID] = append(refs[circuitID], RouteCircuitRef{
				RouteIndex:   routeIndex,
				CircuitIndex: circuitIndex,
			})
		}
	}
	return refs
}
