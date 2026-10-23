package ctc

import (
	"encoding/json"
	"fmt"
	"math"
	"os"
	"path/filepath"
	"slices"
	"strings"
)

// LoadTrackCircuitDefinitionsは分割JSONを結合し、未設定のBlockの次回路を補完する。
// 補完はメモリ上だけで行い、元のJSONは変更しない。
func LoadTrackCircuitDefinitions(manifestPath string) (map[string]TrackCircuitDefinition, error) {
	path, err := filepath.Abs(manifestPath)
	if err != nil {
		return nil, err
	}
	var manifest struct {
		SchemaVersion int      `json:"schemaVersion"`
		GraphID       string   `json:"graphId"`
		Files         []string `json:"files"`
	}
	if err := readDefinitionJSON(path, &manifest); err != nil {
		return nil, err
	}
	if manifest.SchemaVersion != 1 || strings.TrimSpace(manifest.GraphID) == "" || len(manifest.Files) == 0 {
		return nil, fmt.Errorf("invalid TrackGraph manifest: %s", path)
	}

	var graph trackGraphJSON
	loaded := make(map[string]bool)
	for _, file := range manifest.Files {
		if strings.TrimSpace(file) == "" {
			return nil, fmt.Errorf("empty fragment path in %s", path)
		}
		fragmentPath := file
		if !filepath.IsAbs(fragmentPath) {
			fragmentPath = filepath.Join(filepath.Dir(path), fragmentPath)
		}
		fragmentPath = filepath.Clean(fragmentPath)
		if loaded[fragmentPath] {
			return nil, fmt.Errorf("duplicate graph fragment: %s", fragmentPath)
		}
		loaded[fragmentPath] = true
		var fragment trackGraphJSON
		if err := readDefinitionJSON(fragmentPath, &fragment); err != nil {
			return nil, err
		}
		if fragment.GraphID != "" && fragment.GraphID != manifest.GraphID {
			return nil, fmt.Errorf("mismatched graphId in %s", fragmentPath)
		}
		graph.Nodes = append(graph.Nodes, fragment.Nodes...)
		graph.Edges = append(graph.Edges, fragment.Edges...)
		graph.Connections = append(graph.Connections, fragment.Connections...)
		graph.Circuits = append(graph.Circuits, fragment.Circuits...)
	}
	definitions, err := compileCircuitDefinitions(graph)
	if err != nil {
		return nil, fmt.Errorf("TrackGraph %s: %w", path, err)
	}
	return definitions, nil
}

func readDefinitionJSON(path string, value any) error {
	data, err := os.ReadFile(path)
	if err != nil {
		return fmt.Errorf("read %s: %w", path, err)
	}
	if strings.TrimSpace(string(data)) == "null" {
		return fmt.Errorf("null JSON document: %s", path)
	}
	if err := json.Unmarshal(data, value); err != nil {
		return fmt.Errorf("decode %s: %w", path, err)
	}
	return nil
}

// Unityと共通のJSONから、表示名と次回路の検索に必要な項目を読み込む。
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
	DisplayName string             `json:"displayName"`
	ControlKind string             `json:"controlKind"`
	ToCircuitID string             `json:"toCircuitId"`
	Sections    []graphSectionJSON `json:"sections"`
}

type graphSectionJSON struct {
	EdgeID string  `json:"edgeId"`
	Start  float64 `json:"startDistanceOnGeometryM"`
	End    float64 `json:"endDistanceOnGeometryM"`
}

type circuitSection struct {
	CircuitID string
	graphSectionJSON
}

type circuitGraph struct {
	Edges             map[string]graphEdgeJSON
	SectionsByEdge    map[string][]circuitSection
	ConnectionsByNode map[string]graphConnectionJSON
}

func compileCircuitDefinitions(source trackGraphJSON) (map[string]TrackCircuitDefinition, error) {
	if len(source.Circuits) == 0 {
		return nil, fmt.Errorf("TrackGraph has no circuits")
	}
	graph := circuitGraph{
		Edges:             make(map[string]graphEdgeJSON),
		SectionsByEdge:    make(map[string][]circuitSection),
		ConnectionsByNode: make(map[string]graphConnectionJSON),
	}
	nodes := make(map[string]graphNodeJSON)
	for _, node := range source.Nodes {
		if _, exists := nodes[node.NodeID]; exists || strings.TrimSpace(node.NodeID) == "" {
			return nil, fmt.Errorf("empty or duplicate node ID %q", node.NodeID)
		}
		nodes[node.NodeID] = node
	}
	for _, edge := range source.Edges {
		if _, exists := graph.Edges[edge.EdgeID]; exists || strings.TrimSpace(edge.EdgeID) == "" {
			return nil, fmt.Errorf("empty or duplicate edge ID %q", edge.EdgeID)
		}
		if edge.Start < 0 || edge.End < 0 || edge.Start == edge.End || edge.TravelDirection < 0 || edge.TravelDirection > 2 {
			return nil, fmt.Errorf("invalid range or direction for edge %q", edge.EdgeID)
		}
		if !slices.Contains(nodes[edge.NodeAID].ConnectedEdgeIDs, edge.EdgeID) ||
			!slices.Contains(nodes[edge.NodeBID].ConnectedEdgeIDs, edge.EdgeID) {
			return nil, fmt.Errorf("edge %q is not listed by both endpoint nodes", edge.EdgeID)
		}
		graph.Edges[edge.EdgeID] = edge
	}
	for _, node := range source.Nodes {
		listed := make(map[string]bool)
		for _, edgeID := range node.ConnectedEdgeIDs {
			edge, exists := graph.Edges[edgeID]
			if !exists || listed[edgeID] || (edge.NodeAID != node.NodeID && edge.NodeBID != node.NodeID) {
				return nil, fmt.Errorf("invalid edge %q at node %q", edgeID, node.NodeID)
			}
			listed[edgeID] = true
		}
	}
	connectionIDs := make(map[string]bool)
	for _, connection := range source.Connections {
		if connectionIDs[connection.ConnectionID] || strings.TrimSpace(connection.ConnectionID) == "" {
			return nil, fmt.Errorf("empty or duplicate connection ID %q", connection.ConnectionID)
		}
		connectionIDs[connection.ConnectionID] = true
		if _, exists := graph.ConnectionsByNode[connection.NodeID]; exists {
			return nil, fmt.Errorf("multiple connections at node %q", connection.NodeID)
		}
		if err := validateGraphConnection(connection, nodes); err != nil {
			return nil, err
		}
		graph.ConnectionsByNode[connection.NodeID] = connection
	}

	definitions := make(map[string]TrackCircuitDefinition)
	for _, circuit := range source.Circuits {
		if _, exists := definitions[circuit.CircuitID]; exists || strings.TrimSpace(circuit.CircuitID) == "" {
			return nil, fmt.Errorf("empty or duplicate circuit ID %q", circuit.CircuitID)
		}
		var kind TrackControlKind
		switch circuit.ControlKind {
		case "", "Unknown":
			kind = TrackControlUnknown
		case "Block":
			kind = TrackControlBlock
		case "Interlocking":
			kind = TrackControlInterlocking
		default:
			return nil, fmt.Errorf("circuit %q has unknown controlKind %q", circuit.CircuitID, circuit.ControlKind)
		}
		if len(circuit.Sections) == 0 {
			return nil, fmt.Errorf("circuit %q has no sections", circuit.CircuitID)
		}
		for _, section := range circuit.Sections {
			edge, exists := graph.Edges[section.EdgeID]
			if !exists || section.Start < math.Min(edge.Start, edge.End)-circuitDistanceTolerance ||
				section.End > math.Max(edge.Start, edge.End)+circuitDistanceTolerance || section.Start >= section.End {
				return nil, fmt.Errorf("circuit %q has an invalid section on edge %q", circuit.CircuitID, section.EdgeID)
			}
			graph.SectionsByEdge[section.EdgeID] = append(graph.SectionsByEdge[section.EdgeID], circuitSection{circuit.CircuitID, section})
		}
		definitions[circuit.CircuitID] = TrackCircuitDefinition{
			DisplayName: strings.TrimSpace(circuit.DisplayName),
			ControlKind: kind,
			ToCircuitID: circuit.ToCircuitID,
		}
	}
	for circuitID, definition := range definitions {
		if definition.ToCircuitID != "" {
			if _, exists := definitions[definition.ToCircuitID]; !exists {
				return nil, fmt.Errorf("circuit %q references missing toCircuitId %q", circuitID, definition.ToCircuitID)
			}
		}
	}
	for _, circuit := range source.Circuits {
		definition := definitions[circuit.CircuitID]
		if definition.ToCircuitID == "" && definition.ControlKind == TrackControlBlock {
			definition.ToCircuitID = graph.resolveNextCircuit(circuit)
			definitions[circuit.CircuitID] = definition
		}
	}
	return definitions, nil
}

func validateGraphConnection(connection graphConnectionJSON, nodes map[string]graphNodeJSON) error {
	node, exists := nodes[connection.NodeID]
	pairs := connection.EdgePairs
	if !exists || (len(pairs) != 1 && len(pairs) != 2) {
		return fmt.Errorf("invalid node or pair count for connection %q", connection.ConnectionID)
	}
	for _, pair := range pairs {
		if strings.TrimSpace(pair.PairID) == "" || pair.EdgeAID == pair.EdgeBID ||
			!slices.Contains(node.ConnectedEdgeIDs, pair.EdgeAID) || !slices.Contains(node.ConnectedEdgeIDs, pair.EdgeBID) {
			return fmt.Errorf("invalid edge pair %q in connection %q", pair.PairID, connection.ConnectionID)
		}
	}
	if len(pairs) == 1 && pairs[0].Condition == 1 {
		return nil
	}
	if len(pairs) == 2 && pairs[0].PairID != pairs[1].PairID &&
		((pairs[0].Condition == 2 && pairs[1].Condition == 3) || (pairs[0].Condition == 3 && pairs[1].Condition == 2)) {
		common := 0
		if pairs[0].EdgeAID == pairs[1].EdgeAID || pairs[0].EdgeAID == pairs[1].EdgeBID {
			common++
		}
		if pairs[0].EdgeBID == pairs[1].EdgeAID || pairs[0].EdgeBID == pairs[1].EdgeBID {
			common++
		}
		if common == 1 {
			return nil
		}
	}
	return fmt.Errorf("invalid fixed/turnout conditions for connection %q", connection.ConnectionID)
}

const circuitDistanceTolerance = 0.0001 // Unityのfloat値の境界誤差を許容する距離[m]。

func (graph circuitGraph) resolveNextCircuit(circuit graphCircuitJSON) string {
	var nextID string
	for _, section := range circuit.Sections {
		edge := graph.Edges[section.EdgeID]
		if edge.TravelDirection == 0 {
			return ""
		}
		direction := 1
		if edge.End < edge.Start {
			direction = -1
		}
		if edge.TravelDirection == 2 {
			direction = -direction
		}
		position := section.End
		if direction < 0 {
			position = section.Start
		}
		candidate := graph.followCircuit(circuit.CircuitID, edge.EdgeID, position, direction)
		if candidate == "" || (nextID != "" && nextID != candidate) {
			return ""
		}
		nextID = candidate
	}
	return nextID
}

// 同じ回路の続きや非検知区間を固定接続に沿ってたどり、最初の別回路を返す。
// 転轍機・複数候補・循環・終端では推測しない。
func (graph circuitGraph) followCircuit(circuitID, edgeID string, position float64, direction int) string {
	type location struct {
		EdgeID    string
		Position  float64
		Direction int
	}
	visited := make(map[location]bool)
	for {
		current := location{edgeID, position, direction}
		if visited[current] {
			return ""
		}
		visited[current] = true
		nearest := math.Inf(1)
		candidates := make(map[string]float64)
		for _, section := range graph.SectionsByEdge[edgeID] {
			var distance, end float64
			if direction > 0 {
				if section.End <= position+circuitDistanceTolerance {
					continue
				}
				distance, end = math.Max(0, section.Start-position), section.End
			} else {
				if section.Start >= position-circuitDistanceTolerance {
					continue
				}
				distance, end = math.Max(0, position-section.End), section.Start
			}
			if distance < nearest-circuitDistanceTolerance {
				nearest = distance
				clear(candidates)
			}
			if math.Abs(distance-nearest) <= circuitDistanceTolerance {
				if previous, exists := candidates[section.CircuitID]; !exists || float64(direction)*(end-previous) > 0 {
					candidates[section.CircuitID] = end
				}
			}
		}
		if len(candidates) > 1 {
			return ""
		}
		if len(candidates) == 1 {
			for id, end := range candidates {
				if id != circuitID {
					return id
				}
				position = end
			}
			continue
		}

		edge := graph.Edges[edgeID]
		nodeID := edge.NodeBID
		if (direction > 0) != (edge.End > edge.Start) {
			nodeID = edge.NodeAID
		}
		connection, exists := graph.ConnectionsByNode[nodeID]
		if !exists || len(connection.EdgePairs) != 1 || connection.EdgePairs[0].Condition != 1 {
			return ""
		}
		pair := connection.EdgePairs[0]
		if pair.EdgeAID == edgeID {
			edgeID = pair.EdgeBID
		} else if pair.EdgeBID == edgeID {
			edgeID = pair.EdgeAID
		} else {
			return ""
		}
		next := graph.Edges[edgeID]
		position = next.Start
		direction = 1
		if next.End < next.Start {
			direction = -1
		}
		if next.NodeBID == nodeID {
			position = next.End
			direction = -direction
		}
	}
}
