#!/usr/bin/env python3
"""Run the mobile coverage generator with a dense source-line view."""

from __future__ import annotations

import importlib.util
import pathlib
import sys
from typing import Any


CORE_PATH = pathlib.Path(__file__).with_name("generate-mobile-coverage-report-core.py")
MODULE_NAME = "_ssc_mobile_coverage_report_core"
DENSE_SOURCE_CSS = """
    .source-table-wrap { max-height:76vh; overflow:auto; -webkit-overflow-scrolling:touch; }
    .source-table { width:100%; border-collapse:collapse; font-size:10px; line-height:1.15; }
    .source-table th,.source-table td { padding:1px 4px; vertical-align:middle; }
    .source-table thead th { position:sticky; top:0; z-index:1; background:var(--panel); }
    .source-table .line-state { min-width:46px; padding:0 3px; font-size:8px; line-height:1.15; }
    .source-code code { display:block; padding:1px 4px; white-space:pre; font:inherit; font-family:ui-monospace,SFMono-Regular,Consolas,monospace; }
"""


def load_core() -> Any:
    spec = importlib.util.spec_from_file_location(MODULE_NAME, CORE_PATH)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Could not load coverage generator core: {CORE_PATH}")

    module = importlib.util.module_from_spec(spec)
    sys.modules[MODULE_NAME] = module
    spec.loader.exec_module(module)
    return module


def main() -> int:
    core = load_core()
    original_render_report = core.render_report

    def render_dense_report(args: Any, root: Any, classes: Any) -> str:
        report = original_render_report(args, root, classes)
        style_end = "  </style>"
        if style_end not in report:
            raise RuntimeError("Generated coverage report does not contain a style block.")
        return report.replace(style_end, f"{DENSE_SOURCE_CSS}\n{style_end}", 1)

    core.render_report = render_dense_report
    return core.main()


if __name__ == "__main__":
    raise SystemExit(main())
