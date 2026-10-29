"""Build a normalized block and transform its square carrier into model space."""
from __future__ import annotations

from dataclasses import dataclass, field
import math

from .catalog import PICTOGRAM_LAYER, SignDefinition
from .primitives import (
    Block, Bounds, FilledPolygon, Layer, Line, Point, Polyline, Text,
    finite_number, positive_number,
)

BACKGROUND_LAYER = "EVAC_BACKGROUND"
ANNOTATION_LAYER = "EVAC_ANNOTATION"


@dataclass(frozen=True, slots=True)
class Sign:
    code: str
    model_height_mm: float
    insertion: Point = field(default_factory=lambda: Point(0, 0))
    rotation_degrees: float = 0
    paper_scale_denominator: float = 1

    def __post_init__(self) -> None:
        if not isinstance(self.code, str) or not self.code:
            raise ValueError("sign code must be nonempty")
        if not isinstance(self.insertion, Point):
            raise ValueError("insertion must be a Point")
        object.__setattr__(self, "model_height_mm", positive_number(self.model_height_mm, "model_height_mm"))
        object.__setattr__(self, "rotation_degrees", finite_number(self.rotation_degrees, "rotation_degrees"))
        object.__setattr__(self, "paper_scale_denominator", positive_number(self.paper_scale_denominator, "paper_scale_denominator"))


def create_sign_block(definition: SignDefinition) -> Block:
    """One unit high. Width and pictogram are schema-demo values, not official art."""
    if definition.code != "E01-01" or definition.shape != "Square" or definition.aspect_ratio != 1:
        raise ValueError("unsupported week-one sign definition")
    bg, white, dark = definition.background, "#FFFFFF", "#1A1A1A"
    layers = (Layer(BACKGROUND_LAYER, bg, 3), Layer(PICTOGRAM_LAYER, white, 7),
              Layer(ANNOTATION_LAYER, dark, 7))
    carrier = FilledPolygon((Point(0, 0), Point(1, 0), Point(1, 1), Point(0, 1)),
                            BACKGROUND_LAYER, bg)
    # Matches the square carrier in the current C# recipe, but is not a parity proof.
    border = Polyline((Point(.045, .045), Point(.955, .045),
                       Point(.955, .955), Point(.045, .955)),
                      .035, True, PICTOGRAM_LAYER, white)
    # Annotation is outside the physical 1x1 carrier and is not part of the symbol.
    underline = Line(Point(0, -.08), Point(1, -.08), ANNOTATION_LAYER, dark)
    caption = Text(definition.code, Point(0, -.23), .10, ANNOTATION_LAYER, dark)
    return Block("EVAC_E01_01", layers,
                 (carrier, border, *definition.pictogram, underline, caption))


def transform_point(point: Point, sign: Sign) -> Point:
    radians = math.radians(sign.rotation_degrees % 360)
    c, s = math.cos(radians), math.sin(radians)
    x, y = point.x * sign.model_height_mm, point.y * sign.model_height_mm
    return Point(sign.insertion.x + x * c - y * s,
                 sign.insertion.y + x * s + y * c)


def carrier_bounds(sign: Sign) -> Bounds:
    """Axis-aligned bounds of the carrier only, excluding the annotation."""
    corners = (Point(0, 0), Point(1, 0), Point(1, 1), Point(0, 1))
    return Bounds.from_points(tuple(transform_point(p, sign) for p in corners))
