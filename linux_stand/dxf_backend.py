"""A real DXF writer using ezdxf. DXF SOLIDs keep this sample readable by dxfgrabber."""
from __future__ import annotations

import math
from pathlib import Path

import ezdxf

from .cad_backend import ICadBackend
from .core.primitives import Layer, Point, color, finite_number, positive_number

CIRCLE_SEGMENTS = 32


def _xy(p: Point) -> tuple[float, float]:
    return (p.x, p.y)


def _cross(a: Point, b: Point, c: Point) -> float:
    return (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x)


def _segments_intersect(a: Point, b: Point, c: Point, d: Point) -> bool:
    """Detect intersections including collinear touching of non-adjacent edges."""
    eps = 1e-12
    ab_c, ab_d, cd_a, cd_b = _cross(a, b, c), _cross(a, b, d), _cross(c, d, a), _cross(c, d, b)

    def on_segment(p: Point, q: Point, r: Point) -> bool:
        return (min(p.x, r.x) - eps <= q.x <= max(p.x, r.x) + eps
                and min(p.y, r.y) - eps <= q.y <= max(p.y, r.y) + eps)

    if abs(ab_c) <= eps and on_segment(a, c, b): return True
    if abs(ab_d) <= eps and on_segment(a, d, b): return True
    if abs(cd_a) <= eps and on_segment(c, a, d): return True
    if abs(cd_b) <= eps and on_segment(c, b, d): return True
    return ((ab_c > eps and ab_d < -eps or ab_c < -eps and ab_d > eps)
            and (cd_a > eps and cd_b < -eps or cd_a < -eps and cd_b > eps))


def _validate_convex_polygon(points: tuple[Point, ...]) -> None:
    if len(points) < 3 or len(set(points)) != len(points):
        raise ValueError("filled polygon needs at least three distinct vertices")
    n = len(points)
    turns = [_cross(points[i], points[(i + 1) % n], points[(i + 2) % n]) for i in range(n)]
    if any(abs(t) <= 1e-12 for t in turns) or not (all(t > 0 for t in turns) or all(t < 0 for t in turns)):
        raise ValueError("filled polygon must be strictly convex and nondegenerate")
    for i in range(n):
        for j in range(i + 1, n):
            if j == i + 1 or (i == 0 and j == n - 1):
                continue  # adjacent edges share a vertex
            if _segments_intersect(points[i], points[(i + 1) % n], points[j], points[(j + 1) % n]):
                raise ValueError("filled polygon must not self-intersect")


class DxfBackend(ICadBackend):
    def __init__(self) -> None:
        self.document = ezdxf.new("R2010", setup=False)
        self.document.header["$INSUNITS"] = 4  # millimetres
        self._layers: set[str] = set()
        self._blocks: set[str] = set()

    def add_layer(self, layer: Layer) -> None:
        if not layer.name or layer.name in self._layers or layer.name in self.document.layers:
            raise ValueError(f"duplicate or invalid layer: {layer.name!r}")
        if isinstance(layer.aci, bool) or not isinstance(layer.aci, int) or not 1 <= layer.aci <= 255:
            raise ValueError("layer ACI must be 1..255")
        rgb = color(layer.rgb)
        self.document.layers.new(layer.name, dxfattribs={"color": layer.aci, "true_color": int(rgb[1:], 16)})
        self._layers.add(layer.name)

    def add_block(self, name: str) -> None:
        if not name or name in self._blocks or name in self.document.blocks:
            raise ValueError(f"duplicate or invalid block: {name!r}")
        self.document.blocks.new(name=name, base_point=(0, 0))
        self._blocks.add(name)

    def _layout(self, block: str):
        if block not in self._blocks:
            raise ValueError(f"undefined block: {block!r}")
        return self.document.blocks.get(block)

    def _attrs(self, layer: str, rgb: str) -> dict:
        if layer not in self._layers:
            raise ValueError(f"undefined layer: {layer!r}")
        return {"layer": layer, "true_color": int(color(rgb)[1:], 16)}

    def add_line(self, block: str, start: Point, end: Point, layer: str, rgb: str) -> None:
        if start == end:
            raise ValueError("line endpoints must differ")
        self._layout(block).add_line(_xy(start), _xy(end), dxfattribs=self._attrs(layer, rgb))

    def add_polyline(self, block: str, points: tuple[Point, ...], width: float,
                     closed: bool, layer: str, rgb: str) -> None:
        if len(points) < (3 if closed else 2):
            raise ValueError("polyline has too few vertices")
        width = positive_number(width, "polyline width")
        attrs = self._attrs(layer, rgb) | {"const_width": width}
        self._layout(block).add_lwpolyline([_xy(p) for p in points], close=closed, dxfattribs=attrs)

    def _add_triangle(self, block: str, a: Point, b: Point, c: Point, layer: str, rgb: str) -> None:
        self._layout(block).add_solid([_xy(a), _xy(b), _xy(c)], dxfattribs=self._attrs(layer, rgb))

    def add_filled_polygon(self, block: str, points: tuple[Point, ...], layer: str, rgb: str) -> None:
        _validate_convex_polygon(points)
        if len(points) == 4:
            # DXF SOLID uses a 0,1,3,2 traversal order for a quadrilateral.
            a, b, c, d = points
            self._layout(block).add_solid([_xy(a), _xy(b), _xy(d), _xy(c)],
                                          dxfattribs=self._attrs(layer, rgb))
        else:
            for i in range(1, len(points) - 1):
                self._add_triangle(block, points[0], points[i], points[i + 1], layer, rgb)

    def add_filled_circle(self, block: str, center: Point, radius: float, layer: str, rgb: str) -> None:
        radius = positive_number(radius, "circle radius")
        # 32 SOLID triangles approximate the fill; the CIRCLE preserves the exact outline.
        for i in range(CIRCLE_SEGMENTS):
            a = 2 * math.pi * i / CIRCLE_SEGMENTS
            b = 2 * math.pi * (i + 1) / CIRCLE_SEGMENTS
            self._add_triangle(block, center,
                               Point(center.x + radius * math.cos(a), center.y + radius * math.sin(a)),
                               Point(center.x + radius * math.cos(b), center.y + radius * math.sin(b)),
                               layer, rgb)
        self._layout(block).add_circle(_xy(center), radius, dxfattribs=self._attrs(layer, rgb))

    def add_text(self, block: str, value: str, position: Point, height: float, layer: str, rgb: str) -> None:
        if not isinstance(value, str) or not value or "\n" in value or "\r" in value:
            raise ValueError("TEXT needs a nonempty single-line value")
        attrs = self._attrs(layer, rgb) | {"insert": _xy(position), "height": positive_number(height, "text height")}
        self._layout(block).add_text(value, dxfattribs=attrs)

    def insert_block(self, name: str, insertion: Point, scale: float, rotation: float, layer: str) -> None:
        if name not in self._blocks:
            raise ValueError(f"undefined block: {name!r}")
        if layer not in self._layers:
            raise ValueError(f"undefined layer: {layer!r}")
        scale = positive_number(scale, "block scale")
        rotation = finite_number(rotation, "rotation")
        self.document.modelspace().add_blockref(name, _xy(insertion),
            dxfattribs={"layer": layer, "xscale": scale, "yscale": scale, "rotation": rotation})

    def save_document(self, path: str | Path) -> None:
        target = Path(path)
        target.parent.mkdir(parents=True, exist_ok=True)
        self.document.saveas(target)
