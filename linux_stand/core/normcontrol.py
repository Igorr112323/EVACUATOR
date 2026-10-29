"""Demonstration R01 only; values and normative reference are unverified."""
from __future__ import annotations

from dataclasses import dataclass

from .catalog import SignDefinition
from .geometry import Sign


@dataclass(frozen=True, slots=True)
class R01Result:
    passed: bool
    paper_height_mm: float
    minimum_mm: float
    maximum_mm: float
    note: str


def check_r01(sign: Sign, definition: SignDefinition) -> R01Result:
    # [ПРОВЕРИТЬ ПО ТЕКСТУ ГОСТ] No authoritative clause has been verified.
    # These are only the 8..15 mm limits of the repository's 0.1-demo data.
    if sign.code != definition.code:
        raise ValueError("sign and catalog codes do not match")
    paper_height = sign.model_height_mm / sign.paper_scale_denominator
    return R01Result(definition.min_height_mm <= paper_height <= definition.max_height_mm,
                     paper_height, definition.min_height_mm, definition.max_height_mm,
                     definition.gost_note)
