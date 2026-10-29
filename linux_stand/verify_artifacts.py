"""Independent sample verifier: dxfgrabber + raw ASCII tag scan + Pillow.

This module does not import ezdxf, the DXF writer, or the geometry generator.
It checks the particular 10 mm, 1:1 sample against a separately saved golden
specification. It is not a standards or nanoCAD compatibility check.
"""
from __future__ import annotations

import argparse
from collections import Counter
from importlib.metadata import version
import json
import math
from pathlib import Path
import sys
from typing import Iterable

import dxfgrabber
import numpy as np
from PIL import Image

HERE = Path(__file__).resolve().parent


class VerificationError(Exception):
    pass


def require(condition: bool, message: str) -> None:
    if not condition:
        raise VerificationError(message)


def close(actual: float, expected: float, label: str) -> None:
    require(math.isclose(actual, expected, rel_tol=0, abs_tol=1e-8),
            f"{label}: expected {expected}, got {actual}")


def point(actual: Iterable[float], expected: Iterable[float], label: str) -> None:
    a, b = list(actual), list(expected)
    require(len(a) == len(b), f"{label}: expected {len(b)} coordinates, got {len(a)}")
    for index, (x, y) in enumerate(zip(a, b)):
        close(x, y, f"{label}[{index}]")


def points(actual: Iterable[Iterable[float]], expected: Iterable[Iterable[float]], label: str) -> None:
    a, b = list(actual), list(expected)
    require(len(a) == len(b), f"{label}: expected {len(b)} vertices, got {len(a)}")
    for index, (x, y) in enumerate(zip(a, b)):
        point(x, y, f"{label}[{index}]")


def scan_entity_tags(path: Path, target_block: str) -> tuple[Counter, Counter]:
    """Count raw entity tags in ENTITIES and one BLOCK without dropping types.

    This deliberately minimal scanner is for ASCII DXF group-code pairs only;
    it complements dxfgrabber, which silently omits some types such as HATCH.
    """
    with path.open("r", encoding="utf-8-sig", newline=None) as stream:
        lines = stream.readlines()
    require(len(lines) % 2 == 0, "ASCII DXF has an incomplete group-code pair")
    tags = []
    for i in range(0, len(lines), 2):
        try:
            group = int(lines[i].strip())
        except ValueError as exc:
            raise VerificationError(f"invalid group code at line {i + 1}") from exc
        tags.append((group, lines[i + 1].rstrip("\r\n")))
    section = None
    awaiting_section = False
    reading_block = False
    block_name = None
    seen_target = 0
    model: Counter[str] = Counter()
    block: Counter[str] = Counter()
    for group, value in tags:
        if group == 0 and value == "SECTION":
            awaiting_section = True
            continue
        if awaiting_section and group == 2:
            section = value
            awaiting_section = False
            continue
        if group == 0 and value == "ENDSEC":
            section, reading_block, block_name = None, False, None
            continue
        if section == "ENTITIES" and group == 0:
            model[value] += 1
        elif section == "BLOCKS":
            if group == 0 and value == "BLOCK":
                reading_block, block_name = True, None
            elif reading_block and block_name is None and group == 2:
                block_name = value
                if block_name == target_block:
                    seen_target += 1
            elif group == 0 and value == "ENDBLK":
                reading_block, block_name = False, None
            elif group == 0 and reading_block and block_name == target_block:
                block[value] += 1
    require(seen_target == 1, f"raw DXF contains {seen_target} blocks named {target_block!r}")
    return model, block


def verify_dxf(path: Path, spec: dict) -> list[str]:
    messages = []
    require(path.is_file(), f"DXF not found: {path}")
    raw_model, raw_block = scan_entity_tags(path, spec["block_name"])
    require(raw_model == Counter(spec["model_counts"]), f"raw model entity counts: {dict(raw_model)}")
    require(raw_block == Counter(spec["block_counts"]), f"raw block entity counts: {dict(raw_block)}")
    messages.append(f"PASS raw ASCII entity scan: model={dict(raw_model)}; block={dict(raw_block)}")

    drawing = dxfgrabber.readfile(str(path))
    require(drawing.dxfversion == spec["dxf_version"], f"DXF version: {drawing.dxfversion}")
    require(drawing.header.get("$INSUNITS") == spec["insunits"], f"DXF units: {drawing.header.get('$INSUNITS')}")
    actual_model = Counter(entity.dxftype for entity in drawing.entities)
    require(actual_model == Counter(spec["model_counts"]), f"dxfgrabber model counts: {dict(actual_model)}")
    require(len(drawing.entities) == 1, "expected exactly one model INSERT")
    insert = drawing.entities[0]
    expected_insert = spec["insert"]
    require(insert.name == spec["block_name"], f"unexpected INSERT block: {insert.name}")
    require(insert.layer == expected_insert["layer"], f"unexpected INSERT layer: {insert.layer}")
    require(not insert.paperspace, "INSERT is not in model space")
    point(insert.insert, expected_insert["position"], "INSERT position")
    point(insert.scale, expected_insert["scale"], "INSERT scale")
    close(insert.rotation, expected_insert["rotation"], "INSERT rotation")
    messages.append(f"PASS dxfgrabber {version('dxfgrabber')}: DXF {drawing.dxfversion}, INSUNITS=4 (mm), one model INSERT at {insert.insert}, scale={insert.scale}, rotation={insert.rotation:g}°")

    layer_by_name = {item.name: item for item in drawing.layers}
    for name, aci in spec["layer_aci"].items():
        require(name in layer_by_name, f"layer missing: {name}")
        require(layer_by_name[name].color == aci, f"layer {name} ACI: {layer_by_name[name].color}, expected {aci}")
    blocks = [b for b in drawing.blocks if b.name == spec["block_name"]]
    require(len(blocks) == 1, f"expected one block definition, got {len(blocks)}")
    block = blocks[0]
    point(block.basepoint, [0, 0, 0], "block base point")
    entities = list(block)
    type_counts = Counter(e.dxftype for e in entities)
    layer_counts = Counter(e.layer for e in entities)
    require(type_counts == Counter(spec["block_counts"]), f"dxfgrabber block counts: {dict(type_counts)}")
    require(layer_counts == Counter(spec["layer_counts"]), f"block layer counts: {dict(layer_counts)}")
    messages.append(f"PASS block: {len(entities)} primitives, types={dict(type_counts)}, layers={dict(layer_counts)}")

    background = [e for e in entities if e.layer == "EVAC_BACKGROUND"]
    require(len(background) == 1 and background[0].dxftype == "SOLID", "expected one background SOLID")
    require(background[0].true_color == spec["background"]["true_color"], "background true color mismatch")
    points(background[0].points, spec["background"]["vertices"], "background SOLID")
    # The golden INSERT has no rotation and uniform x/y scale; check resulting model bounds.
    world_x = [insert.insert[0] + v[0] * insert.scale[0] for v in background[0].points]
    world_y = [insert.insert[1] + v[1] * insert.scale[1] for v in background[0].points]
    world_bounds = [min(world_x), min(world_y), max(world_x), max(world_y)]
    point(world_bounds, spec["background"]["world_bounds"], "carrier model bounds")
    messages.append(f"PASS carrier: green #008855, local vertices 0..1, model bounds {world_bounds} mm")

    pictogram = [e for e in entities if e.layer == "EVAC_PICTOGRAM"]
    require(all(e.true_color == spec["foreground_true_color"] for e in pictogram), "pictogram true color mismatch")
    actual_polylines = [e for e in pictogram if e.dxftype == "LWPOLYLINE"]
    require(len(actual_polylines) == len(spec["polylines"]), "polyline count mismatch")
    for i, (entity, expected) in enumerate(zip(actual_polylines, spec["polylines"])):
        require(entity.is_closed == expected["closed"], f"polyline[{i}] closed mismatch")
        close(entity.const_width, expected["width"], f"polyline[{i}] width")
        points(entity.points, expected["points"], f"polyline[{i}] points")
    messages.append("PASS pictogram: all seven polyline vertex sequences, widths, closure and true color")

    circle = [e for e in pictogram if e.dxftype == "CIRCLE"]
    require(len(circle) == 1, "expected one exact circle outline")
    head = spec["head"]
    point(circle[0].center, head["center"], "head circle center")
    close(circle[0].radius, head["radius"], "head circle radius")
    solids = [e for e in pictogram if e.dxftype == "SOLID"]
    head_solids = [e for e in solids if all(math.isclose(e.points[0][i], head["center"][i], abs_tol=1e-8, rel_tol=0) for i in range(3))]
    body_solids = [e for e in solids if e not in head_solids]
    require(len(head_solids) == head["triangles"], f"head triangle count: {len(head_solids)}")
    for i, entity in enumerate(head_solids):
        a = 2 * math.pi * i / head["triangles"]
        b = 2 * math.pi * (i + 1) / head["triangles"]
        x, y, _ = head["center"]
        expected = [head["center"], [x + head["radius"] * math.cos(a), y + head["radius"] * math.sin(a), 0],
                    [x + head["radius"] * math.cos(b), y + head["radius"] * math.sin(b), 0]]
        points(entity.points, expected + [expected[2]], f"head triangle[{i}]")
    require(len(body_solids) == len(spec["body_triangles"]), f"body triangle count: {len(body_solids)}")
    for i, (entity, expected) in enumerate(zip(body_solids, spec["body_triangles"])):
        points(entity.points, expected + [expected[2]], f"body triangle[{i}]")
    messages.append(f"PASS filled geometry: exact CIRCLE center/radius, {len(head_solids)} head triangles and {len(body_solids)} body triangles with every vertex checked")

    annotation = [e for e in entities if e.layer == "EVAC_ANNOTATION"]
    require(all(e.true_color == spec["annotation_true_color"] for e in annotation), "annotation true color mismatch")
    line = [e for e in annotation if e.dxftype == "LINE"]
    text = [e for e in annotation if e.dxftype == "TEXT"]
    require(len(line) == len(text) == 1, "expected one annotation LINE and TEXT")
    point(line[0].start, spec["line"]["start"], "annotation LINE start")
    point(line[0].end, spec["line"]["end"], "annotation LINE end")
    require(text[0].text == spec["text"]["value"], f"unexpected TEXT: {text[0].text!r}")
    point(text[0].insert, spec["text"]["position"], "TEXT position")
    close(text[0].height, spec["text"]["height"], "TEXT height")
    close(text[0].rotation, spec["text"]["rotation"], "TEXT rotation")
    messages.append(f"PASS annotation: LINE endpoints; TEXT={text[0].text!r}, local height={text[0].height:g}")
    return messages


def verify_png(path: Path, spec: dict) -> list[str]:
    require(path.is_file(), f"PNG not found: {path}")
    with Image.open(path) as image:
        require(image.format == "PNG", f"not a PNG: {image.format}")
        width, height = image.size
        rgba = np.asarray(image.convert("RGB"))
    expected = spec["png"]
    require(width >= expected["min_width"] and height >= expected["min_height"], f"PNG too small: {width}x{height}")
    red, green, blue = rgba[:, :, 0], rgba[:, :, 1], rgba[:, :, 2]
    green_mask = (red < 30) & (green > 100) & (green < 160) & (blue > 60) & (blue < 120)
    green_count = int(green_mask.sum())
    require(green_count >= expected["minimum_green_pixels"], f"not enough green pixels: {green_count}")
    ys, xs = np.where(green_mask)
    x0, x1, y0, y1 = int(xs.min()), int(xs.max()), int(ys.min()), int(ys.max())
    carrier = rgba[y0:y1 + 1, x0:x1 + 1]
    white_count = int(((carrier[:, :, 0] > 240) & (carrier[:, :, 1] > 240) & (carrier[:, :, 2] > 240)).sum())
    require(white_count >= expected["minimum_white_pixels_inside_carrier_bbox"], f"not enough white pixels inside carrier: {white_count}")
    below = rgba[y1 + 1:, :, :]
    dark_count = int(((below[:, :, 0] < 60) & (below[:, :, 1] < 60) & (below[:, :, 2] < 60)).sum())
    require(dark_count >= expected["minimum_dark_pixels_below_carrier"], f"not enough dark annotation pixels: {dark_count}")
    return [f"PASS PNG: {width}x{height}, green={green_count}, white inside carrier bbox={white_count}, dark below={dark_count}; green bbox=({x0}, {y0})..({x1}, {y1})"]


def verify(dxf: Path, png: Path, spec_path: Path) -> list[str]:
    with spec_path.open(encoding="utf-8") as stream:
        spec = json.load(stream)
    return verify_dxf(dxf, spec) + verify_png(png, spec)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Check the default week-one demo against the independent golden specification")
    parser.add_argument("--dxf", type=Path, default=HERE / "artifacts/hello_e01_01.dxf")
    parser.add_argument("--png", type=Path, default=HERE / "artifacts/hello_e01_01.png")
    parser.add_argument("--spec", type=Path, default=HERE / "spec/hello_e01_01_h10.json")
    args = parser.parse_args(argv)
    print(f"DXF: {args.dxf}\nPNG: {args.png}\nGolden spec: {args.spec}")
    try:
        for message in verify(args.dxf, args.png, args.spec):
            print(message)
    except (VerificationError, OSError, ValueError, KeyError) as exc:
        print(f"FAIL: {exc}", file=sys.stderr)
        return 1
    print("PASS independent artifact verification; this is not GOST or nanoCAD validation.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
