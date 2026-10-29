"""Small, immutable CAD-independent primitives used by the demonstration."""
from __future__ import annotations

from dataclasses import dataclass
import math
import re
from typing import TypeAlias


def finite_number(value: object, name: str) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError(f"{name} must be a finite number")
    number = float(value)
    if not math.isfinite(number):
        raise ValueError(f"{name} must be a finite number")
    return number


def positive_number(value: object, name: str) -> float:
    number = finite_number(value, name)
    if number <= 0:
        raise ValueError(f"{name} must be positive")
    return number


def color(value: object, name: str = "color") -> str:
    if not isinstance(value, str) or not re.fullmatch(r"#[0-9a-fA-F]{6}", value):
        raise ValueError(f"{name} must be #RRGGBB")
    return value.upper()


@dataclass(frozen=True, slots=True)
class Point:
    x: float
    y: float

    def __post_init__(self) -> None:
        object.__setattr__(self, "x", finite_number(self.x, "point.x"))
        object.__setattr__(self, "y", finite_number(self.y, "point.y"))


@dataclass(frozen=True, slots=True)
class Layer:
    name: str
    rgb: str
    aci: int  # fallback for readers which do not support DXF true color


@dataclass(frozen=True, slots=True)
class Line:
    start: Point
    end: Point
    layer: str
    rgb: str


@dataclass(frozen=True, slots=True)
class Polyline:
    points: tuple[Point, ...]
    width: float
    closed: bool
    layer: str
    rgb: str


@dataclass(frozen=True, slots=True)
class FilledPolygon:
    points: tuple[Point, ...]
    layer: str
    rgb: str


@dataclass(frozen=True, slots=True)
class FilledCircle:
    center: Point
    radius: float
    layer: str
    rgb: str


@dataclass(frozen=True, slots=True)
class Text:
    value: str
    position: Point
    height: float
    layer: str
    rgb: str


Primitive: TypeAlias = Line | Polyline | FilledPolygon | FilledCircle | Text


@dataclass(frozen=True, slots=True)
class Block:
    """A definition in local coordinates; it does not imply a CAD smart object."""
    name: str
    layers: tuple[Layer, ...]
    primitives: tuple[Primitive, ...]


@dataclass(frozen=True, slots=True)
class Bounds:
    min_x: float
    min_y: float
    max_x: float
    max_y: float

    @classmethod
    def from_points(cls, points: tuple[Point, ...]) -> Bounds:
        if not points:
            raise ValueError("bounds need at least one point")
        return cls(min(p.x for p in points), min(p.y for p in points),
                   max(p.x for p in points), max(p.y for p in points))
