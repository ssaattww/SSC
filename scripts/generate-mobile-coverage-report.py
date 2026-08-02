#!/usr/bin/env python3
"""Generate a self-contained mobile-friendly HTML report from Cobertura XML."""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
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

LINE_STATUS_LABELS = {
    "covered": "実行済み",
    "uncovered": "未実行",
    "not-coverable": "対象外",
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
class SourceLineCoverage:
    number: int
    hits: int


@dataclass(frozen=True)
class ClassCoverage:
    name: str
    filename: str
    line_rate: float
    branch_rate: float
    methods: tuple[MethodCoverage, ...]
    lines: tuple[SourceLineCoverage, ...]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True, type=pathlib.Path)
    parser.add_argument("--output", required=True, type=pathlib.Path)
    parser.add_argument("--repository", required=True)
    parser.add_argument("--ref", required=True)
    parser.add_argument("--source-root", type=pathlib.Path, default=pathlib.Path.cwd())
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


def line_status(hits: int | None) -> str:
    if hits is None:
        return "not-coverable"
    if hits > 0:
        return "covered"
    return "uncovered"


def percent(rate: float) -> str:
    return f"{rate * 100:.1f}%"


def source_url(repository: str, ref: str, path: str, line: int | None) -> str:
    encoded_path = "/".join(quote(part, safe="") for part in path.split("/"))
    suffix = f"#L{line}" if line is not None else ""
    return f"https://github.com/{repository}/blob/{quote(ref, safe='')}/{encoded_path}{suffix}"


def source_anchor(path: str, line: int | None = None) -> str:
    digest = hashlib.sha1(path.encode("utf-8"), usedforsecurity=False).hexdigest()[:12]
    return f"source-{digest}" if line is None else f"source-{digest}-L{line}"


def parse_line_elements(line_elements: Iterable[ET.Element]) -> tuple[SourceLineCoverage, ...]:
    hits_by_line: dict[int, int] = {}
    for line_element in line_elements:
        number = int(line_element.attrib["number"])
        hits = int(line_element.attrib.get("hits", "0"))
        hits_by_line[number] = max(hits_by_line.get(number, 0), hits)
    return tuple(
        SourceLineCoverage(number=number, hits=hits)
        for number, hits in sorted(hits_by_line.items())
    )


def parse_classes(root: ET.Element) -> tuple[ClassCoverage, ...]:
    classes: list[ClassCoverage] = []
    for class_element in root.findall(".//class"):
        methods: list[MethodCoverage] = []
        method_line_elements: list[ET.Element] = []
        for method_element in class_element.findall("./methods/method"):
            line_elements = method_element.findall("./lines/line")
            method_line_elements.extend(line_elements)
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

        class_line_elements = class_element.findall("./lines/line")
        coverage_lines = parse_line_elements(class_line_elements or method_line_elements)
        classes.append(
            ClassCoverage(
                name=class_element.attrib.get("name", "(unknown)"),
                filename=normalize_source_path(class_element.attrib.get("filename", "")),
                line_rate=parse_rate(class_element.attrib.get("line-rate")),
                branch_rate=parse_rate(class_element.attrib.get("branch-rate")),
                methods=tuple(sorted(methods, key=lambda item: (item.line_rate, item.name))),
                lines=coverage_lines,
            )
        )

    return tuple(sorted(classes, key=lambda item: (item.line_rate, item.name)))


def collect_file_coverage(classes: tuple[ClassCoverage, ...]) -> dict[str, dict[int, int]]:
    files: dict[str, dict[int, int]] = {}
    for class_item in classes:
        hits_by_line = files.setdefault(class_item.filename, {})
        for line in class_item.lines:
            hits_by_line[line.number] = max(hits_by_line.get(line.number, 0), line.hits)
    return files


def read_source_lines(source_root: pathlib.Path, filename: str) -> tuple[str, ...] | None:
    root = source_root.resolve()
    candidate = (root / filename).resolve()
    try:
        candidate.relative_to(root)
    except ValueError:
        return None
    try:
        return tuple(candidate.read_text(encoding="utf-8", errors="replace").splitlines())
    except OSError:
        return None


def escape(value: object) -> str:
    return html.escape(str(value), quote=True)


def render_source_sections(
    args: argparse.Namespace,
    file_coverage: dict[str, dict[int, int]],
) -> str:
    sections: list[str] = []
    for filename, hits_by_line in sorted(file_coverage.items()):
        source_lines = read_source_lines(args.source_root, filename)
        file_anchor = source_anchor(filename)
        github_url = source_url(args.repository, args.ref, filename, None)
        if source_lines is None:
            sections.append(
                f"""
                <details class="source-file" id="{file_anchor}" data-source-search="{escape(filename.lower())}">
                  <summary><span>{escape(filename)}</span><span class="source-counts">ソース取得不可</span></summary>
                  <p class="source-unavailable">ローカルのソースファイルを読めませんでした。<a href="{escape(github_url)}">GitHubで開く</a></p>
                </details>
                """
            )
            continue

        covered_count = sum(hits > 0 for hits in hits_by_line.values())
        uncovered_count = sum(hits == 0 for hits in hits_by_line.values())
        not_coverable_count = max(0, len(source_lines) - len(hits_by_line))
        rows: list[str] = []
        for number, source_text in enumerate(source_lines, start=1):
            hits = hits_by_line.get(number)
            status = line_status(hits)
            hits_text = "—" if hits is None else str(hits)
            rows.append(
                f"""
                <tr id="{source_anchor(filename, number)}" class="source-row {status}-line" data-line-status="{status}" data-source-search="{escape((filename + ' ' + source_text).lower())}">
                  <td class="line-number"><a href="{escape(source_url(args.repository, args.ref, filename, number))}">{number}</a></td>
                  <td><span class="line-state {status}">{escape(LINE_STATUS_LABELS[status])}</span></td>
                  <td class="hit-count">{hits_text}</td>
                  <td class="source-code"><code>{escape(source_text)}</code></td>
                </tr>
                """
            )

        sections.append(
            f"""
            <details class="source-file" id="{file_anchor}" data-source-search="{escape(filename.lower())}">
              <summary>
                <span>{escape(filename)}</span>
                <span class="source-counts">実行済み {covered_count} · 未実行 {uncovered_count} · 対象外 {not_coverable_count}</span>
              </summary>
              <div class="source-tools"><a href="{escape(github_url)}">GitHubでファイルを開く</a></div>
              <div class="table-wrap source-table-wrap">
                <table class="source-table">
                  <thead><tr><th>行</th><th>行状態</th><th>Hits</th><th>Source</th></tr></thead>
                  <tbody>{''.join(rows)}</tbody>
                </table>
              </div>
            </details>
            """
        )
    return "".join(sections)


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
    file_coverage = collect_file_coverage(classes)

    class_sections: list[str] = []
    for class_item in classes:
        class_status = status_for(class_item.line_rate)
        method_rows: list[str] = []
        for method in class_item.methods:
            method_status = status_for(method.line_rate)
            line = method.uncovered_lines[0] if method.uncovered_lines else method.first_line
            github_link = source_url(args.repository, args.ref, class_item.filename, line)
            internal_link = f"#{source_anchor(class_item.filename, line)}" if line is not None else f"#{source_anchor(class_item.filename)}"
            method_rows.append(
                f"""
                <tr data-status="{method_status}" data-search="{escape((class_item.name + ' ' + method.name + ' ' + class_item.filename).lower())}">
                  <td><span class="state-badge {method_status}">{escape(status_label(method_status))}</span></td>
                  <td><a href="{internal_link}">{escape(method.name)}{escape(method.signature)}</a> <a class="external-link" href="{escape(github_link)}">GitHub</a></td>
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
                <a href="#{source_anchor(class_item.filename)}">行別表示へ移動</a>
                <a href="{escape(class_link)}">{escape(class_item.filename)}</a>
                <span>Branch {percent(class_item.branch_rate)}</span>
              </div>
              <div class="table-wrap">
                <table class="method-table">
                  <thead><tr><th>状態</th><th>Method</th><th>Line</th><th>Branch</th><th>Uncovered lines</th></tr></thead>
                  <tbody>{''.join(method_rows)}</tbody>
                </table>
              </div>
            </details>
            """
        )

    source_sections = render_source_sections(args, file_coverage)
    generated = dt.datetime.now(dt.UTC).replace(microsecond=0).isoformat()
    return f"""<!doctype html>
<html lang="ja">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{escape(args.title)}</title>
  <style>
    :root {{ color-scheme:light dark; --bg:#f6f8fa; --panel:#fff; --text:#1f2328; --muted:#59636e; --border:#d0d7de; --good:#1a7f37; --warn:#9a6700; --bad:#cf222e; --link:#0969da; --good-soft:#dafbe1; --bad-soft:#ffebe9; --neutral-soft:#f6f8fa; }}
    @media (prefers-color-scheme:dark) {{ :root {{ --bg:#0d1117; --panel:#161b22; --text:#e6edf3; --muted:#8d96a0; --border:#30363d; --good:#3fb950; --warn:#d29922; --bad:#f85149; --link:#58a6ff; --good-soft:#12261a; --bad-soft:#2d1418; --neutral-soft:#21262d; }} }}
    * {{ box-sizing:border-box; }}
    html {{ scroll-behavior:smooth; }}
    body {{ margin:0; background:var(--bg); color:var(--text); font:16px/1.5 -apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif; }}
    main {{ max-width:1100px; margin:auto; padding:16px; }}
    h1 {{ font-size:1.55rem; margin:0 0 4px; }}
    h2 {{ font-size:1.3rem; margin:28px 0 8px; }}
    .meta,.note {{ color:var(--muted); font-size:.88rem; overflow-wrap:anywhere; }}
    .cards {{ display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:10px; margin:16px 0; }}
    .card,.legend {{ background:var(--panel); border:1px solid var(--border); border-radius:10px; padding:12px; }}
    .card strong {{ display:block; font-size:1.2rem; }}
    .card span,.legend-row span:last-child {{ color:var(--muted); font-size:.85rem; }}
    .legend {{ display:grid; gap:6px; margin:12px 0; }}
    .legend-row {{ display:flex; align-items:flex-start; gap:8px; }}
    .controls {{ display:grid; grid-template-columns:1fr 170px; gap:8px; margin:14px 0; }}
    input,select {{ width:100%; font:inherit; color:inherit; background:var(--panel); border:1px solid var(--border); border-radius:8px; padding:10px; }}
    .class-block,.source-file {{ background:var(--panel); border:1px solid var(--border); border-radius:10px; margin:10px 0; overflow:hidden; scroll-margin-top:10px; }}
    summary {{ cursor:pointer; display:flex; align-items:center; justify-content:space-between; gap:8px; padding:12px; }}
    .class-name {{ font-weight:600; overflow-wrap:anywhere; }}
    .class-summary {{ display:flex; align-items:center; gap:6px; flex-shrink:0; }}
    .class-meta,.source-tools {{ display:flex; flex-wrap:wrap; justify-content:space-between; gap:8px; padding:0 12px 10px; color:var(--muted); font-size:.85rem; }}
    .source-counts {{ color:var(--muted); font-size:.82rem; text-align:right; }}
    .source-unavailable {{ padding:0 12px 12px; }}
    a {{ color:var(--link); }}
    .external-link {{ margin-left:6px; font-size:.78rem; }}
    .state-badge,.rate-badge,.line-state {{ display:inline-block; text-align:center; border-radius:999px; padding:2px 8px; font-weight:600; color:#fff; white-space:nowrap; }}
    .state-badge {{ min-width:92px; }} .rate-badge {{ min-width:62px; }} .line-state {{ min-width:72px; font-size:.78rem; }}
    .covered {{ background:var(--good); }} .partial {{ background:var(--warn); }} .uncovered {{ background:var(--bad); }} .not-coverable {{ background:var(--muted); }}
    .table-wrap {{ overflow-x:auto; border-top:1px solid var(--border); }}
    table {{ width:100%; border-collapse:collapse; font-size:.9rem; }}
    th,td {{ text-align:left; padding:9px 10px; border-bottom:1px solid var(--border); vertical-align:top; }}
    th {{ color:var(--muted); font-size:.8rem; }}
    .source-row.covered-line {{ background:var(--good-soft); }}
    .source-row.uncovered-line {{ background:var(--bad-soft); }}
    .source-row.not-coverable-line {{ background:var(--neutral-soft); color:var(--muted); }}
    .source-row:target {{ outline:3px solid var(--link); outline-offset:-3px; }}
    .line-number,.hit-count {{ white-space:nowrap; font-variant-numeric:tabular-nums; }}
    .source-code {{ min-width:520px; padding:0; }}
    .source-code code {{ display:block; padding:9px 10px; white-space:pre; font-family:ui-monospace,SFMono-Regular,Consolas,monospace; }}
    .hidden {{ display:none; }}
    @media (min-width:720px) {{ .cards {{ grid-template-columns:repeat(5,minmax(0,1fr)); }} }}
    @media (max-width:520px) {{ main {{ padding:12px; }} .controls {{ grid-template-columns:1fr; }} summary {{ align-items:flex-start; }} .class-summary {{ align-items:flex-end; flex-direction:column; }} .state-badge {{ min-width:84px; }} .source-counts {{ max-width:52%; }} }}
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
  <p class="note">method名をタップすると、このページ内の該当ソース行へ移動します。</p>
  <div class="controls">
    <label>検索<input id="search" type="search" placeholder="class / method / file"></label>
    <label>状態<select id="status"><option value="all">すべての状態</option><option value="covered">行カバー済み</option><option value="partial">一部カバー</option><option value="uncovered">未実行</option></select></label>
  </div>
  <section id="classes">{''.join(class_sections)}</section>

  <h2>行別カバレッジ</h2>
  <div class="legend" aria-label="行coverage状態の凡例">
    <div class="legend-row"><span class="line-state covered">実行済み</span><span>Coberturaのhit数が1以上の行です。</span></div>
    <div class="legend-row"><span class="line-state uncovered">未実行</span><span>カバレッジ対象ですがhit数が0の行です。</span></div>
    <div class="legend-row"><span class="line-state not-coverable">対象外</span><span>空行、宣言、波括弧など、Coberturaに実行対象として記録されていない行です。</span></div>
  </div>
  <div class="controls">
    <label>ソース検索<input id="source-search" type="search" placeholder="file / source text"></label>
    <label>行状態<select id="line-status"><option value="all">すべての行</option><option value="covered">実行済み</option><option value="uncovered">未実行</option><option value="not-coverable">対象外</option></select></label>
  </div>
  <section id="sources">{source_sections}</section>
</main>
<script>
  const search = document.getElementById('search');
  const status = document.getElementById('status');
  function applyMethodFilter() {{
    const query = search.value.trim().toLowerCase();
    const wanted = status.value;
    document.querySelectorAll('.class-block').forEach(block => {{
      const rows = [...block.querySelectorAll('.method-table tbody tr')];
      let visibleRows = 0;
      rows.forEach(row => {{
        const show = (!query || row.dataset.search.includes(query)) && (wanted === 'all' || row.dataset.status === wanted);
        row.classList.toggle('hidden', !show);
        if (show) visibleRows++;
      }});
      const classMatch = (!query || block.dataset.search.includes(query)) && (wanted === 'all' || block.dataset.status === wanted);
      const showBlock = visibleRows > 0 || classMatch;
      block.classList.toggle('hidden', !showBlock);
      if (query && showBlock) block.open = true;
    }});
  }}
  search.addEventListener('input', applyMethodFilter);
  status.addEventListener('change', applyMethodFilter);

  const sourceSearch = document.getElementById('source-search');
  const lineStatus = document.getElementById('line-status');
  function applyLineFilter() {{
    const query = sourceSearch.value.trim().toLowerCase();
    const wanted = lineStatus.value;
    document.querySelectorAll('.source-file').forEach(block => {{
      const rows = [...block.querySelectorAll('.source-row')];
      if (!rows.length) {{
        block.classList.toggle('hidden', !!query && !block.dataset.sourceSearch.includes(query));
        return;
      }}
      let visibleRows = 0;
      rows.forEach(row => {{
        const show = (!query || row.dataset.sourceSearch.includes(query)) && (wanted === 'all' || row.dataset.lineStatus === wanted);
        row.classList.toggle('hidden', !show);
        if (show) visibleRows++;
      }});
      const fileMatch = !query || block.dataset.sourceSearch.includes(query);
      const showBlock = visibleRows > 0 || (fileMatch && wanted === 'all');
      block.classList.toggle('hidden', !showBlock);
      if ((query || wanted !== 'all') && showBlock) block.open = true;
    }});
  }}
  sourceSearch.addEventListener('input', applyLineFilter);
  lineStatus.addEventListener('change', applyLineFilter);

  function openTargetSource() {{
    const target = document.querySelector(':target');
    if (target) {{
      const block = target.closest('.source-file');
      if (block) block.open = true;
    }}
  }}
  window.addEventListener('hashchange', openTargetSource);
  openTargetSource();
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
