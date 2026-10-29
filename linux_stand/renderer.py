"""Render the *saved DXF*, not the original in-memory geometry, to a headless PNG."""
from __future__ import annotations

from pathlib import Path

import matplotlib
matplotlib.use("Agg", force=True)
import matplotlib.pyplot as plt
import ezdxf
from ezdxf.addons.drawing import Frontend, RenderContext
from ezdxf.addons.drawing.config import BackgroundPolicy, ColorPolicy, Configuration
from ezdxf.addons.drawing.matplotlib import MatplotlibBackend
from matplotlib.patches import Polygon


def render_dxf_to_png(dxf_path: str | Path, png_path: str | Path) -> None:
    drawing = ezdxf.readfile(dxf_path)
    target = Path(png_path)
    target.parent.mkdir(parents=True, exist_ok=True)
    fig, ax = plt.subplots(figsize=(8, 8), dpi=120)
    try:
        frontend = Frontend(RenderContext(drawing), MatplotlibBackend(ax, adjust_figure=False),
                            config=Configuration(background_policy=BackgroundPolicy.WHITE,
                                                 color_policy=ColorPolicy.COLOR))
        frontend.draw_layout(drawing.modelspace(), finalize=True)
        # SOLID triangles share edges; anti-aliasing each polygon separately creates
        # hairline gaps. Keep text/path anti-aliasing, but not individual polygons.
        for patch in ax.patches:
            if isinstance(patch, Polygon):
                patch.set_antialiased(False)
        ax.autoscale_view()
        ax.set_aspect("equal")
        ax.set_axis_off()
        fig.savefig(target, dpi=120, bbox_inches="tight", pad_inches=.15, facecolor="white")
    finally:
        plt.close(fig)
