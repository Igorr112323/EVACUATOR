"""Read the existing *demonstration* catalog, not a verified GOST database."""
from __future__ import annotations

from dataclasses import dataclass
import json
from pathlib import Path
from typing import Any

from .primitives import (
    FilledCircle, FilledPolygon, Point, Polyline, Primitive, color,
    finite_number, positive_number,
)

DEMO_MARKER = "[ПРОВЕРИТЬ ПО ТЕКСТУ ГОСТ]"
SUPPORTED_CODE = "E01-01"  # only this recipe is implemented in week one
PICTOGRAM_LAYER = "EVAC_PICTOGRAM"


@dataclass(frozen=True, slots=True)
class SignDefinition:
    code: str
    name: str
    shape: str
    aspect_ratio: float
    background: str
    min_height_mm: float
    max_height_mm: float
    default_height_mm: float
    gost_note: str
    catalog_notice: str
    pictogram: tuple[Primitive, ...]


def _point(value: Any) -> Point:
    if not isinstance(value, dict) or "x" not in value or "y" not in value:
        raise ValueError("recipe point must contain x and y")
    point = Point(value["x"], value["y"])
    if not (0 <= point.x <= 1 and 0 <= point.y <= 1):
        raise ValueError("recipe coordinates must be in [0, 1]")
    return point


def _points(value: Any, minimum: int) -> tuple[Point, ...]:
    if not isinstance(value, list) or len(value) < minimum:
        raise ValueError(f"recipe requires at least {minimum} points")
    return tuple(_point(p) for p in value)


def _recipe_primitive(raw: Any) -> Primitive:
    if not isinstance(raw, dict):
        raise ValueError("recipe primitive must be an object")
    kind = raw.get("type")
    if kind == "Polyline":
        return Polyline(_points(raw.get("points"), 2),
                        positive_number(raw.get("strokeWidth"), "strokeWidth"),
                        False, PICTOGRAM_LAYER, color(raw.get("stroke"), "stroke"))
    if kind == "Polygon":
        return FilledPolygon(_points(raw.get("points"), 3),
                             PICTOGRAM_LAYER, color(raw.get("fill"), "fill"))
    if kind == "Circle":
        center = _point(raw)
        radius = positive_number(raw.get("radius"), "radius")
        if not (radius <= center.x <= 1 - radius and radius <= center.y <= 1 - radius):
            raise ValueError("recipe circle must fit within the carrier")
        return FilledCircle(center, radius, PICTOGRAM_LAYER, color(raw.get("fill"), "fill"))
    raise ValueError(f"unsupported recipe primitive: {kind!r}")


def load_demo_definition(catalog_path: str | Path, code: str = SUPPORTED_CODE) -> SignDefinition:
    if code != SUPPORTED_CODE:
        raise ValueError(f"week-one stand implements only {SUPPORTED_CODE}, not {code!r}")
    with Path(catalog_path).open(encoding="utf-8") as stream:
        data = json.load(stream)
    if not isinstance(data, dict) or data.get("version") != "0.1-demo":
        raise ValueError("expected the 0.1-demo catalog")
    notice = data.get("notice")
    if not isinstance(notice, str) or DEMO_MARKER not in notice:
        raise ValueError("catalog is missing the unverified-norms notice")
    signs = data.get("signs")
    if not isinstance(signs, list):
        raise ValueError("catalog signs must be a list")
    matches = [s for s in signs if isinstance(s, dict) and s.get("code") == code]
    if len(matches) != 1:
        raise ValueError(f"expected exactly one definition for {code}; found {len(matches)}")
    raw = matches[0]
    note = raw.get("gostClauseRef")
    if not isinstance(note, str) or DEMO_MARKER not in note:
        raise ValueError("sign definition is missing the unverified-norms notice")
    aspect = positive_number(raw.get("aspectRatio"), "aspectRatio")
    if raw.get("shape") != "Square" or aspect != 1:
        raise ValueError("week-one stand supports only a square 1:1 carrier")
    low = positive_number(raw.get("minHeightMm"), "minHeightMm")
    high = positive_number(raw.get("maxHeightMm"), "maxHeightMm")
    default = positive_number(raw.get("defaultHeightMm"), "defaultHeightMm")
    if not low <= default <= high:
        raise ValueError("expected minHeightMm <= defaultHeightMm <= maxHeightMm")
    name = raw.get("name")
    if not isinstance(name, str) or not name:
        raise ValueError("sign name must be a nonempty string")
    recipe = raw.get("primitives")
    if not isinstance(recipe, list) or not recipe:
        raise ValueError("sign recipe must not be empty")
    return SignDefinition(code, name, "Square", aspect,
                          color(raw.get("backgroundColor"), "backgroundColor"),
                          finite_number(low, "minHeightMm"),
                          finite_number(high, "maxHeightMm"),
                          finite_number(default, "defaultHeightMm"),
                          note, notice, tuple(_recipe_primitive(p) for p in recipe))
