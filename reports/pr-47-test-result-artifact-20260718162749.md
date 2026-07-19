# PR #47 test result artifact 許可・検証記録

## 1. 目的

GitHub Actions の PR test 結果を、maintainer と ChatGPT-assisted review が同じ証跡として確認できるよう、TRX と manifest を workflow artifact に保存する。

## 2. ユーザー許可

2026-07-18 の対話で、repository owner であるユーザーから次の内容について明示的な許可を受けた。

- PR test 結果を GitHub Actions artifact に保存すること
- ChatGPT-assisted review が artifact を取得して test 結果を確認できるようにすること
- この許可を repository 上に記録すること

この許可に基づき、workflow 内の comment、artifact の `manifest.md`、PR 本文、および本記録へ明記する。

## 3. TDD 証跡

### RED

- commit: `42a2be46c24f49b4cafa996bcb55d11c041ecf60`
- workflow run: `29635715913`
- run number: `122`
- 変更: artifact 契約 test を workflow 実装より先に追加
- 結果: `Restore and run tests` が failure
- 理由: 既存 workflow に TRX logger、results directory、artifact upload、retention、許可文が存在しないため

### GREEN

- commit: `129f66ef7c36ce97c430a55f4ed8860dece0b189`
- workflow run: `29635756404`
- run number: `123`
- 結果: success
- `Restore and run tests`: success
- `Create test result manifest`: success
- `Upload .NET test results for ChatGPT review`: success

## 4. Artifact contract

- name: `ssc-pr-test-results-<run-id>-<run-attempt>`
- format: GitHub Actions ZIP artifact
- retention: 7日
- upload condition: test project が存在する場合、test 成否にかかわらず `always()` で実行
- missing file policy: error

保存内容:

```text
manifest.md
*.trx
```

`manifest.md` には repository、commit、workflow run、run attempt、PR番号、test project、TRX file、およびユーザー許可を記録する。

## 5. 実 artifact 検証

workflow run `29635756404` の artifact を GitHub connector 経由で取得し、ZIP 内容を確認した。

- artifact id: `8427039177`
- artifact name: `ssc-pr-test-results-29635756404-1`
- size: 32,937 bytes
- created: 2026-07-18T07:27:49Z
- expires: 2026-07-25T07:27:49Z
- digest: `sha256:9515c99761ebcdbb4b08ff65ad6dea4974fa07f3b3ef7fd5670feca4f57f35dd`

内容:

```text
manifest.md
tests_SSC.E2E.Tests_SSC.E2E.Tests.trx
tests_SSC.Unit.Tests_SSC.Unit.Tests.trx
```

TRX 集計:

| Test project | Total | Passed | Failed |
|---|---:|---:|---:|
| SSC.E2E.Tests | 87 | 87 | 0 |
| SSC.Unit.Tests | 73 | 73 | 0 |
| 合計 | 160 | 160 | 0 |

## 6. 対象範囲

本変更は PR test の観測可能性を追加するものであり、SSC production API、比較結果、NuGet package 内容には影響しない。

artifact には test runner が生成した TRX と workflow manifest だけを含める。source bundle、secret、credential、環境変数の全量は保存しない。
