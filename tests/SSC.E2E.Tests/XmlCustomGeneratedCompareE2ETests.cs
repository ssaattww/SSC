using Sprache;
using SSC;
using SSC.Generated;

namespace SSC.E2E.Tests;

public sealed class XmlCustomGeneratedCompareE2ETests
{
    /// <summary>
    /// Verifies that the gist-equivalent XmlCustom parser can produce documents that compare successfully through the generated view.
    /// </summary>
    [Fact]
    public void Compare_GistXmlCustomParsedDocuments_SucceedsAndGeneratedViewCanAccessRootMembers()
    {
        var models = new[]
        {
            XmlCustom.Document.Parse(
                """
                <?xml version="1.0"?>
                <root id="same" source="left">
                    <section name="intro">Hello</section>
                    <empty flag="true"/>
                </root>
                """),
            XmlCustom.Document.Parse(
                """
                <?xml version="1.0"?>
                <root id="same" source="right">
                    <section name="intro">Hello</section>
                    <empty flag="true"/>
                </root>
                """),
        };

        var result = ParallelCompareApi.Compare(models);

        Assert.False(
            result.HasError,
            string.Join(Environment.NewLine, result.Issues.Select(static issue =>
                $"{issue.Level}: {issue.Code} path={issue.Path} model={issue.ModelIndex} key={issue.KeyText} message={issue.Message}")));
        Assert.DoesNotContain(result.Issues, issue => issue.Level == CompareIssueLevel.Error);

        var root = result.AsGeneratedView()!;

        Assert.Equal("root", root.Root.Name[0]);
        Assert.Equal("same", root.Root.Attribute[0].Value[0]);
        Assert.Equal("right", root.Root.Attribute[1].Value[1]);
        Assert.Equal(ValueState.Mismatched, root.Root.Attribute[1].Value.GetState(0));
        Assert.True(root.Root.Attribute[0].Range.StartLine[0] > 0);
        Assert.Equal("section", root.Root.ChildrenOfNode[0].Name[0]);
        Assert.Equal("intro", root.Root.ChildrenOfNode[0].Attribute[0].Value[1]);
    }
}

public struct TextRange
{
    public int StartPos;
    public int EndPos;
    public int StartLine;
    public int StartColumn;
    public int EndLine;
    public int EndColumn;
}

public sealed class PositionedValue<T> : IPositionAware<PositionedValue<T>>
{
    public T Value = default!;
    public int Pos;
    public int Length;
    public int Line;
    public int Column;

    public PositionedValue<T> SetPos(Position startPos, int length)
    {
        Pos = startPos.Pos;
        Line = startPos.Line;
        Column = startPos.Column;
        Length = length;
        return this;
    }
}

public sealed class XmlAttribute
{
    public required string Name;
    public required string Value;

    public TextRange Range;
}

[GenerateParallelView]
public sealed class Document
{
    public Node Root { get; init; } = new();
}

public class Item
{
}

public sealed class Content : Item
{
    public string Text { get; init; } = string.Empty;

    public TextRange Range;
}

public sealed class Node : Item
{
    public string Name { get; init; } = string.Empty;

    public Dictionary<string, XmlAttribute>? Attribute;

    public IEnumerable<Item>? Children;

    public IEnumerable<Node>? ChildrenOfNode => Children?.Where(static child => child is Node).Cast<Node>();

    public TextRange Range;

    public TextRange BeginTagRange;

    public TextRange EndTagRange;
}

public static class XmlCustom
{
    private static readonly Parser<Content> Content =
        (
            from text in Parse.CharExcept('<').AtLeastOnce().Text()
            where !string.IsNullOrWhiteSpace(text)
            select text
        )
        .Select(static text => new PositionedValue<string> { Value = text })
        .Positioned()
        .Select(static value => new Content
        {
            Text = value.Value,
            Range = CreateTextRange(value),
        });

    private static readonly CommentParser Comment = new("<!--", "-->", "\r\n");

    private static readonly Parser<Item> Item =
        from leadingWs in Parse.WhiteSpace.Many()
        from leading in Comment.MultiLineComment.Many()
        from item in Node.Select(static node => (Item)node).XOr(Content)
        from trailing in Comment.MultiLineComment.Many()
        from trailingWs in Parse.WhiteSpace.Many()
        select item;

    private static readonly Parser<string> Identifier =
        from rest in Parse.LetterOrDigit.XOr(Parse.Char('-')).XOr(Parse.Char('_')).Many()
        select new string(rest.ToArray());

    private static readonly Parser<XmlAttribute> Attribute =
        (
            from key in Identifier.Token()
            from eq in Parse.Char('=').Token()
            from bq in Parse.Char('"')
            from val in Parse.CharExcept('"').Many().Text()
            from eq2 in Parse.Char('"')
            select new PositionedValue<(string key, string val)>
            {
                Value = (key, val),
            }
        )
        .Positioned()
        .Select(static value => new XmlAttribute
        {
            Name = value.Value.key,
            Value = value.Value.val,
            Range = new TextRange
            {
                StartPos = value.Pos,
                EndPos = value.Pos + value.Length,
                StartLine = value.Line,
                StartColumn = value.Column,
                EndLine = value.Line,
                EndColumn = value.Column + value.Length,
            },
        });

    private static readonly Parser<(string Name, IEnumerable<XmlAttribute> Attributes, TextRange Range)> BeginTag =
        Tag(
            from id in Identifier
            from attributes in Attribute.Many()
            select new PositionedValue<(string, IEnumerable<XmlAttribute>)>
            {
                Value = (id, attributes),
            }
        )
        .Positioned()
        .Select(static value => (
            value.Value.Item1,
            value.Value.Item2,
            new TextRange
            {
                StartPos = value.Pos,
                EndPos = value.Pos + value.Length,
                StartLine = value.Line,
                StartColumn = value.Column,
                EndLine = value.Line,
                EndColumn = value.Column + value.Length,
            }));

    private static readonly Parser<Node> FullNode =
        from beginTag in BeginTag
        from nodes in Parse.Ref(() => Item).Many()
        from endTag in EndTag(beginTag.Name)
        select new Node
        {
            Name = beginTag.Name,
            Attribute = beginTag.Attributes.ToDictionary(static attribute => attribute.Name, static attribute => attribute),
            Children = nodes,
            BeginTagRange = beginTag.Range,
            EndTagRange = endTag.Range,
            Range = new TextRange
            {
                StartPos = beginTag.Range.StartPos,
                EndPos = endTag.Range.EndPos,
                StartLine = beginTag.Range.StartLine,
                StartColumn = beginTag.Range.StartColumn,
                EndLine = endTag.Range.EndLine,
                EndColumn = endTag.Range.EndColumn,
            },
        };

    private static readonly Parser<Node> ShortNode =
        Tag(
            from id in Identifier
            from attributes in Attribute.Many()
            from slash in Parse.Char('/')
            select new PositionedValue<(string Id, IEnumerable<XmlAttribute> Attributes)>
            {
                Value = (id, attributes),
            }
        )
        .Positioned()
        .Select(static value =>
        {
            var range = new TextRange
            {
                StartPos = value.Pos,
                EndPos = value.Pos + value.Length,
                StartLine = value.Line,
                StartColumn = value.Column,
                EndLine = value.Line,
                EndColumn = value.Column + value.Length,
            };

            return new Node
            {
                Name = value.Value.Id,
                Attribute = value.Value.Attributes.ToDictionary(static attribute => attribute.Name, static attribute => attribute),
                Range = range,
                BeginTagRange = range,
                EndTagRange = range,
            };
        });

    private static readonly Parser<Node> Node = ShortNode.Or(FullNode);

    private static readonly Parser<string> XmlVersion = Tag(
        from q1 in Parse.Char('?')
        from id in Identifier
        from attribute in Attribute.AtLeastOnce()
        from q2 in Parse.Char('?')
        select attribute.First().Value);

    public static readonly Parser<Document> Document =
        (
            from leading in Parse.WhiteSpace.Many()
            from xmlVersion in XmlVersion.Many()
            from whitespace in Parse.WhiteSpace.Many()
            from node in Node
            from trailing in Parse.WhiteSpace.Many()
            select new Document { Root = node }
        ).End();

    private static TextRange CreateTextRange(PositionedValue<string> value)
    {
        var endLine = value.Line;
        var endColumn = value.Column;

        for (var index = 0; index < value.Value.Length; index++)
        {
            if (value.Value[index] == '\r')
            {
                if (index + 1 < value.Value.Length && value.Value[index + 1] == '\n')
                {
                    index++;
                }

                endLine++;
                endColumn = 1;
                continue;
            }

            if (value.Value[index] == '\n')
            {
                endLine++;
                endColumn = 1;
                continue;
            }

            endColumn++;
        }

        return new TextRange
        {
            StartPos = value.Pos,
            EndPos = value.Pos + value.Length,
            StartLine = value.Line,
            StartColumn = value.Column,
            EndLine = endLine,
            EndColumn = endColumn,
        };
    }

    private static Parser<T> Tag<T>(Parser<T> content)
    {
        return
            from lt in Parse.Char('<')
            from value in content
            from gt in Parse.Char('>')
            select value;
    }

    private static Parser<(string Name, TextRange Range)> EndTag(string name)
    {
        return Tag(
            from slash in Parse.Char('/')
            from id in Identifier
            where id == name
            select new PositionedValue<string> { Value = id }
        )
        .Positioned()
        .Select(static value => (
            value.Value,
            new TextRange
            {
                StartPos = value.Pos,
                EndPos = value.Pos + value.Length,
                StartLine = value.Line,
                StartColumn = value.Column,
                EndLine = value.Line,
                EndColumn = value.Column + value.Length,
            }));
    }
}
