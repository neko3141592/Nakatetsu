#!/usr/bin/env python3
"""Export draw.io circuit geometry to SVG and the Web diagram data.

Run from any directory: python3 Tools/export-circuit-diagram.py [NT01 NT02 ... | --all]
Destination marker IDs use Destination_<full circuit ID>; labels come from the interlocking definition.
Origin triangles use Origin_<full circuit ID> and must match a route's start track circuit.
Append _R or _L when separate origins are needed for both directions on one circuit.
"""

import argparse
import json
import math
from copy import deepcopy
from pathlib import Path
import re
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[1]
diagram_dir = ROOT / "SourceAssets/Track/Network/NakatetsuTrafficControl/CircuitDiagram"
DIAGRAM_ID = re.compile(r"NT\d+(?:_NT\d+)?")


def export(station_id):
    model = ET.parse(diagram_dir / "drawio" / f"{station_id}.drawio").find(".//mxGraphModel")
    cells = {}
    for element in model.find("root"):
        # draw.io wraps cells with custom metadata in an <object> element.
        cell = element if element.tag == "mxCell" else deepcopy(element.find("mxCell"))
        if element.tag != "mxCell":
            cell.set("id", element.get("id"))
            cell.set("value", element.get("label", ""))
        cell_id = cell.get("id")
        if cell_id in cells:
            raise ValueError(f"Duplicate diagram cell ID: {cell_id}")
        cells[cell_id] = cell

    graph_kind = "Sections" if "_" in station_id else "Stations"
    graph_path = ROOT / "SourceAssets/Track/Network/NakatetsuTrackGraph" / graph_kind / f"{station_id}.json"
    graph = json.loads(graph_path.read_text())
    circuit_definitions = {circuit["circuitId"]: circuit for circuit in graph["circuits"]}
    turnout_connections = [
        connection for connection in graph["connections"]
        if any(pair["condition"] in (2, 3) for pair in connection["edgePairs"])
    ]
    turnout_circuit_ids = {
        circuit_id
        for circuit_id, circuit in circuit_definitions.items()
        if any(
            {edge_id for pair in connection["edgePairs"] for edge_id in (pair["edgeAId"], pair["edgeBId"])}
            <= {section["edgeId"] for section in circuit["sections"]}
            for connection in turnout_connections
        )
    }
    circuit_svg_ids = set(circuit_definitions) | {
        f"{circuit_id}_{position}" for circuit_id in turnout_circuit_ids for position in ("N", "R")
    }


    def number(element, name):
        return float(element.get(name, "0"))


    def style(cell):
        return dict(part.split("=", 1) for part in cell.get("style", "").split(";") if "=" in part)


    def is_destination(cell):
        return re.fullmatch(r"Destination_NT\d+_[A-Z0-9_]+", cell.get("id", "")) is not None


    def is_origin(cell):
        return re.fullmatch(r"Origin_NT\d+_[A-Z0-9_]+", cell.get("id", "")) is not None


    def origin_target(cell):
        circuit_id = cell.get("id").removeprefix("Origin_")
        direction = None
        if circuit_id not in circuit_definitions and circuit_id.endswith(("_R", "_L")):
            circuit_id, direction = circuit_id.rsplit("_", 1)
        return circuit_id, direction


    def is_turnout_indicator(cell):
        return re.fullmatch(fr"{station_id}_TurnoutIndicator_[A-Z0-9]+", cell.get("id", "")) is not None


    def is_train_label(cell):
        return cell.get("id", "").startswith(f"{station_id}_TrainLabel_")


    def is_label(cell):
        return re.fullmatch(fr"{station_id}_Label_[A-Za-z0-9_]+", cell.get("id", "")) is not None


    def is_boundary(cell):
        return re.fullmatch(fr"{station_id}_Boundary_[A-Za-z0-9_]+", cell.get("id", "")) is not None


    def children(cell):
        return [child for child in cells.values() if child.get("parent") == cell.get("id")]


    def origin(cell):
        parent = cells.get(cell.get("parent"))
        if parent is None or parent.get("id") in ("0", "1"):
            return 0, 0
        x, y = origin(parent)
        geometry = parent.find("mxGeometry")
        return x + number(geometry, "x"), y + number(geometry, "y")


    def circuit_edges(cell):
        if is_destination(cell) or is_origin(cell) or is_turnout_indicator(cell) or is_train_label(cell) or is_label(cell) or is_boundary(cell):
            return []
        if cell.get("edge") == "1":
            return [cell]
        return [edge for child in children(cell) for edge in circuit_edges(child)]


    def edge_path(cell):
        geometry = cell.find("mxGeometry")
        points = [geometry.find("mxPoint[@as='sourcePoint']")]
        points += geometry.findall("Array[@as='points']/mxPoint")
        points += [geometry.find("mxPoint[@as='targetPoint']")]
        x, y = origin(cell)
        return " ".join(
            f"{'M' if index == 0 else 'L'} {x + number(point, 'x'):g} {y + number(point, 'y'):g}"
            for index, point in enumerate(points)
        )


    def stroke_width(cell):
        widths = {float(style(edge).get("strokeWidth", "1")) for edge in circuit_edges(cell)}
        if len(widths) != 1:
            raise ValueError(f"Circuit segments must have one stroke width: {cell.get('id')}")
        return widths.pop()


    circuits = []
    platforms = []
    labels = []


    def collect(cell):
        cell_id = cell.get("id")
        if cell_id in circuit_svg_ids:
            paths = [edge_path(edge) for edge in circuit_edges(cell)]
            if not paths:
                raise ValueError(f"Circuit has no track segments: {cell_id}")
            circuits.append({"id": cell_id, "paths": paths})
        elif cell_id.startswith(f"{station_id}_Platform_"):
            geometry = cell.find("mxGeometry")
            x, y = origin(cell)
            platforms.append({"id": cell_id, "x": x + number(geometry, "x"), "y": y + number(geometry, "y"), **{key: number(geometry, key) for key in ("width", "height")}})
        elif is_label(cell):
            if cell.get("vertex") != "1" or children(cell):
                raise ValueError(f"Label must be one text vertex: {cell_id}")
            labels.append(cell)
        elif is_destination(cell) or is_origin(cell) or is_turnout_indicator(cell) or is_train_label(cell) or is_boundary(cell):
            pass
        elif "group" in cell.get("style", "").split(";"):
            for child in children(cell):
                collect(child)
        else:
            raise ValueError(f"Unrecognized diagram cell: {cell_id}")


    for cell in cells.values():
        if cell.get("parent") == "1":
            collect(cell)

    boundaries = [cell for cell in cells.values() if is_boundary(cell)]
    for boundary in boundaries:
        geometry = boundary.find("mxGeometry")
        if (
            boundary.get("edge") != "1"
            or geometry is None
            or geometry.find("mxPoint[@as='sourcePoint']") is None
            or geometry.find("mxPoint[@as='targetPoint']") is None
        ):
            raise ValueError(f"Boundary must be an edge with source and target points: {boundary.get('id')}")

    exported_ids = {circuit["id"] for circuit in circuits}
    for circuit_id in circuit_definitions:
        branch_ids = {f"{circuit_id}_N", f"{circuit_id}_R"}
        has_plain = circuit_id in exported_ids
        has_branches = branch_ids <= exported_ids
        if has_plain == has_branches or (branch_ids & exported_ids and not has_branches):
            raise ValueError(f"Circuit must have one SVG ID or both N/R SVG IDs: {circuit_id}")

    train_labels = []
    for cell in cells.values():
        if not is_train_label(cell):
            continue
        marker_id = cell.get("id")
        suffix = marker_id.removeprefix(f"{station_id}_TrainLabel_")
        svg_id = f"{station_id}_{suffix}"
        if svg_id not in exported_ids:
            raise ValueError(f"Unknown circuit SVG ID for train label {marker_id}: {svg_id}")
        circuit_id = svg_id if svg_id in circuit_definitions else svg_id[:-2]
        geometry = cell.find("mxGeometry")
        if cell.get("vertex") != "1" or cell.get("edge") == "1" or children(cell) or geometry is None or geometry.get("relative") == "1":
            raise ValueError(f"Train label marker must be one vertex with absolute geometry: {marker_id}")
        try:
            dimensions = {key: number(geometry, key) for key in ("x", "y", "width", "height")}
        except ValueError as error:
            raise ValueError(f"Train label marker geometry must contain numeric coordinates and size: {marker_id}") from error
        if not all(math.isfinite(value) for value in dimensions.values()) or dimensions["width"] <= 0 or dimensions["height"] <= 0:
            raise ValueError(f"Train label marker geometry must be finite with positive width and height: {marker_id}")
        x, y = origin(cell)
        if not math.isfinite(x) or not math.isfinite(y):
            raise ValueError(f"Train label marker parent coordinates must be finite: {marker_id}")
        train_labels.append({"id": marker_id, "circuitId": circuit_id, "svgId": svg_id, "x": x + dimensions["x"] + dimensions["width"] / 2, "y": y + dimensions["y"] + dimensions["height"] / 2})

    interlocking_path = ROOT / "SourceAssets/Track/Network/NakatetsuInterlocking/Stations" / f"{station_id}.json"
    interlocking = json.loads(interlocking_path.read_text()) if interlocking_path.exists() else {"routes": [], "turnoutLocks": [], "destinations": []}
    turnout_ids = {turnout["connectionId"] for turnout in interlocking["turnoutLocks"]}
    turnout_indicators = []
    for cell in cells.values():
        if not is_turnout_indicator(cell):
            continue
        marker_id = cell.get("id")
        label = marker_id.removeprefix(f"{station_id}_TurnoutIndicator_")
        turnout_id = f"{station_id}_{label}"
        if turnout_id not in turnout_ids:
            raise ValueError(f"Unknown turnout for indicator {marker_id}: {turnout_id}")
        geometry = cell.find("mxGeometry")
        x, y = origin(cell)
        turnout_indicators.append({"id": marker_id, "turnoutId": turnout_id, "label": label, "x": x + number(geometry, "x") + number(geometry, "width") / 2, "y": y + number(geometry, "y") + number(geometry, "height") / 2})
    destinations = [cell for cell in cells.values() if is_destination(cell)]
    destinations_by_circuit_id = {}
    for definition in interlocking["destinations"]:
        circuit_id = definition["trackCircuitId"]
        if circuit_id in destinations_by_circuit_id:
            raise ValueError(f"Multiple destination codes for track circuit: {circuit_id}")
        destinations_by_circuit_id[circuit_id] = definition
    for destination in destinations:
        circuit_id = destination.get("id").removeprefix("Destination_")
        if circuit_id not in circuit_definitions:
            raise ValueError(f"Unknown destination track circuit: {circuit_id}")
        if circuit_id not in destinations_by_circuit_id:
            raise ValueError(f"Track circuit is not registered as a destination: {circuit_id}")
    origins = [cell for cell in cells.values() if is_origin(cell)]
    start_circuit_ids = {route["startTrackCircuitId"] for route in interlocking["routes"]}
    for cell in origins:
        marker_id = cell.get("id")
        circuit_id, direction = origin_target(cell)
        if circuit_id not in circuit_definitions or circuit_id not in start_circuit_ids:
            raise ValueError(f"Unknown route start track circuit for origin {marker_id}: {circuit_id}")
        if direction and not any(route["startTrackCircuitId"] == circuit_id and re.match(fr"\d+{direction}", route["routeId"]) for route in interlocking["routes"]):
            raise ValueError(f"No route in direction {direction} for origin: {marker_id}")
        geometry = cell.find("mxGeometry")
        if cell.get("vertex") != "1" or children(cell) or "triangle" not in cell.get("style", "").split(";") or geometry is None or geometry.get("relative") == "1":
            raise ValueError(f"Origin must be one triangle vertex with absolute geometry: {marker_id}")
        dimensions = {key: number(geometry, key) for key in ("x", "y", "width", "height")}
        if not all(math.isfinite(value) for value in dimensions.values()) or dimensions["width"] <= 0 or dimensions["height"] <= 0:
            raise ValueError(f"Origin geometry must be finite with positive width and height: {marker_id}")
        if style(cell).get("direction", "east") not in ("east", "west", "north", "south"):
            raise ValueError(f"Unsupported origin triangle direction: {marker_id}")
    data = {"width": number(model, "pageWidth"), "height": number(model, "pageHeight"), "circuits": circuits, "platforms": platforms}
    layout = {"width": data["width"], "height": data["height"], "turnoutIndicators": turnout_indicators, "trainLabels": train_labels}
    layout_path = ROOT / "client/src/data/diagrams" / f"{station_id}.layout.json"
    layout_path.parent.mkdir(parents=True, exist_ok=True)
    layout_path.write_text(json.dumps(layout, ensure_ascii=False, indent=2) + "\n")
    data_path = ROOT / "client/src/components/debug" / f"{station_id}Diagram.data.json"
    data_path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n")

    ET.register_namespace("", "http://www.w3.org/2000/svg")
    svg = ET.Element("svg", {"xmlns": "http://www.w3.org/2000/svg", "width": f"{data['width']:g}", "height": f"{data['height']:g}", "viewBox": f"0 0 {data['width']:g} {data['height']:g}", "style": "background: transparent; color-scheme: light dark; color: light-dark(#000000, #ffffff);"})
    for circuit in circuits:
        group = ET.SubElement(svg, "g", {"id": circuit["id"], "data-cell-id": circuit["id"], "fill": "none", "stroke": "currentColor", "stroke-width": f"{stroke_width(cells[circuit['id']]):g}px"})
        for path in circuit["paths"]:
            ET.SubElement(group, "path", {"d": path, "vector-effect": "non-scaling-stroke"})
    for platform in platforms:
        group = ET.SubElement(svg, "g", {"id": platform["id"], "data-cell-id": platform["id"]})
        platform_style = style(cells[platform["id"]])
        ET.SubElement(group, "rect", {**{key: f"{platform[key]:g}" for key in ("x", "y", "width", "height")}, "fill": "none", "stroke": platform_style.get("strokeColor", "#3f3f46"), "stroke-width": "1"})
    for destination in destinations:
        destination_id = destination.get("id")
        circuit_id = destination_id.removeprefix("Destination_")
        definition = destinations_by_circuit_id[circuit_id]
        group = ET.SubElement(svg, "g", {"id": destination_id, "data-cell-id": destination_id, "data-circuit-id": circuit_id, "data-destination-code": definition["code"]})
        for cell in children(destination):
            geometry = cell.find("mxGeometry")
            x, y = origin(cell)
            cx = x + number(geometry, "x") + number(geometry, "width") / 2
            cy = y + number(geometry, "y") + number(geometry, "height") / 2
            cell_style = style(cell)
            attrs = {"id": cell.get("id"), "data-cell-id": cell.get("id")}
            tokens = cell.get("style", "").split(";")
            if "ellipse" in tokens:
                ET.SubElement(group, "ellipse", {**attrs, "cx": f"{cx:g}", "cy": f"{cy:g}", "rx": f"{number(geometry, 'width') / 2:g}", "ry": f"{number(geometry, 'height') / 2:g}", "fill": cell_style.get("fillColor", "none"), "stroke": cell_style.get("strokeColor", "currentColor"), "stroke-width": cell_style.get("strokeWidth", "1"), "vector-effect": "non-scaling-stroke"})
            elif "text" in tokens:
                text = ET.SubElement(group, "text", {**attrs, "x": f"{cx:g}", "y": f"{cy:g}", "text-anchor": "middle", "dominant-baseline": "central", "font-family": cell_style.get("fontFamily", "Helvetica, Arial, sans-serif"), "font-size": cell_style.get("fontSize", "12"), "fill": cell_style.get("fontColor", "currentColor")})
                text.text = definition["code"]
            else:
                raise ValueError(f"Unsupported destination shape: {cell.get('id')}")
    for cell in origins:
        marker_id = cell.get("id")
        geometry = cell.find("mxGeometry")
        cell_style = style(cell)
        x, y = origin(cell)
        x += number(geometry, "x")
        y += number(geometry, "y")
        points = {
            "east": [(0, 0), (1, .5), (0, 1)],
            "west": [(1, 0), (0, .5), (1, 1)],
            "north": [(0, 1), (.5, 0), (1, 1)],
            "south": [(0, 0), (.5, 1), (1, 0)],
        }[cell_style.get("direction", "east")]
        if cell_style.get("flipH") == "1":
            points = [(1 - px, py) for px, py in points]
        if cell_style.get("flipV") == "1":
            points = [(px, 1 - py) for px, py in points]
        circuit_id, direction = origin_target(cell)
        attrs = {"id": marker_id, "data-cell-id": marker_id, "data-circuit-id": circuit_id}
        if direction:
            attrs["data-route-direction"] = direction
        group = ET.SubElement(svg, "g", attrs)
        ET.SubElement(group, "polygon", {
            "points": " ".join(f"{x + px * number(geometry, 'width'):g},{y + py * number(geometry, 'height'):g}" for px, py in points),
            "fill": cell_style.get("fillColor", "none"),
            "stroke": cell_style.get("strokeColor", "currentColor"),
            "stroke-width": cell_style.get("strokeWidth", "1"),
            "vector-effect": "non-scaling-stroke",
        })
    for label in labels:
        geometry = label.find("mxGeometry")
        label_style = style(label)
        x, y = origin(label)
        x += number(geometry, "x")
        y += number(geometry, "y") + number(geometry, "height") / 2
        alignment = label_style.get("align", "center")
        anchor = {"left": "start", "center": "middle", "right": "end"}.get(alignment)
        if anchor is None:
            raise ValueError(f"Unsupported label alignment: {label.get('id')}")
        x += number(geometry, "width") * {"left": 0, "center": .5, "right": 1}[alignment]
        text = ET.SubElement(svg, "text", {"id": label.get("id"), "data-cell-id": label.get("id"), "x": f"{x:g}", "y": f"{y:g}", "text-anchor": anchor, "dominant-baseline": "central", "font-family": label_style.get("fontFamily", "Helvetica, Arial, sans-serif"), "font-size": label_style.get("fontSize", "12"), "fill": label_style.get("fontColor", "currentColor")})
        text.text = label.get("value", "")
    for boundary in boundaries:
        boundary_id = boundary.get("id")
        boundary_style = style(boundary)
        group = ET.SubElement(svg, "g", {"id": boundary_id, "data-cell-id": boundary_id, "fill": "none", "stroke": boundary_style.get("strokeColor", "#EC4899"), "stroke-width": f"{float(boundary_style.get('strokeWidth', '1')):g}px"})
        ET.SubElement(group, "path", {"d": edge_path(boundary), "vector-effect": "non-scaling-stroke"})
    ET.indent(svg, space="  ")
    svg_path = diagram_dir / "svg" / f"{station_id}.svg"
    ET.ElementTree(svg).write(svg_path, encoding="utf-8", xml_declaration=True)
    with svg_path.open("a") as output:
        output.write("\n")
    public_path = ROOT / "client/public/ctc/diagram" / f"{station_id}.svg"
    public_path.parent.mkdir(parents=True, exist_ok=True)
    public_path.write_bytes(svg_path.read_bytes())
    print(f"Exported {len(circuits)} circuit shapes, {len(destinations)} destinations, {len(origins)} origins, {len(turnout_indicators)} turnout indicators, {len(train_labels)} train labels: {svg_path.relative_to(ROOT)}, {data_path.relative_to(ROOT)}, {public_path.relative_to(ROOT)}, {layout_path.relative_to(ROOT)}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("diagram_ids", nargs="*", help="Station or section IDs, for example NT01 NT01_NT02")
    parser.add_argument("--all", action="store_true", help="Export every named draw.io diagram")
    args = parser.parse_args()
    if args.all and args.diagram_ids:
        parser.error("Choose --all or explicit diagram IDs.")
    diagram_ids = (
        sorted(path.stem for path in (diagram_dir / "drawio").glob("*.drawio") if DIAGRAM_ID.fullmatch(path.stem))
        if args.all else args.diagram_ids or ["NT01"]
    )
    for diagram_id in diagram_ids:
        if not DIAGRAM_ID.fullmatch(diagram_id):
            parser.error(f"Diagram ID must have the form NT01 or NT01_NT02: {diagram_id}")
        export(diagram_id)


if __name__ == "__main__":
    main()
