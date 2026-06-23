using SSC.Internal;

namespace SSC.Unit.Tests;

public sealed class XPathLikePathParserUnitTests
{
    [Fact]
    public void TryParse_WithRootPrefixAndKeySelector_ParsesSegments()
    {
        // Intent: root type 名 prefix と key selector を grammar 通りに分解する。
        var parsed = AssertParsed("Dataset.Groups[1].Items[100].MetricA", rootName: "Dataset");

        Assert.Equal("Dataset", parsed.RootName);
        Assert.Equal(3, parsed.Segments.Count);
        Assert.Equal("Groups", parsed.Segments[0].MemberName);
        Assert.Equal(XPathLikePathSelectorKind.Key, parsed.Segments[0].Selector?.Kind);
        Assert.Equal("1", parsed.Segments[0].Selector?.KeyText);
        Assert.Equal("Items", parsed.Segments[1].MemberName);
        Assert.Equal("100", parsed.Segments[1].Selector?.KeyText);
        Assert.Equal("MetricA", parsed.Segments[2].MemberName);
        Assert.Null(parsed.Segments[2].Selector);
    }

    [Fact]
    public void TryParse_WithOrdinalSelector_ParsesOrdinal()
    {
        // Intent: # + digits は key ではなく ordinal discriminator として扱う。
        var parsed = AssertParsed("Items[#0].Name");

        Assert.Null(parsed.RootName);
        Assert.Equal(XPathLikePathSelectorKind.Ordinal, parsed.Segments[0].Selector?.Kind);
        Assert.Equal(0, parsed.Segments[0].Selector?.Ordinal);
        Assert.Null(parsed.Segments[0].Selector?.KeyText);
    }

    [Fact]
    public void TryParse_WithoutExpectedRootName_KeepsDottedPathAsSegments()
    {
        // Intent: root type 名を知らない parser 呼び出しでは、先頭 segment を root prefix と決め打ちしない。
        var parsed = AssertParsed("Customer.Name");

        Assert.Null(parsed.RootName);
        Assert.Equal("Customer", parsed.Segments[0].MemberName);
        Assert.Equal("Name", parsed.Segments[1].MemberName);
    }

    [Theory]
    [InlineData("Items[A.B]", "A.B")]
    [InlineData("Items[A\\]B]", "A]B")]
    [InlineData("Items[A\\\\B]", "A\\B")]
    [InlineData("Items[\\#0]", "#0")]
    public void TryParse_WithEscapedKeySelector_UnescapesKeyText(string path, string expectedKey)
    {
        // Intent: bracket 内の dot と escape 対象文字を key text として保持する。
        var parsed = AssertParsed(path);

        Assert.Equal(XPathLikePathSelectorKind.Key, parsed.Segments[0].Selector?.Kind);
        Assert.Equal(expectedKey, parsed.Segments[0].Selector?.KeyText);
    }

    [Theory]
    [InlineData("")]
    [InlineData(".Items")]
    [InlineData("Items.")]
    [InlineData("Items[]")]
    [InlineData("Items[#]")]
    [InlineData("Items[#A]")]
    [InlineData("Items[A")]
    [InlineData("Items[A\\x]")]
    [InlineData("Items[A][B]")]
    [InlineData("Items[#0][B]")]
    [InlineData("Items[A]B]")]
    public void TryParse_WithInvalidGrammar_ReturnsFalse(string path)
    {
        // Intent: 解釈できない path は例外ではなく parse 失敗として扱う。
        Assert.False(XPathLikePathParser.TryParse(path, out var parsed));
        Assert.Null(parsed);
    }

    private static XPathLikePath AssertParsed(string path)
    {
        Assert.True(XPathLikePathParser.TryParse(path, out var parsed));
        return Assert.IsType<XPathLikePath>(parsed);
    }

    private static XPathLikePath AssertParsed(string path, string rootName)
    {
        Assert.True(XPathLikePathParser.TryParse(path, rootName, out var parsed));
        return Assert.IsType<XPathLikePath>(parsed);
    }
}
