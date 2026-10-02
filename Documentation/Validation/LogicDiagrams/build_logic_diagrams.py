#!/usr/bin/env python3
"""Create/check direct-call draw.io diagrams; C# parsing uses tree-sitter.

python -m pip install tree-sitter==0.21.3 tree-sitter-languages==1.10.2
python build_logic_diagrams.py /path/to/Nakatetsu --check
Only --write creates diagrams/index/report. Existing ATC/Interlocking diagrams stay intact.
"""
import argparse
import hashlib
import html
import json
import re
import subprocess
import warnings
import xml.etree.ElementTree as ET
from pathlib import Path

warnings.filterwarnings("ignore", category=FutureWarning)
from tree_sitter_languages import get_parser

BASELINE = {
    "TrackAtcLogic": "Documentation/Track/Atc/TrackAtcStateTransitions.drawio",
    "TrackInterlockingLogic": "Documentation/Track/Interlocking/TrackInterlockingLogicCalls.drawio",
}
METHOD_NOTES = {
    "Calculate": "入力を照査し、計算結果を更新",
    "TryInitialize": "定義を検証し実行時データを初期化",
    "RangesOverlap": "境界一致を含めて範囲重複を判定",
    "TryRequestPosition": "転換要求を受け付け、実位置は未確定",
    "TryConfirmPosition": "要求に合う完了通知で位置を確定",
    "TryResolveNextEdge": "確定位置から接続先Edgeを取得",
    "IsConfirmedPosition": "Normal / Reverseだけを確定位置とする",
    "Fail": "エラー文を返しfalseで終了",
    "Clamp": "値を指定範囲へ制限",
    "Resolve": "revisionと開許可から左右の指令を決定",
    "ConfigureLimits": "力行・制動段数の上限を設定",
    "Initialize": "初期状態を設定して範囲を補正",
    "SetPowerPosition": "力行段を設定し、正なら制動をゼロ",
    "SetBrakePosition": "制動段を設定し、正なら力行をゼロ",
    "SetServiceBrakePosition": "常用上限内で制動段を設定",
    "SetEmergencyBrake": "非常ブレーキ位置を設定",
    "SetNeutral": "力行・制動位置をゼロにする",
    "TrySetReverserPosition": "許可条件を照査して逆転器位置を変更",
    "MoveOneStepTowardBrake": "力行を戻す、または制動を1段増やす",
    "MoveOneStepTowardPower": "制動を戻す、または力行を1段増やす",
    "StepTowardNeutral": "現在の制動または力行を1段戻す",
    "StepTowardServiceMaxBrake": "力行を戻し、常用最大まで制動を増やす",
    "Normalize": "設定・状態の範囲と排他を補正",
    "ThrowIfNull": "Context欠損を例外として検出",
    "Configure": "位置範囲とばね復帰設定を正規化",
    "SetPosition": "範囲内に位置を設定し変更有無を返す",
    "MoveOneStep": "方向の符号に従い位置を1段変更",
    "Release": "ばね復帰設定があれば復帰位置へ変更",
    "ClampToRange": "設定された最小〜最大位置に制限",
    "HasStateChanged": "マスコン操作の変化を照査",
    "Reset": "非常ラッチ・無操作時間・出力をリセット",
    "InitializeWorkSpace": "制動配分の作業領域を消去",
    "CalculateTargetDeceleration": "制動段から目標減速度を計算",
    "CalculateTargetTotalBrakeForce": "総質量×目標減速度から必要力を計算",
    "CalculateMinimumAirBrakePressure": "常用制動の最低込め圧・力を計算",
    "CalculateRemainingTargetBrakeForce": "最低込め分を差し引いた残余力を計算",
    "CalculateOutput": "空制力・圧力の上限を反映して指令を生成",
    "CalculateTargetCarBrakeForces": "残余制動力を車両質量比で配分",
    "CalculateTargetRegenForces": "回生目標を飽和付きで均等配分",
    "CalculateAdditionalAirBrakeForces": "実回生・余剰回生から追加空制を計算",
    "ShouldCollectSources": "シミュレーション時間から収集時機を判定",
    "CollectFloatFromCars": "端末ごとの値と取得有無を返す",
    "HasTerminal": "対象号車に使用可能な端末があるか照査",
    "FormatManualBrakeNotch": "手動制動段の表示文字列を生成",
    "ResolveBrakeNotch": "手動・ATCの制動段と非常要求を調停",
    "ResolvePowerNotch": "制動による力行抑止後に手動力行段を決定",
    "CalculateConstantAccelerationEndSpeedMps": "定格出力 / (質量×加速度)を計算",
    "AreAllBCReleased": "BC緩解または勾配起動条件を照査",
    "DistributeTargetForce": "利用可能VVVFへ目標力を均等配分",
    "UpdateSlipFrequency": "トルク偏差と上限から滑り周波数を更新",
    "GetSpeedLimitedMaximumSlipFrequencyHz": "車速・起動条件を反映した滑り上限",
    "UpdateThreePhaseWave": "位相を進めて三相電圧を生成",
    "GetWheelRpm": "車速・車輪半径から車輪回転数を計算",
    "CalculateTargetMotorTorqueNm": "目標力をモータートルクに換算",
    "ResolveDriveMode": "目標力の符号・しきい値から運転モードを決定",
    "MoveTowards": "最大変化量の範囲で目標に近づける",
    "GetTravelTimeSeconds": "号車・側・戸ごとの決定的な開閉時間",
    "Step": "指令・時間・故障から開度とドア状態を更新",
    "IsFinite": "NaN / Infinityを除外",
    "ApplyOutput": "計算OutputをStateへ反映",
    "TryApplyBrakeHold": "停止付近での制動保持を判定・適用",
    "CalculateAcceleration": "非制動力・抵抗・制動力から加速度を計算",
    "CalculateRunningResistanceForceN": "走行方向または停止条件から抵抗力を計算",
    "CalculateGradeForceN": "質量と勾配から勾配力を計算",
    "CalculateVelocity": "速度・変位を積分し停止時の符号跨ぎを抑止",
    "CaptureOutPut": "速度・加速度・変位をOutputへ写す",
    "TryLocateOffset": "基準位置からoffset先のEdgeと距離を探索",
    "TryGetAdjacent": "接続resolverから隣接Edge・進入方向を取得",
    "IsValidEdge": "Edgeと正の有限長を照査",
    "AdvanceEdgeIfNeeded": "越境時にEdge・距離・向きを更新",
    "ConfigureLayout": "車両・台車の相対offsetを設定",
    "TryGetTrackSample": "車上の指定位置の線路サンプルを取得",
    "RefreshOutput": "全車の台車サンプルと占有範囲を再構築",
    "TryBuildOccupiedEdges": "先頭〜最後尾台車間の占有Edgeを構築",
    "GetSynchronousRpm": "周波数と極数から同期回転数を計算",
    "GetFrequencyFromSynchronousRpm": "同期回転数と極数から周波数を計算",
    "GetSlipRatio": "同期回転数と実回転数から滑り率を計算",
    "GetAngularSpeedRadS": "rpmを角速度に換算",
    "GetTorqueFromPowerAndRpm": "出力と回転数からトルクを計算",
    "CalculateParallelImpedance": "並列インピーダンスを計算",
    "ClearElectricalOutput": "トルク・電流・電力出力をゼロにする",
    "ResolveMotion": "実測力・有効性から表示用運転状態を判定",
    "ResolveDirection": "有効な運転台と逆転器から方向符号を取得",
    "FormatCarNumber": "有効な号車番号を全角数字に変換",
}


def walk(node):
    yield node
    for child in node.children:
        yield from walk(child)


def analyze(path, root, parser):
    raw = path.read_bytes()
    tree = parser.parse(raw)
    if tree.root_node.has_error:
        raise ValueError(f"C# parse error: {path}")
    text = lambda n: raw[n.start_byte:n.end_byte].decode("utf-8")
    cls = next(n for n in walk(tree.root_node) if n.type == "class_declaration"
               and text(n.child_by_field_name("name")) == path.stem)
    declarations = [n for n in cls.child_by_field_name("body").children
                    if n.type == "method_declaration"]
    names = {text(n.child_by_field_name("name")) for n in declarations}
    methods, calls, external = [], set(), set()
    for method in declarations:
        name = text(method.child_by_field_name("name"))
        modifiers = [text(n) for n in method.children if n.type == "modifier"]
        predicates, mutations = [], []
        for node in walk(method):
            if node.type in ("if_statement", "while_statement", "for_statement", "conditional_expression"):
                condition = node.child_by_field_name("condition")
                if condition:
                    predicates.append({"line": condition.start_point[0] + 1, "code": text(condition)})
            if node.type in ("assignment_expression", "postfix_unary_expression", "prefix_unary_expression"):
                # Keep exact expressions, including local variables and ref aliases.
                # These are code references, not inferred state transitions.
                if node.type != "prefix_unary_expression" or text(node).startswith(("++", "--")):
                    mutations.append({"line": node.start_point[0] + 1, "code": text(node)})
            if node.type != "invocation_expression":
                continue
            function = node.child_by_field_name("function")
            if function is None:
                continue
            target = text(function)
            if function.type in ("identifier", "generic_name"):
                short = target.split("<")[0]
                if short in names:
                    calls.add((name, short))
            elif function.type == "member_access_expression":
                receiver = text(function.child_by_field_name("expression"))
                called = text(function.child_by_field_name("name")).split("<")[0]
                if receiver in (path.stem, "this") and called in names:
                    calls.add((name, called))
                elif re.search(r"(?:Logic|Calculator|Validator)$", receiver) or receiver == "TimsMath":
                    external.add((name, receiver + "." + called))
        methods.append({"name": name, "line": method.start_point[0] + 1,
                        "endLine": method.end_point[0] + 1,
                        "public": "public" in modifiers, "modifiers": modifiers,
                        "conditions": predicates, "assignments": mutations})
    return {"class": path.stem, "source": str(path.relative_to(root)),
            "sourceSha256": hashlib.sha256(raw).hexdigest(), "methods": methods,
            "calls": sorted(calls), "externalCalls": sorted(external)}


def diagram_path(source):
    parent = Path(source).parent
    parts = list(parent.parts[2:])
    if parts[-1] == "Scripts":
        parts.pop()
    # Interlocking has scripts directly in its device folder; exclude filename already.
    return str(Path("Documentation", *parts, Path(source).stem + "Calls.drawio"))


def cell(parent, cid, value, style, x, y, width, height, **attrs):
    n = ET.SubElement(parent, "mxCell", id=cid, value=value, style=style,
                      vertex="1", parent="1", **attrs)
    ET.SubElement(n, "mxGeometry", x=str(x), y=str(y), width=str(width), height=str(height), **{"as": "geometry"})
    return n


def wrapped_identifier(name):
    return re.sub(r"(?<=[a-z0-9])(?=[A-Z])", "<wbr>", html.escape(name))


def write_diagram(data, meta, target):
    mxfile = ET.Element("mxfile", host="Electron", agent="Nakatetsu Logic diagram generator")
    diagram = ET.SubElement(mxfile, "diagram", id=data["class"] + "-calls", name="関数呼び出し")
    model = ET.SubElement(diagram, "mxGraphModel", grid="1", gridSize="10", page="1",
                          pageWidth="1600", pageHeight="1000", pageScale="1", background="#ffffff")
    root = ET.SubElement(model, "root")
    ET.SubElement(root, "mxCell", id="0")
    ET.SubElement(root, "mxCell", id="1", parent="0")
    font = "fontFamily=Noto Sans JP;fontColor=#222222;"
    cell(root, "title", html.escape(data["class"]) + "｜関数呼び出し", "text;html=1;align=left;fontSize=25;" + font,
         40, 24, 1700, 42)
    cell(root, "legend", "青：公開API　白：内部関数　橙：別Logic/計算補助への直接呼び出し<br>実線・破線：呼び出し元 → 呼び出し先（実行順・状態遷移ではない）",
         "text;html=1;align=left;fontSize=13;" + font, 40, 78, 1700, 42)
    cell(root, "source", html.escape(data["source"]), "text;html=1;align=left;fontSize=12;" + font,
         40, 126, 1750, 24)
    base = "rounded=1;arcSize=10;whiteSpace=wrap;html=1;align=left;verticalAlign=middle;spacingLeft=16;spacingRight=12;strokeColor=#222222;strokeWidth=1.5;" + font
    for i, method in enumerate(data["methods"]):
        name = method["name"]
        purpose = meta["title"] + "の更新・計算" if name == "Calculate" else METHOD_NOTES.get(name, "実装の計算・判定")
        label = (f'<b style="font-size:15px">{wrapped_identifier(name)}</b><br>'
                 f'<span style="font-size:13px">{html.escape(purpose)}</span><br>'
                 f'<span style="font-size:11px;color:#666666">L{method["line"]}–{method["endLine"]}</span>')
        cell(root, name, label, base + "fillColor=" + ("#dae8fc;" if method["public"] else "#ffffff;"),
             40 + (i % 3) * 400, 190 + (i // 3) * 150, 330, 104,
             function=name, sourceLine=str(method["line"]))
    external_targets = sorted({t for _, t in data["externalCalls"]})
    for i, name in enumerate(external_targets):
        cls, method = name.rsplit(".", 1)
        label = f'<b style="font-size:14px">{wrapped_identifier(cls)}<br>{wrapped_identifier(method)}</b><br><span style="font-size:12px">別Logic / 計算補助</span>'
        cell(root, "external:" + name, label, base + "fillColor=#fff2cc;", 1300, 190 + i * 130, 350, 104, externalFunction=name)
    edges = [(s, t, False) for s, t in data["calls"]] + [(s, "external:" + t, True) for s, t in data["externalCalls"]]
    for i, (source, target_id, is_external) in enumerate(edges):
        style = "edgeStyle=orthogonalEdgeStyle;rounded=0;jumpStyle=arc;jumpSize=8;html=1;endArrow=block;endFill=1;strokeColor=#222222;strokeWidth=1.5;"
        if is_external:
            style += "dashed=1;"
        edge = ET.SubElement(root, "mxCell", id="call-" + str(i), parent="1", edge="1", source=source, target=target_id, style=style)
        ET.SubElement(edge, "mxGeometry", relative="1", **{"as": "geometry"})
    target.parent.mkdir(parents=True, exist_ok=True)
    ET.indent(mxfile)
    ET.ElementTree(mxfile).write(target, encoding="utf-8", xml_declaration=True)


def verify(data, target, baseline):
    xml = ET.parse(target).getroot()
    all_methods, calls, ext = set(), set(), set()
    for page in xml.findall("diagram"):
        cells = page.findall("./mxGraphModel/root/mxCell")
        byid = {c.get("id"): c for c in cells}
        for c in cells:
            if c.get("function"):
                all_methods.add(c.get("function"))
            if c.get("edge") != "1":
                continue
            a, b = byid[c.get("source")], byid[c.get("target")]
            if a.get("function") and b.get("function"):
                calls.add((a.get("function"), b.get("function")))
            if a.get("function") and b.get("externalFunction"):
                ext.add((a.get("function"), b.get("externalFunction")))
    expected_methods = {m["name"] for m in data["methods"]}
    expected_calls, expected_ext = set(map(tuple, data["calls"])), set(map(tuple, data["externalCalls"]))
    if all_methods != expected_methods or calls != expected_calls or (not baseline and ext != expected_ext):
        raise AssertionError({"diagram": str(target), "missingMethods": sorted(expected_methods - all_methods),
                              "extraMethods": sorted(all_methods - expected_methods),
                              "missingCalls": sorted(expected_calls - calls), "extraCalls": sorted(calls - expected_calls),
                              "missingExternalCalls": sorted(expected_ext - ext), "extraExternalCalls": sorted(ext - expected_ext)})
    return {"methods": len(all_methods), "internalCallPairs": len(calls),
            "externalCallPairs": len(ext), "existingDiagram": baseline,
            "externalChecked": not baseline, "result": "passed"}


def make_index(root, results, excluded, revision):
    path = root / "Documentation/Architecture/LogicDiagrams.md"
    lines = ["# Logicの関数呼び出し図", "", f"照合した実装: `{revision}`。", "",
             "地上ATC・Interlockingの既存draw.ioと同じく、箱は関数、矢印は直接呼び出しを表す。状態遷移図や実行順序図ではない。公開APIを青、内部関数を白、別Logic/Calculator/Validatorへの明示的な呼び出しを橙・破線で示す。呼び出し回数はまとめ、同じ呼び出し元/先の組を1本にする。条件・状態更新は下の実装メモと機械抽出結果に記録する。", "",
             "対象は実装のある全 `*Logic.cs`。Controller・View・Unity/UI描画、Editor・テスト・Definition・Contextは図にしない。`TrainStatusDisplayLogic`はPresentation名前空間だが、純粋な判定・文字列変換のみを持つLogicなので含める。表示コンポーネントのライフサイクルやUI描画へは範囲を広げない。TIMSのSpeed/Door/Safety等は独立Logicではなく既存Logicを使う接続・Adapterが中心のため、そのController図は作らない。", "",
             "計算だけの装置に状態機械を仮定しない。別装置の入力はContext経由で受けるため、入力データの流れを関数呼び出しの矢印へ読み替えない。注入された接続resolver・interfaceの実装先や、Controllerからの呼び出し順も推測しない。標準ライブラリ、Math/Mathf、Context/State/Outputへのアクセスは外部関数箱としては載せない。", "",
             "## 対象一覧", "", "|Logic|図|方法|", "|---|---|---|"]
    for r in results:
        link = Path("../") / Path(r["diagram"]).relative_to("Documentation")
        lines.append(f'|`{r["class"]}`|[{"既存" if r["existingDiagram"] else "追加"}]({link.as_posix()})|{r["methods"]}関数・{r["internalCallPairs"]}内部呼び出し組|')
    lines += ["", "## 図を作らない空実装", "", "|ファイル|理由|", "|---|---|"]
    for x in excluded:
        lines.append(f'|`{x["source"]}`|{x["reason"]}|')
    lines += ["", "荷重計には空のLogicファイルしかなく、独立した処理を捏造しない。編成Simulationの空Logicも同様で、Controllerのオーケストレーションを代わりに図へ入れない。", "", "## 条件分岐・状態更新の実装メモ", ""]
    for r in results:
        lines += [f'### {r["class"]}', "", f'ソース: [`{r["class"]}.cs`](../../{r["source"]})', ""]
        if "notes" in r:
            lines.append(f'保持・計算対象: {r["kind"]}。')
            lines.append("")
            lines.extend("- " + note for note in r["notes"])
        else:
            lines.append("既存図を保持。C#宣言された全関数とクラス内直接呼び出しの組を今回の検証で再照合した。詳細の分岐・代入式は検証JSONを参照。")
        lines.append("")
    lines += ["## 再検証", "", "必要な解析環境は `tree-sitter==0.21.3` と `tree-sitter-languages==1.10.2`。C#の構文木から宣言と呼び出しを抽出するため、コメントや文字列内の関数名を誤って呼び出しと数えない。", "", "```sh", "python Documentation/Validation/LogicDiagrams/build_logic_diagrams.py . --check", "```", "", "[`CodeCheck.json`](../Validation/LogicDiagrams/CodeCheck.json)にソースSHA-256、全関数・全内部呼び出し組、明示的な外部Logic/計算補助呼び出し、実装の条件式と代入式を記録する。代入式にはローカル変数も含まれる。これは状態更新だけを抽出したものではなく、コード照合のための原文参照である。外部Logic等の同名関数の宣言存在も確認する。既存2図は既存形式どおり内部呼び出しだけを照合し、外部呼び出しの図示を後付けしない。", "", "描画はdraw.ioで使われるmxGraph 4.2.2とChromeで確認する。再描画には同フォルダーの `render_logic_diagrams.cjs` と `mxgraph@4.2.2`、`puppeteer-core` が必要。", "", "```sh", "node Documentation/Validation/LogicDiagrams/render_logic_diagrams.cjs /path/to/repository /path/to/node_modules /path/to/Google\\ Chrome /tmp/logic-diagram-render", "```", "", "チェック結果は [`RenderCheck.json`](../Validation/LogicDiagrams/RenderCheck.json)。描画画像はPRに重複収録しない。解析・描画の検証であり、Unityの動作検証や装置のテスト実行ではない。", ""]
    path.write_text("\n".join(lines), encoding="utf-8")


def main():
    cli = argparse.ArgumentParser(description=__doc__)
    cli.add_argument("root", type=Path)
    cli.add_argument("--write", action="store_true")
    cli.add_argument("--check", action="store_true")
    cli.add_argument("--catalog", type=Path, default=Path(__file__).with_name("diagram_catalog.json"))
    args = cli.parse_args()
    if args.write == args.check:
        cli.error("Choose exactly one of --write or --check")
    root = args.root.resolve()
    catalog = json.loads(args.catalog.read_text())
    parser = get_parser("c_sharp")
    results, excluded = [], []
    declarations = {}
    # Explicit cross-class targets also need a declaration in the source tree.
    for source in (root / "Assets/Nakatetsu").rglob("*.cs"):
        if "/Tests/" in str(source):
            continue
        raw = source.read_bytes()
        tree = parser.parse(raw)
        for cls in (n for n in walk(tree.root_node) if n.type == "class_declaration"):
            name_node = cls.child_by_field_name("name")
            name = raw[name_node.start_byte:name_node.end_byte].decode()
            methods = cls.child_by_field_name("body")
            if methods:
                declarations.setdefault(name, set()).update(raw[n.child_by_field_name("name").start_byte:n.child_by_field_name("name").end_byte].decode()
                    for n in methods.children if n.type == "method_declaration")
    for source in sorted((root / "Assets/Nakatetsu").rglob("*Logic.cs")):
        if not source.read_bytes().strip():
            excluded.append({"source": str(source.relative_to(root)), "reason": "空ファイル。関数・状態更新の実装なし"})
            continue
        data = analyze(source, root, parser)
        for _, target in data["externalCalls"]:
            cls, method = target.rsplit(".", 1)
            if method not in declarations.get(cls.rsplit(".", 1)[-1], set()):
                raise AssertionError(f"Undeclared external target: {target}")
        baseline = data["class"] in BASELINE
        target_path = BASELINE.get(data["class"], diagram_path(data["source"]))
        if args.write and not baseline:
            write_diagram(data, catalog[data["class"]], root / target_path)
        checked = verify(data, root / target_path, baseline)
        results.append({**data, **checked, "methodsDetail": data["methods"], "diagram": target_path,
                        **catalog.get(data["class"], {})})
    if set(catalog) != {r["class"] for r in results if not r["existingDiagram"]}:
        raise AssertionError("Catalog must cover exactly all non-baseline Logic classes")
    revision = subprocess.check_output(["git", "-C", str(root), "rev-parse", "HEAD"], text=True).strip()
    report = {"sourceCommit": revision, "logicFileCount": len(results) + len(excluded),
              "diagramCount": len(results), "newDiagramCount": sum(not r["existingDiagram"] for r in results),
              "excluded": excluded, "results": results}
    if args.write:
        directory = root / "Documentation/Validation/LogicDiagrams"
        directory.mkdir(parents=True, exist_ok=True)
        (directory / "CodeCheck.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
        make_index(root, results, excluded, revision)
    else:
        saved = json.loads((root / "Documentation/Validation/LogicDiagrams/CodeCheck.json").read_text())
        # The PR documentation commit changes HEAD; code hashes remain the source of truth.
        saved.pop("sourceCommit")
        comparable = json.loads(json.dumps(report))
        comparable.pop("sourceCommit")
        if comparable != saved:
            raise AssertionError("CodeCheck.json is stale; inspect changed source/diagrams")
    print(json.dumps({k: report[k] for k in ("sourceCommit", "logicFileCount", "diagramCount", "newDiagramCount", "excluded")}, ensure_ascii=False))


if __name__ == "__main__":
    main()
