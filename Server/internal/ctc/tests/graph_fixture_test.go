package tests

// JSON入力を組み立てるためのテスト用データ。
type trackGraphJSON struct {
	GraphID     string                `json:"graphId"`
	Nodes       []graphNodeJSON       `json:"nodes"`
	Edges       []graphEdgeJSON       `json:"edges"`
	Connections []graphConnectionJSON `json:"connections"`
	Circuits    []graphCircuitJSON    `json:"circuits"`
}

type graphNodeJSON struct {
	NodeID           string   `json:"nodeId"`
	ConnectedEdgeIDs []string `json:"connectedEdgeIds"`
}

type graphEdgeJSON struct {
	EdgeID          string  `json:"edgeId"`
	NodeAID         string  `json:"nodeAId"`
	NodeBID         string  `json:"nodeBId"`
	TravelDirection int     `json:"travelDirection"` // 0: 未指定、1: A→B、2: B→A
	Start           float64 `json:"startDistanceOnGeometryM"`
	End             float64 `json:"endDistanceOnGeometryM"`
}

type graphConnectionJSON struct {
	ConnectionID string          `json:"connectionId"`
	NodeID       string          `json:"nodeId"`
	EdgePairs    []graphPairJSON `json:"edgePairs"`
}

type graphPairJSON struct {
	PairID    string `json:"pairId"`
	EdgeAID   string `json:"edgeAId"`
	EdgeBID   string `json:"edgeBId"`
	Condition int    `json:"condition"` // 1: 固定、2: 定位、3: 反位
}

type graphCircuitJSON struct {
	CircuitID   string             `json:"circuitId"`
	ControlKind string             `json:"controlKind"`
	ToCircuitID string             `json:"toCircuitId"`
	Sections    []graphSectionJSON `json:"sections"`
}

type graphSectionJSON struct {
	EdgeID string  `json:"edgeId"`
	Start  float64 `json:"startDistanceOnGeometryM"`
	End    float64 `json:"endDistanceOnGeometryM"`
}
