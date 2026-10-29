from __future__ import annotations

from collections import Counter
import json
import subprocess
import sys

import pytest

from linux_stand.core.catalog import DEMO_MARKER, load_demo_definition
from linux_stand.core.geometry import Sign, carrier_bounds, create_sign_block, transform_point
from linux_stand.core.primitives import Point
from linux_stand.tests.conftest import CATALOG, ROOT


def test_catalog_loads_single_unverified_demo(definition):
    assert definition.code == "E01-01"
    assert definition.shape == "Square"
    assert definition.background == "#008855"
    assert (definition.min_height_mm, definition.max_height_mm, definition.default_height_mm) == (8, 15, 10)
    assert DEMO_MARKER in definition.gost_note and DEMO_MARKER in definition.catalog_notice
    assert Counter(type(p).__name__ for p in definition.pictogram) == {"Polyline": 6, "FilledCircle": 1, "FilledPolygon": 1}


def test_catalog_rejects_other_codes():
    with pytest.raises(ValueError, match="only E01-01"):
        load_demo_definition(CATALOG, "E99-99")


@pytest.mark.parametrize("change, reason", [
    ("notice", "notice"),
    ("gost_note", "notice"),
    ("duplicate", "exactly one"),
    ("unknown_primitive", "unsupported recipe primitive"),
    ("out_of_range", "coordinates"),
    ("bad_limits", "minHeightMm"),
    ("bad_color", "backgroundColor"),
    ("wrong_shape", "square"),
])
def test_catalog_rejects_malformed_or_unmarked_demo(tmp_path, change, reason):
    data = json.loads(CATALOG.read_text(encoding="utf-8"))
    sign = next(s for s in data["signs"] if s["code"] == "E01-01")
    if change == "notice": data["notice"] = "no warning"
    if change == "gost_note": sign["gostClauseRef"] = "no warning"
    if change == "duplicate": data["signs"].append(sign)
    if change == "unknown_primitive": sign["primitives"][0]["type"] = "ImaginaryShape"
    if change == "out_of_range": sign["primitives"][0]["points"][0]["x"] = 1.01
    if change == "bad_limits": sign["minHeightMm"] = 20
    if change == "bad_color": sign["backgroundColor"] = "green"
    if change == "wrong_shape": sign["shape"] = "CircleSlash"
    path = tmp_path / "invalid.json"
    path.write_text(json.dumps(data, ensure_ascii=False), encoding="utf-8")
    with pytest.raises(ValueError, match=reason):
        load_demo_definition(path)


def test_core_imports_without_any_cad_or_render_library():
    script = (
        "import linux_stand.core.catalog, linux_stand.core.geometry, linux_stand.core.normcontrol; "
        "import sys; banned=('ezdxf','dxfgrabber','matplotlib','PIL'); "
        "assert not [m for m in sys.modules if m.split('.')[0] in banned]"
    )
    result = subprocess.run([sys.executable, "-c", script], cwd=ROOT, capture_output=True, text=True)
    assert result.returncode == 0, result.stdout + result.stderr


def test_abstract_block_contains_expected_primitives(definition):
    block = create_sign_block(definition)
    assert block.name == "EVAC_E01_01"
    assert [layer.name for layer in block.layers] == ["EVAC_BACKGROUND", "EVAC_PICTOGRAM", "EVAC_ANNOTATION"]
    assert Counter(type(p).__name__ for p in block.primitives) == {
        "FilledPolygon": 2, "Polyline": 7, "FilledCircle": 1, "Line": 1, "Text": 1
    }


def test_carrier_bounds_and_transform_identity():
    sign = Sign("E01-01", 10)
    bounds = carrier_bounds(sign)
    assert (bounds.min_x, bounds.min_y, bounds.max_x, bounds.max_y) == pytest.approx((0, 0, 10, 10))
    p = transform_point(Point(.54, .73), sign)
    assert (p.x, p.y) == pytest.approx((5.4, 7.3))


def test_rotated_translated_carrier_bounds():
    sign = Sign("E01-01", 12, Point(25, -10), 90)
    bounds = carrier_bounds(sign)
    assert (bounds.min_x, bounds.min_y, bounds.max_x, bounds.max_y) == pytest.approx((13, -10, 25, 2))
    point = transform_point(Point(1, 1), sign)
    assert (point.x, point.y) == pytest.approx((13, 2))


@pytest.mark.parametrize("height", [0, -1, float("nan"), float("inf"), -float("inf"), True, "10"])
def test_sign_rejects_nonpositive_or_nonfinite_height(height):
    with pytest.raises(ValueError):
        Sign("E01-01", height)


@pytest.mark.parametrize("scale", [0, -1, float("nan"), float("inf"), True])
def test_sign_rejects_invalid_paper_scale(scale):
    with pytest.raises(ValueError):
        Sign("E01-01", 10, paper_scale_denominator=scale)


@pytest.mark.parametrize("angle", [float("nan"), float("inf"), -float("inf")])
def test_sign_rejects_nonfinite_rotation(angle):
    with pytest.raises(ValueError):
        Sign("E01-01", 10, rotation_degrees=angle)


def test_point_rejects_nonfinite_coordinates():
    with pytest.raises(ValueError):
        Point(float("nan"), 0)
