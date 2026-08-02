#!/usr/bin/env python3
"""Generate a self-contained mobile-friendly HTML report from Cobertura XML."""

from __future__ import annotations

import argparse
import datetime as dt
import html
import pathlib
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from typing import Iterable
from urllib.parse import quote


STATUS_LABELS = {
    "covered": "行カバー済み",
    "partial": "一部カバー",
    "uncovered": "未実行",
}


@dataclass(frozen=True)
class MethodCoverage:
    name: str
    signature: str
    line_rate: float
    branch_rate: float
    first_line: int | None
    uncovered_lines: tuple[int, ...]


@dataclass(frozen=True)
class ClassCoverage:
    name: str
    filename: str
    line_rate: float
    branch_rate: float
    methods: tuple[MethodCoverage, ...]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True, type=pathlib.Path)
    parser.add_argument("--output", required=True, type=pathlib.Path)
    parser.add_argument("--repository", required=True)
    parser.add_argument("--ref", required=True)
    parser.add_argument("--title", default="SSC code coverage")
    return parser.parse_args()


def parse_rate(value: str | None) -> float:
    try:
        return max(0.0, min(1.0, float(value or "0")))
    except ValueError:
        return 0.0


def normalize_source_path(filename: str) -> str:
    normalized = filename.replace("\\", "/")
    for marker in ("/src/", "/tests/", "/scripts/"):
        if marker in normalized:
            return f"{marker.strip('/')}/{normalized.split(marker, 1)[1]}"
    return normalized.lstrip("/")


def compact_line_ranges(lines: Iterable[int]) -> str:
    values = sorted(set(lines))
    if not values:
        return "—"

    ranges: list[str] = []
    start = previous = values[0]
    for value in values[1:]:
        if value == previous + 1:
            previous = value
            continue
        ranges.append(str(start) if start == previous else f"{start}-{previous}")
        start = previous = value
    ranges.append(str(start) if start == previous else f"{start}-{previous}")
    return ", ".join(ranges)


def status_for(rate: float) -> str:
    if rate <= 0:
        return "uncovered"
    if rate >= 1:
        return "covered"
    return "partial"


def status_label(status: str) -> str:
    return STATUS_LABELS[status]


def percent(rate: float) -> str:
    return f"{rate * 100:.1f}%"


def source_url(repository: str, ref: str, path: str, line: int | None) -> str:
    encoded_path = "/".join(quote(part, safe="") for part in path.split("/"))
    suffix = f"#L{line}" if line is not None else ""
    return f"https://github.com/{repository}/blob/{quote(ref, safe='')}/{encoded_path}{suffix}"


def parse_classes(root: ET.Element) -> tuple[ClassCoverage, ...]:
    classes: list[ClassCoverage] = []
    for class_element in root.findall(".//class"):
        methods: list[MethodCoverage] = []
        for method_element in class_element.findall("./methods/method"):
            line_elements = method_element.findall("./lines/line")
            line_numbers = [int(line.attrib["number"]) for line in line_elements]
            uncovered = tuple(
                int(line.attrib["number"])
                for line in line_elements
                if int(line.attrib.get("hits", "0")) == 0
            )
            methods.append(
                MethodCoverage(
                    name=method_element.attrib.get("name", "(unknown)"),
                    signature=method_element.attrib.get("signature", ""),
                    line_rate=parse_rate(method_element.attrib.get("line-rate")),
                    branch_rate=parse_rate(method_element.attrib.get("branch-rate")),
                    first_line=min(line_numbers) if line_numbers else None,
                    uncovered_lines=uncovered,
                )
            )

        classes.append(
            ClassCoverage(
                name=class_element.attrib.get("name", "(unknown)"),
                filename=normalize_source_path(class_element.attrib.get("filename", "")),
                line_rate=parse_rate(class_element.attrib.get("line-rate")),
                branch_rate=parse_rate(class_element.attrib.get("branch-rate")),
                methods=tuple(sorted(methods, key=lambda item: (item.line_rate, item.name))),
            )
        )

    return tuple(sorted(classes, key=lambda item: (item.line_rate, item.name)))


def escape(value: object) -> str:
    return html.escape(str(value), quote=True)


def render_report(args: argparse.Namespace, root: ET.Element, classes: tuple[ClassCoverage, ...]) -> str:
    line_rate = parse_rate(root.attrib.get("line-rate"))
    branch_rate = parse_rate(root.attrib.get("branch-rate"))
    lines_covered = int(root.attrib.get("lines-covered", "0"))
    lines_valid = int(root.attrib.get("lines-valid", "0"))
    branches_covered = int(root.attrib.get("branches-covered", "0"))
    branches_valid = int(root.attrib.get("branches-valid", "0"))

    all_methods = [method for item in classes for method in item.methods]
    fully_covered_methods = sum(method.line_rate >= 1 for method in all_methods)
    partial_methods = sum(0 < method.line_rate < 1 for method in all_methods)
    uncovered_methods = sum(method.line_rate <= 0 for method in all_methods)

    class_sections: list[str] = []
    for class_item in classes:
        class_status = status_for(class_item.line_rate)
        method_rows: list[str] = []
        for method in class_item.methods:
            method_status = status_for(method.line_rate)
            line = method.uncovered_lines[0] if method.uncovered_lines else method.first_line
            link = source_url(args.repository, args.ref, class_item.filename, line)
            method_rows.append(
                f"""
                <tr data-status="{method_status}" data-search="{escape((class_item.name + ' ' + method.name + ' ' + class_item.filename).lower())}">
                  <td><span class="state-badge {method_status}">{escape(status_label(method_status))}</span></td>
                  <td><a href="{escape(link)}">{escape(method.name)}{escape(method.signature)}</a></td>
                  <td><span class="rate-badge {method_status}">{percent(method.line_rate)}</span></td>
                  <td>{percent(method.branch_rate)}</td>
                  <td>{escape(compact_line_ranges(method.uncovered_lines))}</td>
                </tr>
                """
            )

        class_link = source_url(args.repository, args.ref, class_item.filename, None)
        class_sections.append(
            f"""
            <details class="class-block" data-status="{class_status}" data-search="{escape((class_item.name + ' ' + class_item.filename).lower())}">
              <summary>
                <span class="class-name">{escape(class_item.name)}</span>
                <span class="class-summary">
                  <span class="state-badge {class_status}">{escape(status_label(class_status))}</span>
                  <span class="rate-badge {class_status}">{percent(class_item.line_rate)}</span>
                </span>
              </summary>
              <div class="class-meta">
                <a href="{escape(class_link)}">{escape(class_item.filename)}</a>
                <span>Branch {percent(class_item.branch_rate)}</span>
              </div>
              <div class="table-wrap">
                <table>
                  <thead><tr><th>状態</th><th>Method</th><th>Line</th><th>Branch</th><th>Uncovered lines</th></tr></thead>
                  <tbody>{''.join(method_rows)}</tbody>
                </table>
              </div>
            </details>
            """
        )

    generated = dt.datetime.now(dt.UTC).replace(microsecond=0).isoformat()
    return f"""<!doctype html>
<html lang="ja">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{escape(args.title)}</title>
  <style>
    :root {{ color-scheme: light dark; --bg:#f6f8fa; --panel:#fff; --text:#1f2328; --muted:#59636e; --border:#d0d7de; --good:#1a7f37; --warn:#9a6700; --bad:#cf222e; --link:#0969da; }}
    @media (prefers-color-scheme: dark) {{ :root {{ --bg:#0d1117; --panel:#161b22; --text:#e6edf3; --muted:#8d96a0; --border:#30363d; --good:#3fb950; --warn:#d29922; --bad:#f85149; --link:#58a6ff; }} }}
    * {{ box-sizing:border-box; }}
    body {{ margin:0; background:var(--bg); color:var(--text); font:16px/1.5 -apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif; }}
    main {{ max-width:1000px; margin:auto; padding:16px; }}
    h1 {{ font-size:1.55rem; margin:0 0 4px; }}
    .meta {{ color:var(--muted); font-size:.88rem; overflow-wrap:anywhere; }}
    .cards {{ display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:10px; margin:16px 0; }}
    .card {{ background:var(--panel); border:1px solid var(--border); border-radius:10px; padding:12px; }}
    .card strong {{ display:block; font-size:1.2rem; }}
    .card span {{ color:var(--muted); font-size:.85rem; }}
    .legend {{ display:grid; gap:6px; background:var(--panel); border:1px solid var(--border); border-radius:10px; padding:12px; margin:12px 0; }}
    .legend-row {{ display:flex; align-items:flex-start; gap:8px; }}
    .legend-row span:last-child {{ color:var(--muted); font-size:.88rem; }}
    .controls {{ display:grid; grid-template-columns:1fr 170px; gap:8px; margin:14px 0; }}
    input,select {{ width:100%; font:inherit; color:inherit; background:var(--panel); border:1px solid var(--border); border-radius:8px; padding:10px; }}
    .class-block {{ background:var(--panel); border:1px solid var(--border); border-radius:10px; margin:10px 0; overflow:hidden; }}
    summary {{ cursor:pointer; display:flex; align-items:center; justify-content:space-between; gap:8px; padding:12px; }}
    .class-name {{ font-weight:600; overflow-wrap:anywhere; }}
    .class-summary {{ display:flex; align-items:center; gap:6px; flex-shrink:0; }}
    .class-meta {{ display:flex; flex-wrap:wrap; justify-content:space-between; gap:6px; padding:0 12px 10px; color:var(--muted); font-size:.85rem; }}
    a {{ color:var(--link); }}
    .state-badge,.rate-badge {{ display:inline-block; text-align:center; border-radius:999px; padding:2px 8px; font-weight:600; color:#fff; white-space:nowrap; }}
    .state-badge {{ min-width:92px; }}
    .rate-badge {{ min-width:62px; }}
    .covered {{ background:var(--good); }} .partial {{ background:var(--warn); }} .uncovered {{ background:var(--bad); }}
    .table-wrap {{ overflow-x:auto; border-top:1px solid var(--border); }}
    table {{ width:100%; border-collapse:collapse; font-size:.9rem; }}
    th,td {{ text-align:left; padding:9px 10px; border-bottom:1px solid var(--border); vertical-align:top; }}
    th {{ color:var(--muted); font-size:.8rem; }}
    .hidden {{ display:none; }}
    .note {{ color:var(--muted); font-size:.88rem; }}
    @media (min-width:720px) {{ .cards {{ grid-template-columns:repeat(5,minmax(0,1fr)); }} }}
    @media (max-width:520px) {{
      main {{ padding:12px; }}
      .controls {{ grid-template-columns:1fr; }}
      summary {{ align-items:flex-start; }}
      .class-summary {{ align-items:flex-end; flex-direction:column; }}
      th:nth-child(4),td:nth-child(4) {{ display:none; }}
      .state-badge {{ min-width:84px; }}
    }}
  </style>
</head>
<body>
<main>
  <h1>{escape(args.title)}</h1>
  <div class="meta">Coverage source: <a href="https://github.com/{escape(args.repository)}/commit/{escape(args.ref)}"><code>{escape(args.ref)}</code></a><br>Generated: {escape(generated)}</div>
  <div class="cards">
    <div class="card"><strong>{percent(line_rate)}</strong><span>全体 Line · {lines_covered}/{lines_valid}</span></div>
    <div class="card"><strong>{percent(branch_rate)}</strong><span>全体 Branch · {branches_covered}/{branches_valid}</span></div>
    <div class="card"><strong>完全カバー {fully_covered_methods}</strong><span>行カバー率100%のmethod</span></div>
    <div class="card"><strong>部分カバー {partial_methods}</strong><span>行カバー率1〜99.9%</span></div>
    <div class="card"><strong>未カバー {uncovered_methods}</strong><span>行カバー率0%のmethod</span></div>
  </div>
  <div class="legend" aria-label="coverage状態の凡例">
    <div class="legend-row"><span class="state-badge covered">行カバー済み</span><span>そのmethodのcoverable lineがすべて実行済みです。Branch列は別に確認してください。</span></div>
    <div class="legend-row"><span class="state-badge partial">一部カバー</span><span>methodは実行されていますが、未実行行が残っています。</span></div>
    <div class="legend-row"><span class="state-badge uncovered">未実行</span><span>そのmethodのcoverable lineは一度も実行されていません。</span></div>
  </div>
  <p class="note">状態はmethodのLine coverageで判定します。緑でもBranchが100%未満なら未通過分岐があります。method名をタップすると対象ソースへ移動します。</p>
  <div class="controls">
    <label>検索<input id="search" type="search" placeholder="class / method / file"></label>
    <label>状態<select id="status"><option value="all">すべての状態</option><option value="covered">行カバー済み</option><option value="partial">一部カバー</option><option value="uncovered">未実行</option></select></label>
  </div>
  <section id="classes">{''.join(class_sections)}</section>
</main>
<script>
  const search = document.getElementById('search');
  const status = document.getElementById('status');
  function applyFilter() {{
    const query = search.value.trim().toLowerCase();
    const wanted = status.value;
    document.querySelectorAll('.class-block').forEach(block => {{
      const rows = [...block.querySelectorAll('tbody tr')];
      let visibleRows = 0;
      rows.forEach(row => {{
        const searchMatch = !query || row.dataset.search.includes(query);
        const statusMatch = wanted === 'all' || row.dataset.status === wanted;
        row.classList.toggle('hidden', !(searchMatch && statusMatch));
        if (searchMatch && statusMatch) visibleRows++;
      }});
      const classSearchMatch = !query || block.dataset.search.includes(query);
      const classStatusMatch = wanted === 'all' || block.dataset.status === wanted;
      const show = visibleRows > 0 || (classSearchMatch && classStatusMatch);
      block.classList.toggle('hidden', !show);
      if ((query || wanted !== 'all') && show) block.open = true;
    }});
  }}
  search.addEventListener('input', applyFilter);
  status.addEventListener('change', applyFilter);
</script>
</body>
</html>
"""


def main() -> int:
    args = parse_args()
    try:
        root = ET.parse(args.input).getroot()
    except (OSError, ET.ParseError) as error:
        print(f"Failed to read Cobertura report: {error}", file=sys.stderr)
        return 1

    classes = parse_classes(root)
    if not classes:
        print("Cobertura report contains no classes.", file=sys.stderr)
        return 1

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(render_report(args, root, classes), encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
