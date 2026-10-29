from pathlib import Path

import pytest

from linux_stand.core.catalog import load_demo_definition
from linux_stand.core.geometry import Sign
from linux_stand.dxf_backend import DxfBackend
from linux_stand.export import export_sign
from linux_stand.renderer import render_dxf_to_png

ROOT = Path(__file__).resolve().parents[2]
CATALOG = ROOT / "norms/gost_12_4_026_2015.json"
SPEC = ROOT / "linux_stand/spec/hello_e01_01_h10.json"


@pytest.fixture
def definition():
    return load_demo_definition(CATALOG)


@pytest.fixture(scope="module")
def sample_files(tmp_path_factory):
    folder = tmp_path_factory.mktemp("sample")
    dxf, png = folder / "sample.dxf", folder / "sample.png"
    definition = load_demo_definition(CATALOG)
    export_sign(Sign("E01-01", 10), definition, DxfBackend(), dxf)
    render_dxf_to_png(dxf, png)
    return dxf, png
