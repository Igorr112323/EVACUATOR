import pytest

from linux_stand.core.catalog import DEMO_MARKER
from linux_stand.core.geometry import Sign
from linux_stand.core.normcontrol import check_r01


@pytest.mark.parametrize("height, denominator, paper, expected", [
    (7.999999, 1, 7.999999, False),
    (8, 1, 8, True),
    (10, 1, 10, True),
    (15, 1, 15, True),
    (15.000001, 1, 15.000001, False),
    (799, 100, 7.99, False),
    (800, 100, 8, True),
    (1000, 100, 10, True),
    (1500, 100, 15, True),
    (1501, 100, 15.01, False),
])
def test_r01_demo_bounds_and_paper_scale(definition, height, denominator, paper, expected):
    # [ПРОВЕРИТЬ ПО ТЕКСТУ ГОСТ] 8..15 mm are unverified demo data, not a real clause.
    result = check_r01(Sign("E01-01", height, paper_scale_denominator=denominator), definition)
    assert result.passed is expected
    assert result.paper_height_mm == pytest.approx(paper)
    assert (result.minimum_mm, result.maximum_mm) == (8, 15)
    assert DEMO_MARKER in result.note


def test_r01_rejects_mismatched_code(definition):
    # [ПРОВЕРИТЬ ПО ТЕКСТУ ГОСТ] This checks only the input contract of the demo rule.
    with pytest.raises(ValueError, match="do not match"):
        check_r01(Sign("OTHER", 10), definition)
