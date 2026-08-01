# 投影済み差分pathからの値参照

## 1. 目的

`IParallelDiffPathProjector` によって生成した利用側定義pathを使い、該当する差分entryを取得し、そのentryが保持する各model slotの値と状態を自然に参照できるようにする。

既存の投影APIは、標準差分entryと利用側定義pathを `ParallelDiffEntryPathProjection` で対応付けている。

```csharp
var projections = result.GetDiffEntryPathProjections(projector);
```

現状でも値は次のように取得できる。

```csharp
var value = projection.Entry.Values[modelIndex].Value;
var state = projection.Entry.Values[modelIndex].State;
```

しかし、投影結果から値へ到達するために内部構造を辿る必要があり、利用側定義pathによる検索も利用者が毎回LINQで記述する必要がある。

```csharp
var projection = result
    .GetDiffEntryPathProjections(projector)
    .Single(x => x.ProjectedPath == targetPath);
```

本機能は、既存のprojector契約を維持したまま、次を追加する。

1. `ParallelDiffEntryPathProjection` から各model slotの値と状態を直接参照するAPI
2. 利用側定義pathの完全一致で投影結果を取得するAPI
3. 既存の `ParallelDiffPathPattern` で投影結果を絞り込むAPI

## 2. 前提

- 利用側定義pathは `IParallelDiffPathProjector` によって生成する。
- 標準pathの生成規則と `ParallelDiffEntry.Path` の契約は変更しない。
- 利用側定義pathを `CompareResult<T>.GetNodeByPath()` で解決可能なnode住所にはしない。
- 異なる標準pathが同じ利用側定義pathへ投影されることを許容する。
- したがって、利用側定義pathによる検索結果は0件、1件、複数件のいずれも取り得る。

## 3. 公開API

### 3.1 投影結果からの値参照

`ParallelDiffEntryPathProjection` に次を追加する。

```csharp
public sealed class ParallelDiffEntryPathProjection
{
    public int Count { get; }

    public object? this[int modelIndex] { get; }

    public ValueState GetState(int modelIndex);
}
```

#### Count

比較したmodel slot数を返す。

`Entry.Values.Count` と同じ値とする。

#### インデクサー

指定したmodel slotの値を返す。

```csharp
var before = projection[0];
var after = projection[1];
```

戻り値は `Entry.Values[modelIndex].Value` と同じ値とする。

#### GetState

指定したmodel slotの状態を返す。

```csharp
var state = projection.GetState(modelIndex);
```

戻り値は `Entry.Values[modelIndex].State` と同じ値とする。

#### modelIndexの検証

次の条件では `ArgumentOutOfRangeException` を送出する。

- `modelIndex < 0`
- `modelIndex >= Count`

インデクサーと `GetState()` は同一の範囲検証規則を使用する。

### 3.2 完全一致検索

既存の全件取得APIに、利用側定義pathを指定するoverloadを追加する。

```csharp
public static IReadOnlyList<ParallelDiffEntryPathProjection>
    GetDiffEntryPathProjections<T>(
        this CompareResult<T> result,
        IParallelDiffPathProjector projector,
        string projectedPath);
```

`ProjectedPath` が `projectedPath` とordinal完全一致する投影結果を返す。

比較は次と同じ意味とする。

```csharp
string.Equals(
    projection.ProjectedPath,
    projectedPath,
    StringComparison.Ordinal)
```

戻り値を単一要素にはしない。異なる標準pathが同じ利用側定義pathへ投影される可能性があるためである。

利用例:

```csharp
var matches = result.GetDiffEntryPathProjections(
    projector,
    "Root.Child1[0].Attribute1[0].Value");

foreach (var match in matches)
{
    var before = match[0];
    var after = match[1];
}
```

#### 引数検証

- `result == null`: `ArgumentNullException`
- `projector == null`: `ArgumentNullException`
- `projectedPath == null`: `ArgumentNullException`
- `projectedPath == string.Empty`: `ArgumentException`

### 3.3 pattern検索

既存の `ParallelDiffPathPattern` を指定するoverloadを追加する。

```csharp
public static IReadOnlyList<ParallelDiffEntryPathProjection>
    GetDiffEntryPathProjections<T>(
        this CompareResult<T> result,
        IParallelDiffPathProjector projector,
        ParallelDiffPathPattern pattern);
```

各投影結果の `ProjectedPath` に対し、既存の `ParallelDiffEntryPathProjectionExtensions.PathMatches()` と同じ判定を適用する。

利用例:

```csharp
var pattern = ParallelDiffPathPattern.Parse(
    "Root.Children[*].Fields[*].Value");

var matches = result.GetDiffEntryPathProjections(
    projector,
    pattern);

foreach (var match in matches)
{
    Console.WriteLine(match.ProjectedPath);
    Console.WriteLine(match[0]);
    Console.WriteLine(match[1]);
}
```

wildcardやescapeを含むpattern文法は、既存の `ParallelDiffPathPattern` の契約をそのまま使用する。新しいpattern文法は追加しない。

#### 引数検証

- `result == null`: `ArgumentNullException`
- `projector == null`: `ArgumentNullException`
- `pattern == null`: `ArgumentNullException`

## 4. 既存APIとの関係

既存の全件取得APIは維持する。

```csharp
result.GetDiffEntryPathProjections(projector);
```

用途ごとの入口は次の通りとする。

```csharp
// 全投影結果
var all = result.GetDiffEntryPathProjections(projector);

// 利用側定義pathの完全一致
var exact = result.GetDiffEntryPathProjections(
    projector,
    projectedPath);

// 利用側定義pathのpattern一致
var matched = result.GetDiffEntryPathProjections(
    projector,
    pattern);
```

既存の次の記述も引き続き可能である。

```csharp
var values = projection.Entry.Values;
```

今回追加する値参照APIは、`Entry` や `Values` を廃止するものではない。

## 5. 実装方針

### 5.1 値参照

`ParallelDiffEntryPathProjection` は、保持している `Entry.Values` へ委譲する。

新しい値の複製やcacheは行わない。

### 5.2 検索

完全一致検索とpattern検索は、既存の全件投影処理を再利用する。

概念上は次と同じ処理とする。

```csharp
result
    .GetDiffEntryPathProjections(projector)
    .Where(x => x.ProjectedPath == projectedPath)
    .ToArray();
```

```csharp
result
    .GetDiffEntryPathProjections(projector)
    .Where(x => x.PathMatches(pattern))
    .ToArray();
```

ただし、利用者が毎回このLINQを記述しなくてもよいよう、SSCの公開APIとして提供する。

### 5.3 順序

検索結果の順序は、既存の `GetDiffEntryPathProjections(projector)` が返す順序を維持する。

### 5.4 重複

同じ `ProjectedPath` を持つ複数結果を除外しない。

各結果は異なる標準差分entryを指す可能性があるため、重複排除すると情報が失われる。

## 6. 非対象

次は本機能の対象外とする。

- 利用側定義pathからnodeを逆引きするAPI
- 利用側定義pathから標準pathへの逆変換
- 同じ利用側定義pathを持つ投影結果の重複排除
- 同じ利用側定義pathを持つ投影結果の自動集約
- 1回の呼び出しで複数projectorを適用するAPI
- `ParallelDiffPathPattern` の文法拡張
- 標準pathの既存契約変更
- `ParallelDiffEntry` の値参照API変更

## 7. テスト観点

実装時はTDDで次を確認する。

### 7.1 値参照

- `Count` がmodel slot数を返す
- インデクサーが各slotの値を返す
- `GetState()` が各slotの状態を返す
- `Missing` のslotで値と状態が既存entryと一致する
- 負のindexで `ArgumentOutOfRangeException`
- `Count` と同じindexで `ArgumentOutOfRangeException`

### 7.2 完全一致検索

- 一致する投影結果だけを返す
- 一致しない場合は空一覧を返す
- 同じ利用側定義pathへ投影された複数entryをすべて返す
- 元の投影順序を維持する
- 大文字小文字を区別する
- nullおよび空文字列の例外契約

### 7.3 pattern検索

- 具体pathへの一致
- selector wildcard `[*]` への一致
- 既存escape規則への一致
- 一致しない場合は空一覧を返す
- 同じ利用側定義pathを持つ複数entryをすべて返す
- patternがnullの場合の例外契約

### 7.4 回帰

- 既存の全件取得結果が変わらない
- `ProjectedPath` と `ProjectedParentPath` の生成結果が変わらない
- `Entry` と `Entry.Values` の既存参照が変わらない
- 標準pathの `GetNodeByPath()`、`GetValueByPath()`、`GetStateByPath()` が変わらない
