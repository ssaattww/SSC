#!/usr/bin/env python3
"""Generate a compact mobile coverage index and one source-line page per file."""

from __future__ import annotations

import hashlib
import importlib.util
import pathlib
import re
import shutil
import sys
from typing import Any


CORE_PATH = pathlib.Path(__file__).with_name("generate-mobile-coverage-report-core.py")
MODULE_NAME = "_ssc_mobile_coverage_report_core"
SOURCE_PAGES_DIRECTORY = "files"
DENSE_SOURCE_CSS = """
    .source-table-wrap { max-height:76vh; overflow:auto; -webkit-overflow-scrolling:touch; }
    .source-table { width:100%; border-collapse:collapse; font-size:10px; line-height:1.05; -webkit-text-size-adjust:none; text-size-adjust:none; }
    .source-table th,.source-table td { padding:0 3px; vertical-align:middle; }
    .source-table thead th { position:sticky; top:0; z-index:1; background:var(--panel); }
    .source-table .line-state { min-width:40px; padding:0 2px; font-size:7px; line-height:1; }
    .source-table .line-number,.source-table .hit-count { width:1%; }
    .source-code { min-width:320px; padding:0; }
    .source-code code { display:block; padding:0 3px; white-space:pre; font-size:10px; line-height:1.05; font-weight:400; font-family:ui-monospace,SFMono-Regular,Consolas,monospace; -webkit-text-size-adjust:none; text-size-adjust:none; }
"""


def load_core() -> Any:
    spec = importlib.util.spec_from_file_location(MODULE_NAME, CORE_PATH)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Could not load coverage generator core: {CORE_PATH}")

    module = importlib.util.module_from_spec(spec)
    sys.modules[MODULE_NAME] = module
    spec.loader.exec_module(module)
    return module


def source_page_filename(filename: str) -> str:
    basename = pathlib.PurePosixPath(filename).name
    safe_basename = re.sub(r"[^A-Za-z0-9._-]+", "-", basename).strip("-") or "source"
    digest = hashlib.sha1(
        filename.encode("utf-8"),
        usedforsecurity=False,
    ).hexdigest()[:12]
    return f"{safe_basename}-{digest}.html"


def inject_dense_source_css(report: str) -> str:
    style_end = "  </style>"
    if style_end not in report:
        raise RuntimeError("Generated coverage report does not contain a style block.")
    return report.replace(style_end, f"{DENSE_SOURCE_CSS}\n{style_end}", 1)


def extract_style(report: str) -> str:
    match = re.search(r"<style>(.*?)</style>", report, flags=re.DOTALL)
    if match is None:
        raise RuntimeError("Generated coverage report does not contain CSS.")
    return match.group(1)


def render_source_rows(
    core: Any,
    args: Any,
    filename: str,
    source_lines: tuple[str, ...],
    hits_by_line: dict[int, int],
) -> str:
    rows: list[str] = []
    for number, source_text in enumerate(source_lines, start=1):
        hits = hits_by_line.get(number)
        status = core.line_status(hits)
        hits_text = "—" if hits is None else str(hits)
        rows.append(
            f"""
            <tr id="{core.source_anchor(filename, number)}" class="source-row {status}-line" data-line-status="{status}" data-source-search="{core.escape((filename + ' ' + source_text).lower())}">
              <td class="line-number"><a href="{core.escape(core.source_url(args.repository, args.ref, filename, number))}">{number}</a></td>
              <td><span class="line-state {status}">{core.escape(core.LINE_STATUS_LABELS[status])}</span></td>
              <td class="hit-count">{hits_text}</td>
              <td class="source-code"><code>{core.escape(source_text)}</code></td>
            </tr>
            """
        )
    return "".join(rows)


def render_source_page(
    core: Any,
    args: Any,
    filename: str,
    hits_by_line: dict[int, int],
    style: str,
) -> str:
    source_lines = core.read_source_lines(args.source_root, filename)
    github_url = core.source_url(args.repository, args.ref, filename, None)
    if source_lines is None:
        body = f"""
        <p>ローカルのソースファイルを読めませんでした。</p>
        <p><a href="{core.escape(github_url)}">GitHubでファイルを開く</a></p>
        """
        counts = "ソース取得不可"
    else:
        covered_count = sum(hits > 0 for hits in hits_by_line.values())
        uncovered_count = sum(hits == 0 for hits in hits_by_line.values())
        not_coverable_count = max(0, len(source_lines) - len(hits_by_line))
        counts = (
            f"実行済み {covered_count} · 未実行 {uncovered_count} · "
            f"対象外 {not_coverable_count}"
        )
        rows = render_source_rows(core, args, filename, source_lines, hits_by_line)
        body = f"""
        <div class="legend" aria-label="行coverage状態の凡例">
          <div class="legend-row"><span class="line-state covered">実行済み</span><span>Coberturaのhit数が1以上の行です。</span></div>
          <div class="legend-row"><span class="line-state uncovered">未実行</span><span>カバレッジ対象ですがhit数が0の行です。</span></div>
          <div class="legend-row"><span class="line-state not-coverable">対象外</span><span>Coberturaに実行対象として記録されていない行です。</span></div>
        </div>
        <div class="controls">
          <label>ソース検索<input id="source-search" type="search" placeholder="source text"></label>
          <label>行状態<select id="line-status"><option value="all">すべての行</option><option value="covered">実行済み</option><option value="uncovered">未実行</option><option value="not-coverable">対象外</option></select></label>
        </div>
        <div class="table-wrap source-table-wrap">
          <table class="source-table">
            <thead><tr><th>行</th><th>行状態</th><th>Hits</th><th>Source</th></tr></thead>
            <tbody>{rows}</tbody>
          </table>
        </div>
        """

    return f"""<!doctype html>
<html lang="ja">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{core.escape(filename)} - {core.escape(args.title)}</title>
  <style>{style}
    .file-page-nav {{ display:flex; justify-content:space-between; gap:8px; margin:0 0 10px; }}
    .file-page-path {{ overflow-wrap:anywhere; }}
  </style>
</head>
<body>
<main>
  <nav class="file-page-nav"><a href="../index.html">一覧へ戻る</a><a href="{core.escape(github_url)}">GitHubでファイルを開く</a></nav>
  <h1 class="file-page-path">{core.escape(filename)}</h1>
  <p class="meta">{core.escape(counts)}<br>Coverage source: <a href="https://github.com/{core.escape(args.repository)}/commit/{core.escape(args.ref)}"><code>{core.escape(args.ref)}</code></a></p>
  {body}
</main>
<script>
  const sourceSearch = document.getElementById('source-search');
  const lineStatus = document.getElementById('line-status');
  function applyLineFilter() {{
    if (!sourceSearch || !lineStatus) return;
    const query = sourceSearch.value.trim().toLowerCase();
    const wanted = lineStatus.value;
    document.querySelectorAll('.source-row').forEach(row => {{
      const show = (!query || row.dataset.sourceSearch.includes(query)) && (wanted === 'all' || row.dataset.lineStatus === wanted);
      row.classList.toggle('hidden', !show);
    }});
  }}
  if (sourceSearch && lineStatus) {{
    sourceSearch.addEventListener('input', applyLineFilter);
    lineStatus.addEventListener('change', applyLineFilter);
  }}
</script>
</body>
</html>
"""


def render_source_index(
    core: Any,
    args: Any,
    report: str,
    file_coverage: dict[str, dict[int, int]],
    page_paths: dict[str, str],
) -> str:
    for filename, relative_path in page_paths.items():
        anchor = core.source_anchor(filename)
        report = re.sub(
            rf'href="#({re.escape(anchor)}-L\d+)"',
            lambda match, path=relative_path: f'href="{path}#{match.group(1)}"',
            report,
        )
        report = report.replace(
            f'href="#{anchor}"',
            f'href="{relative_path}"',
        )

    file_entries: list[str] = []
    for filename, hits_by_line in sorted(file_coverage.items()):
        source_lines = core.read_source_lines(args.source_root, filename)
        if source_lines is None:
            counts = "ソース取得不可"
        else:
            covered_count = sum(hits > 0 for hits in hits_by_line.values())
            uncovered_count = sum(hits == 0 for hits in hits_by_line.values())
            not_coverable_count = max(0, len(source_lines) - len(hits_by_line))
            counts = (
                f"実行済み {covered_count} · 未実行 {uncovered_count} · "
                f"対象外 {not_coverable_count}"
            )
        relative_path = page_paths[filename]
        file_entries.append(
            f"""
            <details class="source-file" data-source-search="{core.escape(filename.lower())}">
              <summary><span>{core.escape(filename)}</span><span class="source-counts">{core.escape(counts)}</span></summary>
              <div class="source-tools"><a href="{relative_path}">このファイルの行別カバレッジを開く</a></div>
            </details>
            """
        )

    source_area = f"""
  <h2>ファイル別行カバレッジ</h2>
  <p class="note">ソースファイルごとにページを分割しています。ファイルを選ぶと、そのファイルの全行と実行状態を確認できます。</p>
  <div class="controls">
    <label>ファイル検索<input id="source-search" type="search" placeholder="file"></label>
    <select id="line-status" class="hidden" aria-hidden="true"><option value="all">すべて</option></select>
  </div>
  <section id="sources">{''.join(file_entries)}</section>
"""
    pattern = re.compile(
        r"\n  <h2>行別カバレッジ</h2>.*?<section id=\"sources\">.*?</section>",
        flags=re.DOTALL,
    )
    if pattern.search(report) is None:
        raise RuntimeError("Generated coverage report does not contain the source section.")
    return pattern.sub(f"\n{source_area.rstrip()}", report, count=1)


def main() -> int:
    core = load_core()
    original_render_report = core.render_report

    def render_multipage_report(args: Any, root: Any, classes: Any) -> str:
        report = inject_dense_source_css(original_render_report(args, root, classes))
        style = extract_style(report)
        file_coverage = core.collect_file_coverage(classes)
        pages_directory = args.output.parent / SOURCE_PAGES_DIRECTORY
        if pages_directory.exists():
            shutil.rmtree(pages_directory)
        pages_directory.mkdir(parents=True, exist_ok=True)

        page_paths: dict[str, str] = {}
        for filename, hits_by_line in sorted(file_coverage.items()):
            page_name = source_page_filename(filename)
            relative_path = f"{SOURCE_PAGES_DIRECTORY}/{page_name}"
            page_paths[filename] = relative_path
            (pages_directory / page_name).write_text(
                render_source_page(core, args, filename, hits_by_line, style),
                encoding="utf-8",
            )

        return render_source_index(core, args, report, file_coverage, page_paths)

    core.render_report = render_multipage_report
    return core.main()


if __name__ == "__main__":
    raise SystemExit(main())
