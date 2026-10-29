from __future__ import annotations

import json
import subprocess
import sys

import ezdxf
import pytest
from PIL import Image

from linux_stand.tests.conftest import ROOT, SPEC
from linux_stand.verify_artifacts import VerificationError, verify, verify_dxf, verify_png


def test_default_sample_matches_independent_golden_spec(sample_files):
    dxf, png = sample_files
    messages = verify(dxf, png, SPEC)
    assert len(messages) == 8
    assert all(message.startswith("PASS") for message in messages)


def test_independent_verifier_rejects_changed_text(sample_files, tmp_path):
    dxf, _ = sample_files
    doc = ezdxf.readfile(dxf)
    text = doc.blocks["EVAC_E01_01"].query("TEXT")[0]
    text.dxf.text = "WRONG"
    altered = tmp_path / "changed_text.dxf"
    doc.saveas(altered)
    spec = json.loads(SPEC.read_text(encoding="utf-8"))
    with pytest.raises(VerificationError, match="unexpected TEXT"):
        verify_dxf(altered, spec)


def test_independent_verifier_rejects_changed_geometry(sample_files, tmp_path):
    dxf, _ = sample_files
    doc = ezdxf.readfile(dxf)
    polyline = doc.blocks["EVAC_E01_01"].query("LWPOLYLINE")[1]
    coordinates = list(polyline.get_points("xy"))
    coordinates[0] = (0.5, 0.15)
    polyline.set_points(coordinates, format="xy")
    altered = tmp_path / "changed_geometry.dxf"
    doc.saveas(altered)
    spec = json.loads(SPEC.read_text(encoding="utf-8"))
    with pytest.raises(VerificationError, match="polyline"):
        verify_dxf(altered, spec)


def test_raw_scan_detects_hatch_that_dxfgrabber_omits(sample_files, tmp_path):
    dxf, _ = sample_files
    doc = ezdxf.readfile(dxf)
    hatch = doc.blocks["EVAC_E01_01"].add_hatch()
    hatch.paths.add_polyline_path([(0, 0), (.1, 0), (.1, .1), (0, .1)], is_closed=True)
    altered = tmp_path / "extra_hatch.dxf"
    doc.saveas(altered)
    spec = json.loads(SPEC.read_text(encoding="utf-8"))
    with pytest.raises(VerificationError, match="raw block entity counts"):
        verify_dxf(altered, spec)


def test_png_verifier_rejects_blank_image(tmp_path):
    path = tmp_path / "blank.png"
    Image.new("RGB", (780, 775), "white").save(path)
    spec = json.loads(SPEC.read_text(encoding="utf-8"))
    with pytest.raises(VerificationError, match="not enough green"):
        verify_png(path, spec)


def test_cli_rejects_invalid_demo_r01_without_writing(tmp_path):
    target = tmp_path / "output"
    process = subprocess.run([sys.executable, "-m", "linux_stand", "demo", "--height", "7", "--output-dir", str(target)],
                             cwd=ROOT, text=True, capture_output=True)
    assert process.returncode == 2
    assert "R01 demo: FAIL" in process.stdout
    assert "No DXF or PNG written" in process.stderr
    assert not target.exists()


def test_cli_can_explicitly_export_failing_demo_r01(tmp_path):
    target = tmp_path / "negative"
    process = subprocess.run([sys.executable, "-m", "linux_stand", "demo", "--height", "7", "--allow-invalid", "--output-dir", str(target)],
                             cwd=ROOT, text=True, capture_output=True)
    assert process.returncode == 0, process.stdout + process.stderr
    assert "R01 demo: FAIL" in process.stdout
    assert (target / "hello_e01_01.dxf").is_file()
    assert (target / "hello_e01_01.png").is_file()
