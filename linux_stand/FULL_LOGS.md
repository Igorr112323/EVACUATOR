# Полные журналы итоговых запусков

Ниже приведены без сокращений сохранённые stdout/stderr итоговых команд. Это журналы проверки учебного стенда, не доказательство соответствия ГОСТ или работы в nanoCAD.

## Определение среды — `logs/environment.log`

```text
$ uname -a
Linux e2b.local 6.1.158+ #1 SMP PREEMPT_DYNAMIC Mon May 11 18:48:24 UTC 2026 x86_64 GNU/Linux
[exit=0]

$ command -v dotnet
[exit=1]

$ command -v python3
/usr/bin/python3
[exit=0]

$ python3 --version
Python 3.11.2
[exit=0]

$ python3 -m pip list --disable-pip-version-check | grep -i ezdxf
[exit=1] (system Python has no matching package)

$ /tmp/evacuator-linux-stand-clean-venv/bin/python --version
Python 3.11.2
[exit=0]
```

## Чистое окружение и зависимости — `logs/install.log`

```text
$ python3 -m venv /tmp/evacuator-linux-stand-clean-venv
$ /tmp/evacuator-linux-stand-clean-venv/bin/python -m pip install --index-url https://pypi.org/simple --disable-pip-version-check -r linux_stand/requirements-lock.txt
Collecting contourpy==1.3.3
  Using cached contourpy-1.3.3-cp311-cp311-manylinux_2_27_x86_64.manylinux_2_28_x86_64.whl (355 kB)
Collecting cycler==0.12.1
  Using cached cycler-0.12.1-py3-none-any.whl (8.3 kB)
Collecting dxfgrabber==1.0.1
  Using cached dxfgrabber-1.0.1-py2.py3-none-any.whl (34 kB)
Collecting ezdxf==1.4.5
  Using cached ezdxf-1.4.5-cp311-cp311-manylinux2014_x86_64.manylinux_2_17_x86_64.whl (5.0 MB)
Collecting fonttools==4.68.0
  Using cached fonttools-4.68.0-cp311-cp311-manylinux2014_x86_64.manylinux_2_17_x86_64.whl (5.5 MB)
Collecting iniconfig==2.3.1
  Using cached iniconfig-2.3.1-py3-none-any.whl (7.6 kB)
Collecting kiwisolver==1.5.1
  Using cached kiwisolver-1.5.1-cp311-cp311-manylinux2014_x86_64.manylinux_2_17_x86_64.whl (1.4 MB)
Collecting matplotlib==3.10.9
  Using cached matplotlib-3.10.9-cp311-cp311-manylinux2014_x86_64.manylinux_2_17_x86_64.whl (8.8 MB)
Collecting numpy==2.4.6
  Using cached numpy-2.4.6-cp311-cp311-manylinux_2_27_x86_64.manylinux_2_28_x86_64.whl (16.9 MB)
Collecting packaging==26.3
  Using cached packaging-26.3-py3-none-any.whl (129 kB)
Collecting pillow==13.0.0
  Using cached pillow-13.0.0-cp311-cp311-manylinux_2_27_x86_64.manylinux_2_28_x86_64.whl (7.1 MB)
Collecting pluggy==1.7.0
  Using cached pluggy-1.7.0-py3-none-any.whl (27 kB)
Collecting Pygments==2.21.0
  Using cached pygments-2.21.0-py3-none-any.whl (1.3 MB)
Collecting pyparsing==3.3.3
  Using cached pyparsing-3.3.3-py3-none-any.whl (126 kB)
Collecting pytest==8.4.2
  Using cached pytest-8.4.2-py3-none-any.whl (365 kB)
Collecting python-dateutil==3.0.0
  Using cached python_dateutil-3.0.0-py2.py3-none-any.whl (94 kB)
Collecting six==1.17.0
  Using cached six-1.17.0-py2.py3-none-any.whl (11 kB)
Collecting typing_extensions==4.16.0
  Using cached typing_extensions-4.16.0-py3-none-any.whl (45 kB)
Collecting tzdata==2026.6
  Using cached tzdata-2026.6-py2.py3-none-any.whl (348 kB)
Installing collected packages: dxfgrabber, tzdata, typing_extensions, six, pyparsing, Pygments, pluggy, pillow, packaging, numpy, kiwisolver, iniconfig, fonttools, cycler, python-dateutil, pytest, ezdxf, contourpy, matplotlib
Successfully installed Pygments-2.21.0 contourpy-1.3.3 cycler-0.12.1 dxfgrabber-1.0.1 ezdxf-1.4.5 fonttools-4.68.0 iniconfig-2.3.1 kiwisolver-1.5.1 matplotlib-3.10.9 numpy-2.4.6 packaging-26.3 pillow-13.0.0 pluggy-1.7.0 pyparsing-3.3.3 pytest-8.4.2 python-dateutil-3.0.0 six-1.17.0 typing_extensions-4.16.0 tzdata-2026.6

$ /tmp/evacuator-linux-stand-clean-venv/bin/python -m pip check
No broken requirements found.

$ /tmp/evacuator-linux-stand-clean-venv/bin/python -m pip freeze
contourpy==1.3.3
cycler==0.12.1
dxfgrabber==1.0.1
ezdxf==1.4.5
fonttools==4.68.0
iniconfig==2.3.1
kiwisolver==1.5.1
matplotlib==3.10.9
numpy==2.4.6
packaging==26.3
pillow==13.0.0
pluggy==1.7.0
Pygments==2.21.0
pyparsing==3.3.3
pytest==8.4.2
python-dateutil==3.0.0
six==1.17.0
typing_extensions==4.16.0
tzdata==2026.6
```

## Автоматические тесты — `logs/pytest.log`

```text
$ MPLCONFIGDIR=/tmp/evacuator-matplotlib PYTEST_DISABLE_PLUGIN_AUTOLOAD=1 /tmp/evacuator-linux-stand-clean-venv/bin/python -m pytest -v linux_stand/tests
============================= test session starts ==============================
platform linux -- Python 3.11.2, pytest-8.4.2, pluggy-1.7.0 -- /tmp/evacuator-linux-stand-clean-venv/bin/python
cachedir: .pytest_cache
rootdir: /home/user/EVACUATOR
collecting ... collected 58 items

linux_stand/tests/test_artifacts.py::test_default_sample_matches_independent_golden_spec PASSED [  1%]
linux_stand/tests/test_artifacts.py::test_independent_verifier_rejects_changed_text PASSED [  3%]
linux_stand/tests/test_artifacts.py::test_independent_verifier_rejects_changed_geometry PASSED [  5%]
linux_stand/tests/test_artifacts.py::test_raw_scan_detects_hatch_that_dxfgrabber_omits PASSED [  6%]
linux_stand/tests/test_artifacts.py::test_png_verifier_rejects_blank_image PASSED [  8%]
linux_stand/tests/test_artifacts.py::test_cli_rejects_invalid_demo_r01_without_writing PASSED [ 10%]
linux_stand/tests/test_artifacts.py::test_cli_can_explicitly_export_failing_demo_r01 PASSED [ 12%]
linux_stand/tests/test_backend.py::test_backend_contract_driven_by_abstract_core PASSED [ 13%]
linux_stand/tests/test_backend.py::test_dxf_insert_retains_parameters_for_independent_reader[8-0-0-0] PASSED [ 15%]
linux_stand/tests/test_backend.py::test_dxf_insert_retains_parameters_for_independent_reader[12-25--10-90] PASSED [ 17%]
linux_stand/tests/test_backend.py::test_dxf_insert_retains_parameters_for_independent_reader[15--3-7-180] PASSED [ 18%]
linux_stand/tests/test_backend.py::test_dxf_backend_rejects_undefined_block_and_layer PASSED [ 20%]
linux_stand/tests/test_backend.py::test_dxf_backend_rejects_unsupported_filled_polygons[vertices0] PASSED [ 22%]
linux_stand/tests/test_backend.py::test_dxf_backend_rejects_unsupported_filled_polygons[vertices1] PASSED [ 24%]
linux_stand/tests/test_backend.py::test_dxf_backend_rejects_unsupported_filled_polygons[vertices2] PASSED [ 25%]
linux_stand/tests/test_backend.py::test_dxf_backend_rejects_unsupported_filled_polygons[vertices3] PASSED [ 27%]
linux_stand/tests/test_backend.py::test_dxf_backend_rejects_multiline_text PASSED [ 29%]
linux_stand/tests/test_core.py::test_catalog_loads_single_unverified_demo PASSED [ 31%]
linux_stand/tests/test_core.py::test_catalog_rejects_other_codes PASSED  [ 32%]
linux_stand/tests/test_core.py::test_catalog_rejects_malformed_or_unmarked_demo[notice-notice] PASSED [ 34%]
linux_stand/tests/test_core.py::test_catalog_rejects_malformed_or_unmarked_demo[gost_note-notice] PASSED [ 36%]
linux_stand/tests/test_core.py::test_catalog_rejects_malformed_or_unmarked_demo[duplicate-exactly one] PASSED [ 37%]
linux_stand/tests/test_core.py::test_catalog_rejects_malformed_or_unmarked_demo[unknown_primitive-unsupported recipe primitive] PASSED [ 39%]
linux_stand/tests/test_core.py::test_catalog_rejects_malformed_or_unmarked_demo[out_of_range-coordinates] PASSED [ 41%]
linux_stand/tests/test_core.py::test_catalog_rejects_malformed_or_unmarked_demo[bad_limits-minHeightMm] PASSED [ 43%]
linux_stand/tests/test_core.py::test_catalog_rejects_malformed_or_unmarked_demo[bad_color-backgroundColor] PASSED [ 44%]
linux_stand/tests/test_core.py::test_catalog_rejects_malformed_or_unmarked_demo[wrong_shape-square] PASSED [ 46%]
linux_stand/tests/test_core.py::test_core_imports_without_any_cad_or_render_library PASSED [ 48%]
linux_stand/tests/test_core.py::test_abstract_block_contains_expected_primitives PASSED [ 50%]
linux_stand/tests/test_core.py::test_carrier_bounds_and_transform_identity PASSED [ 51%]
linux_stand/tests/test_core.py::test_rotated_translated_carrier_bounds PASSED [ 53%]
linux_stand/tests/test_core.py::test_sign_rejects_nonpositive_or_nonfinite_height[0] PASSED [ 55%]
linux_stand/tests/test_core.py::test_sign_rejects_nonpositive_or_nonfinite_height[-1] PASSED [ 56%]
linux_stand/tests/test_core.py::test_sign_rejects_nonpositive_or_nonfinite_height[nan] PASSED [ 58%]
linux_stand/tests/test_core.py::test_sign_rejects_nonpositive_or_nonfinite_height[inf] PASSED [ 60%]
linux_stand/tests/test_core.py::test_sign_rejects_nonpositive_or_nonfinite_height[-inf] PASSED [ 62%]
linux_stand/tests/test_core.py::test_sign_rejects_nonpositive_or_nonfinite_height[True] PASSED [ 63%]
linux_stand/tests/test_core.py::test_sign_rejects_nonpositive_or_nonfinite_height[10] PASSED [ 65%]
linux_stand/tests/test_core.py::test_sign_rejects_invalid_paper_scale[0] PASSED [ 67%]
linux_stand/tests/test_core.py::test_sign_rejects_invalid_paper_scale[-1] PASSED [ 68%]
linux_stand/tests/test_core.py::test_sign_rejects_invalid_paper_scale[nan] PASSED [ 70%]
linux_stand/tests/test_core.py::test_sign_rejects_invalid_paper_scale[inf] PASSED [ 72%]
linux_stand/tests/test_core.py::test_sign_rejects_invalid_paper_scale[True] PASSED [ 74%]
linux_stand/tests/test_core.py::test_sign_rejects_nonfinite_rotation[nan] PASSED [ 75%]
linux_stand/tests/test_core.py::test_sign_rejects_nonfinite_rotation[inf] PASSED [ 77%]
linux_stand/tests/test_core.py::test_sign_rejects_nonfinite_rotation[-inf] PASSED [ 79%]
linux_stand/tests/test_core.py::test_point_rejects_nonfinite_coordinates PASSED [ 81%]
linux_stand/tests/test_normcontrol.py::test_r01_demo_bounds_and_paper_scale[7.999999-1-7.999999-False] PASSED [ 82%]
linux_stand/tests/test_normcontrol.py::test_r01_demo_bounds_and_paper_scale[8-1-8-True] PASSED [ 84%]
linux_stand/tests/test_normcontrol.py::test_r01_demo_bounds_and_paper_scale[10-1-10-True] PASSED [ 86%]
linux_stand/tests/test_normcontrol.py::test_r01_demo_bounds_and_paper_scale[15-1-15-True] PASSED [ 87%]
linux_stand/tests/test_normcontrol.py::test_r01_demo_bounds_and_paper_scale[15.000001-1-15.000001-False] PASSED [ 89%]
linux_stand/tests/test_normcontrol.py::test_r01_demo_bounds_and_paper_scale[799-100-7.99-False] PASSED [ 91%]
linux_stand/tests/test_normcontrol.py::test_r01_demo_bounds_and_paper_scale[800-100-8-True] PASSED [ 93%]
linux_stand/tests/test_normcontrol.py::test_r01_demo_bounds_and_paper_scale[1000-100-10-True] PASSED [ 94%]
linux_stand/tests/test_normcontrol.py::test_r01_demo_bounds_and_paper_scale[1500-100-15-True] PASSED [ 96%]
linux_stand/tests/test_normcontrol.py::test_r01_demo_bounds_and_paper_scale[1501-100-15.01-False] PASSED [ 98%]
linux_stand/tests/test_normcontrol.py::test_r01_rejects_mismatched_code PASSED [100%]

============================== 58 passed in 1.61s ==============================
```

## Положительный пример и генерация DXF/PNG — `logs/demo.log`

```text
$ MPLCONFIGDIR=/tmp/evacuator-matplotlib /tmp/evacuator-linux-stand-clean-venv/bin/python -m linux_stand demo --output-dir linux_stand/artifacts
Linux DXF stand — DEMO ONLY; not nanoCAD, not a GOST verification
[ПРОВЕРИТЬ ПО ТЕКСТУ ГОСТ]: demo catalog E01-01, limits 8..15 mm on paper
R01 demo: PASS; paper height=10 mm, scale=1:1
Sign: code=E01-01; model height=10 mm; insertion=(0, 0); rotation=0°
Carrier AABB (model mm, excluding caption): [0, 0] .. [10, 10]
DXF: linux_stand/artifacts/hello_e01_01.dxf
PNG: linux_stand/artifacts/hello_e01_01.png
The DXF is a scaled ordinary BLOCK/INSERT, not a nanoCAD custom/smart object.
```

## Отрицательные примеры R01 — `logs/r01-negative.log`

```text
$ PYTHONUNBUFFERED=1 MPLCONFIGDIR=/tmp/evacuator-matplotlib /tmp/evacuator-linux-stand-clean-venv/bin/python -m linux_stand demo --height 7 --output-dir /tmp/evacuator-r01-negative-7
Linux DXF stand — DEMO ONLY; not nanoCAD, not a GOST verification
[ПРОВЕРИТЬ ПО ТЕКСТУ ГОСТ]: demo catalog E01-01, limits 8..15 mm on paper
R01 demo: FAIL; paper height=7 mm, scale=1:1
No DXF or PNG written: R01 failed (use --allow-invalid only for a negative demo).
[exit=2; expected=2]
PASS: no output directory was created

$ PYTHONUNBUFFERED=1 MPLCONFIGDIR=/tmp/evacuator-matplotlib /tmp/evacuator-linux-stand-clean-venv/bin/python -m linux_stand demo --height 16 --output-dir /tmp/evacuator-r01-negative-16
Linux DXF stand — DEMO ONLY; not nanoCAD, not a GOST verification
[ПРОВЕРИТЬ ПО ТЕКСТУ ГОСТ]: demo catalog E01-01, limits 8..15 mm on paper
R01 demo: FAIL; paper height=16 mm, scale=1:1
No DXF or PNG written: R01 failed (use --allow-invalid only for a negative demo).
[exit=2; expected=2]
PASS: no output directory was created

```

## Независимая перечитка и проверка PNG — `logs/verification.log`

```text
$ /tmp/evacuator-linux-stand-clean-venv/bin/python -m linux_stand.verify_artifacts --dxf linux_stand/artifacts/hello_e01_01.dxf --png linux_stand/artifacts/hello_e01_01.png
DXF: linux_stand/artifacts/hello_e01_01.dxf
PNG: linux_stand/artifacts/hello_e01_01.png
Golden spec: /home/user/EVACUATOR/linux_stand/spec/hello_e01_01_h10.json
PASS raw ASCII entity scan: model={'INSERT': 1}; block={'SOLID': 36, 'LWPOLYLINE': 7, 'CIRCLE': 1, 'LINE': 1, 'TEXT': 1}
PASS dxfgrabber 1.0.1: DXF AC1024, INSUNITS=4 (mm), one model INSERT at (0.0, 0.0, 0.0), scale=(10.0, 10.0, 1.0), rotation=0°
PASS block: 46 primitives, types={'SOLID': 36, 'LWPOLYLINE': 7, 'CIRCLE': 1, 'LINE': 1, 'TEXT': 1}, layers={'EVAC_BACKGROUND': 1, 'EVAC_PICTOGRAM': 43, 'EVAC_ANNOTATION': 2}
PASS carrier: green #008855, local vertices 0..1, model bounds [0.0, 0.0, 10.0, 10.0] mm
PASS pictogram: all seven polyline vertex sequences, widths, closure and true color
PASS filled geometry: exact CIRCLE center/radius, 32 head triangles and 3 body triangles with every vertex checked
PASS annotation: LINE endpoints; TEXT='E01-01', local height=0.1
PASS PNG: 780x775, green=197232, white inside carrier bbox=100804, dark below=4266; green bbox=(117, 51)..(662, 596)
PASS independent artifact verification; this is not GOST or nanoCAD validation.
```

## Дополнительная проверка форматов — `logs/integrity.log`

```text
$ command -v file
[exit=1] utility not installed; using Python readers instead

$ /tmp/evacuator-linux-stand-clean-venv/bin/python (independent DXF and PNG format inspection)
DXF linux_stand/artifacts/hello_e01_01.dxf: AC1024; model entities=1
PNG linux_stand/artifacts/hello_e01_01.png: PNG; 780x775; mode=RGBA
PASS: both files opened; Pillow PNG structural verification passed
[exit=0]
```
