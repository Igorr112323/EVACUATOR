"""CLI for the week-one hello-DXF and PNG."""
from __future__ import annotations

import argparse
from pathlib import Path
import sys

from .core.catalog import DEMO_MARKER, load_demo_definition
from .core.geometry import Sign, carrier_bounds
from .core.normcontrol import check_r01
from .core.primitives import Point
from .dxf_backend import DxfBackend
from .export import export_sign
from .renderer import render_dxf_to_png

REPO = Path(__file__).resolve().parent.parent


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Linux demonstration stand; not a nanoCAD plugin")
    sub = parser.add_subparsers(dest="command", required=True)
    demo = sub.add_parser("demo", help="create one DXF block and render the saved drawing")
    demo.add_argument("--catalog", type=Path, default=REPO / "norms/gost_12_4_026_2015.json")
    demo.add_argument("--code", default="E01-01")
    demo.add_argument("--height", type=float, default=10, help="model height in mm (DXF model units)")
    demo.add_argument("--scale", type=float, default=1, help="paper scale denominator, e.g. 100 for 1:100")
    demo.add_argument("--x", type=float, default=0)
    demo.add_argument("--y", type=float, default=0)
    demo.add_argument("--rotation", type=float, default=0, help="degrees")
    demo.add_argument("--output-dir", type=Path, default=REPO / "linux_stand/artifacts")
    demo.add_argument("--allow-invalid", action="store_true", help="create output even when demo R01 fails")
    args = parser.parse_args(argv)
    try:
        definition = load_demo_definition(args.catalog, args.code)
        sign = Sign(args.code, args.height, Point(args.x, args.y), args.rotation, args.scale)
        result = check_r01(sign, definition)
        print("Linux DXF stand — DEMO ONLY; not nanoCAD, not a GOST verification")
        print(f"{DEMO_MARKER}: demo catalog {definition.code}, limits {result.minimum_mm:g}..{result.maximum_mm:g} mm on paper")
        print(f"R01 demo: {'PASS' if result.passed else 'FAIL'}; paper height={result.paper_height_mm:g} mm, scale=1:{sign.paper_scale_denominator:g}")
        if not result.passed and not args.allow_invalid:
            print("No DXF or PNG written: R01 failed (use --allow-invalid only for a negative demo).", file=sys.stderr)
            return 2
        dxf_path, png_path = args.output_dir / "hello_e01_01.dxf", args.output_dir / "hello_e01_01.png"
        export_sign(sign, definition, DxfBackend(), dxf_path)
        render_dxf_to_png(dxf_path, png_path)
        bounds = carrier_bounds(sign)
        print(f"Sign: code={sign.code}; model height={sign.model_height_mm:g} mm; insertion=({sign.insertion.x:g}, {sign.insertion.y:g}); rotation={sign.rotation_degrees:g}°")
        print(f"Carrier AABB (model mm, excluding caption): [{bounds.min_x:g}, {bounds.min_y:g}] .. [{bounds.max_x:g}, {bounds.max_y:g}]")
        print(f"DXF: {dxf_path}")
        print(f"PNG: {png_path}")
        print("The DXF is a scaled ordinary BLOCK/INSERT, not a nanoCAD custom/smart object.")
        return 0
    except (ValueError, FileNotFoundError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
