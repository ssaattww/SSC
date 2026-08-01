# T-094 Issue #50 投影済み差分pathからの値参照API 初回レビュー

## 1. レビュー対象

- Repository: `ssaattww/SSC`
- Issue: #50
- Pull Request: #51
- Review mode: initial review
- Branch: `design/issue-50-projected-path-value-access`
- Base: `main`
- Reviewed implementation HEAD: `d82f60c52fd46836fe0235d463ee885a339b851c`
- Reviewer continuity: このチャットは実装を行っておらず、初回レビューとして実施した。

## 2. 対象範囲

次を確認した。

- Issue #50とPR #51の説明
- PRの全変更ファイル
- 投影結果からのmodel slot値・状態参照API
- 投影済みpathの完全一致検索とpattern検索
- 引数・index境界の例外契約
- 既存projector、path pattern、投影結果APIとの整合
- unit testのhappy path・境界・error path
- 設計書、実装レポート、PR本文、task追跡の整合
- テスト失敗時の診断artifact workflow
- reviewed HEADと一致するGitHub Actions runおよびartifact

## 3. 検証証拠

Reviewed implementation HEAD `d82f60c52fd46836fe0235d463ee885a339b851c` に一致するworkflow runを確認した。

- Workflow: `PR .NET Tests`
- Run: `30685161775`
- Conclusion: `success`
- Artifact: `8813632905`
- Artifact name: `ssc-pr-test-diagnostics-30685161775-1`
- Artifact workflow head SHA: `d82f60c52fd46836fe0235d463ee885a339b851c`

別SHAのrunは代用していない。

## 4. Coverage disposition

| 観点 | 判定 | 証拠 |
|---|---|---|
| 要件・設計との一致 | checked finding | 値・状態参照、完全一致、pattern検索は実装済み。重複保持・順序保持の契約に直接対応するtestがない。 |
| 値・状態参照 | checked no finding | `Count`、indexer、`GetState()`が`Entry.Values`へ委譲し、範囲外を拒否する。 |
| 完全一致検索 | checked no finding | `StringComparison.Ordinal`を使用し、0件・1件をtestしている。 |
| pattern検索 | checked no finding | 既存`PathMatches()`を再利用し、wildcard例をtestしている。 |
| 重複・順序契約 | checked finding | 設計に明記されているが、同一投影pathが複数entryになるcaseと順序維持をtestしていない。 |
| 引数検証 | checked no finding | nullおよび空文字列をtestしている。 |
| 公開API documentation | checked no finding | 追加public APIに日本語XML documentationがある。 |
| workflow診断artifact | checked no finding | TRX、stdout、stderr、source、HEAD、git statusをartifact配下へ保存する。 |
| current HEAD CI | checked no finding | run `30685161775`がreviewed HEADと一致しsuccess。 |
| task追跡・PR説明 | checked finding | `tasks/tasks-status.md`にT-094がなく、PR本文は設計のみと記載したまま実装差分を反映していない。 |

## 5. Findings

### F-001 Medium: 重複保持・順序保持の公開契約を回帰testで固定していない

- Origin: coverage miss
- Location: `tests/SSC.Unit.Tests/Issue50ProjectedPathValueAccessTddTests.cs`
- Related design: `doc/design/detail/12-DiffEntryProjectedPathValueAccess.md` の「順序」「重複」

設計では、異なる標準pathが同一の`ProjectedPath`へ投影され得るため、検索結果を単一化せず、重複を保持し、既存全件投影APIの順序を維持すると明示している。

現在のtestは完全一致検索で1件、pattern検索で異なる2pathを確認しているだけであり、次を検出できない。

- 実装が将来`DistinctBy(ProjectedPath)`等で重複を除去する退行
- 検索結果がsort等により元の投影順序を失う退行
- 同一投影pathを持つ複数entryの値・標準path対応が失われる退行

Required action:

- 複数の異なる標準pathをprojectorで同一の利用側定義pathへ投影するfixtureを追加する。
- 完全一致検索が全entryを元順序のまま返すことをassertする。
- 必要に応じてpattern検索にも同じ重複保持契約をassertする。

### F-002 Medium: task追跡とPR説明が現行差分に追随していない

- Origin: introduced by change / process completeness
- Location: `tasks/tasks-status.md`, PR #51 body

`tasks/tasks-status.md`はIn Progress/Backlogが「なし」のままで、実装レポートが参照するT-094を記録していない。PR本文も「設計を追加」「設計書のみの変更」と記載したままだが、現行差分にはworkflow、production code、unit test、実装レポートが含まれる。

この状態では、対象範囲、実装完了条件、検証内容、残作業をrepositoryとPRから正確に追跡できない。

Required action:

- `tasks/tasks-status.md`へT-094のscope、exit criteria、変更、検証、PR #51を記録する。
- PR #51のtitle/bodyを実装済み内容、TDD、current-HEAD CI、診断artifact、残るレビュー対応が分かる内容へ更新する。
- Issue #50を閉じる条件は、実装・レビュー完了後のPR本文で明確にする。

## 6. Held / unexplored

- Held: なし。
- Unexplored: package公開後の外部利用コード互換性。今回の追加APIは既存APIを削除・変更していないため、verdictへの影響は限定的。
- Artifact ZIPの全ファイル内容の再ダウンロード検査は未実施。ただしartifact metadata、workflow定義、同一HEAD runの成功を確認した。

## 7. Verdict

**fail**

実装の主要ロジックとcurrent-HEAD CIは支持されるが、公開契約である重複・順序保持のtest不足、および必須のtask追跡・PR説明の不整合が残るため、修正が必要である。

## 8. 次のアクション

実装チャットでF-001、F-002を修正し、変更後HEADに一致するworkflow runを確認する。その後、この通常レビューチャットでfix verificationを実施する。

mergeは実施しない。
