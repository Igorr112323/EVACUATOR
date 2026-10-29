"""Drive a backend from the CAD-independent primitives."""
from __future__ import annotations

from pathlib import Path

from .cad_backend import ICadBackend
from .core.catalog import SignDefinition
from .core.geometry import BACKGROUND_LAYER, Sign, create_sign_block
from .core.primitives import FilledCircle, FilledPolygon, Line, Polyline, Text


def export_sign(sign: Sign, definition: SignDefinition, backend: ICadBackend, path: str | Path) -> None:
    if sign.code != definition.code:
        raise ValueError("sign and catalog codes do not match")
    block = create_sign_block(definition)
    for layer in block.layers:
        backend.add_layer(layer)
    backend.add_block(block.name)
    for item in block.primitives:
        if isinstance(item, Line):
            backend.add_line(block.name, item.start, item.end, item.layer, item.rgb)
        elif isinstance(item, Polyline):
            backend.add_polyline(block.name, item.points, item.width, item.closed, item.layer, item.rgb)
        elif isinstance(item, FilledPolygon):
            backend.add_filled_polygon(block.name, item.points, item.layer, item.rgb)
        elif isinstance(item, FilledCircle):
            backend.add_filled_circle(block.name, item.center, item.radius, item.layer, item.rgb)
        elif isinstance(item, Text):
            backend.add_text(block.name, item.value, item.position, item.height, item.layer, item.rgb)
        else:
            raise TypeError(f"unhandled primitive: {type(item).__name__}")
    backend.insert_block(block.name, sign.insertion, sign.model_height_mm,
                         sign.rotation_degrees, BACKGROUND_LAYER)
    backend.save_document(path)
