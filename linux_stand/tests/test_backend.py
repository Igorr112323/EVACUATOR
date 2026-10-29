from __future__ import annotations

from collections import Counter

import dxfgrabber
import pytest

from linux_stand.cad_backend import ICadBackend
from linux_stand.core.geometry import Sign
from linux_stand.core.primitives import Layer, Point
from linux_stand.dxf_backend import DxfBackend
from linux_stand.export import export_sign


class RecordingBackend(ICadBackend):
    def __init__(self): self.calls = []
    def record(self, name, *args): self.calls.append((name, args))
    def add_layer(self, layer): self.record("layer", layer)
    def add_block(self, name): self.record("block", name)
    def add_line(self, *args): self.record("line", *args)
    def add_polyline(self, *args): self.record("polyline", *args)
    def add_filled_polygon(self, *args): self.record("polygon", *args)
    def add_filled_circle(self, *args): self.record("circle", *args)
    def add_text(self, *args): self.record("text", *args)
    def insert_block(self, *args): self.record("insert", *args)
    def save_document(self, path): self.record("save", path)


def test_backend_contract_driven_by_abstract_core(definition, tmp_path):
    backend = RecordingBackend()
    sign = Sign("E01-01", 12, Point(3, 4), 90)
    export_sign(sign, definition, backend, tmp_path / "unused.dxf")
    assert Counter(name for name, _ in backend.calls) == {
        "layer": 3, "block": 1, "line": 1, "polyline": 7, "polygon": 2,
        "circle": 1, "text": 1, "insert": 1, "save": 1,
    }
    insert = next(args for name, args in backend.calls if name == "insert")
    assert insert == ("EVAC_E01_01", Point(3, 4), 12, 90, "EVAC_BACKGROUND")
    assert backend.calls[-1][0] == "save"


@pytest.mark.parametrize("height, x, y, rotation", [(8, 0, 0, 0), (12, 25, -10, 90), (15, -3, 7, 180)])
def test_dxf_insert_retains_parameters_for_independent_reader(definition, tmp_path, height, x, y, rotation):
    path = tmp_path / "parameters.dxf"
    export_sign(Sign("E01-01", height, Point(x, y), rotation), definition, DxfBackend(), path)
    drawing = dxfgrabber.readfile(str(path))
    assert len(drawing.entities) == 1
    instance = drawing.entities[0]
    assert instance.dxftype == "INSERT"
    assert instance.name == "EVAC_E01_01"
    assert instance.insert == pytest.approx((x, y, 0))
    assert instance.scale == pytest.approx((height, height, 1))
    assert instance.rotation == pytest.approx(rotation)
    block = next(b for b in drawing.blocks if b.name == instance.name)
    bg = next(e for e in block if e.layer == "EVAC_BACKGROUND")
    assert bg.dxftype == "SOLID"
    for actual, expected in zip(bg.points, [(0, 0, 0), (1, 0, 0), (0, 1, 0), (1, 1, 0)], strict=True):
        assert actual == pytest.approx(expected)


def test_dxf_backend_rejects_undefined_block_and_layer():
    backend = DxfBackend()
    with pytest.raises(ValueError, match="undefined block"):
        backend.add_line("MISSING", Point(0, 0), Point(1, 1), "MISSING", "#000000")
    backend.add_block("B")
    with pytest.raises(ValueError, match="undefined layer"):
        backend.add_line("B", Point(0, 0), Point(1, 1), "MISSING", "#000000")


@pytest.mark.parametrize("vertices", [
    [(0, 0), (1, 0), (0.5, 0.3), (1, 1), (0, 1)],  # concave
    [(0, 0), (1, 1), (0, 1), (1, 0)],              # crossed
    [(0, 0), (1, 0), (2, 0)],                       # degenerate
    [(0, 0), (1, 0), (1, 0), (0, 1)],              # repeated vertex
])
def test_dxf_backend_rejects_unsupported_filled_polygons(vertices):
    backend = DxfBackend()
    backend.add_layer(Layer("L", "#FFFFFF", 7))
    backend.add_block("B")
    with pytest.raises(ValueError, match="polygon"):
        backend.add_filled_polygon("B", tuple(Point(*v) for v in vertices), "L", "#FFFFFF")


def test_dxf_backend_rejects_multiline_text():
    backend = DxfBackend()
    backend.add_layer(Layer("L", "#FFFFFF", 7))
    backend.add_block("B")
    with pytest.raises(ValueError, match="single-line"):
        backend.add_text("B", "bad\ntext", Point(0, 0), .1, "L", "#FFFFFF")
