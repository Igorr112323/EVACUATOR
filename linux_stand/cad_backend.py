"""CAD-neutral backend contract; MultiCAD is intentionally not implemented."""
from __future__ import annotations

from abc import ABC, abstractmethod
from pathlib import Path

from .core.primitives import Layer, Point


class ICadBackend(ABC):
    @abstractmethod
    def add_layer(self, layer: Layer) -> None: ...

    @abstractmethod
    def add_block(self, name: str) -> None: ...

    @abstractmethod
    def add_line(self, block: str, start: Point, end: Point, layer: str, rgb: str) -> None: ...

    @abstractmethod
    def add_polyline(self, block: str, points: tuple[Point, ...], width: float,
                     closed: bool, layer: str, rgb: str) -> None: ...

    @abstractmethod
    def add_filled_polygon(self, block: str, points: tuple[Point, ...], layer: str, rgb: str) -> None: ...

    @abstractmethod
    def add_filled_circle(self, block: str, center: Point, radius: float, layer: str, rgb: str) -> None: ...

    @abstractmethod
    def add_text(self, block: str, value: str, position: Point, height: float, layer: str, rgb: str) -> None: ...

    @abstractmethod
    def insert_block(self, name: str, insertion: Point, scale: float, rotation: float, layer: str) -> None: ...

    @abstractmethod
    def save_document(self, path: str | Path) -> None: ...
