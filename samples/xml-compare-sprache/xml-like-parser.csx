#nullable enable

using Sprache;

public static class XmlLikeParser
{
    private static readonly Parser<string> Name =
        Parse.Char(IsNameChar, "name character").AtLeastOnce().Text();

    private static readonly Parser<string> QuotedValue =
        from open in Parse.Char('"')
        from value in Parse.CharExcept('"').Many().Text()
        from close in Parse.Char('"')
        select value;

    private static readonly Parser<XmlAttributeNode> Attribute =
        from leading in Parse.WhiteSpace.AtLeastOnce()
        from name in Name
        from beforeEquals in Parse.WhiteSpace.Many()
        from eq in Parse.Char('=')
        from afterEquals in Parse.WhiteSpace.Many()
        from value in QuotedValue
        select new XmlAttributeNode(name, value);

    private static readonly Parser<XmlContent> Text =
        Parse.CharExcept('<').AtLeastOnce().Text().Select(XmlContent.FromText);

    private static readonly Parser<XmlContent> Content =
        Parse.Ref(() => Element.Select(XmlContent.FromElement).Or(Text));

    private static readonly Parser<XmlElement> Element =
        from open in Parse.Char('<')
        from name in Name
        from attributes in Attribute.Many()
        from beforeClose in Parse.WhiteSpace.Many()
        from close in Parse.Char('>')
        from children in Content.Many()
        from end in EndTag(name)
        select new XmlElement(name, attributes.ToArray(), children.ToArray());

    public static XmlElement ParseDocument(string text)
    {
        return Element.End().Parse(text.Trim());
    }

    private static Parser<string> EndTag(string expectedName)
    {
        return
            from open in Parse.String("</")
            from actualName in Name
            from beforeClose in Parse.WhiteSpace.Many()
            from close in Parse.Char('>')
            where string.Equals(actualName, expectedName, StringComparison.Ordinal)
            select actualName;
    }

    private static bool IsNameChar(char value)
    {
        return char.IsLetterOrDigit(value)
            || value == '_'
            || value == ':'
            || value == '-';
    }
}

public sealed record XmlElement(
    string Name,
    IReadOnlyList<XmlAttributeNode> Attributes,
    IReadOnlyList<XmlContent> Children);

public sealed record XmlAttributeNode(string Name, string Value);

public sealed record XmlContent(XmlElement? Element, string? Text)
{
    public static XmlContent FromElement(XmlElement element)
    {
        return new XmlContent(element, null);
    }

    public static XmlContent FromText(string text)
    {
        return new XmlContent(null, text);
    }
}
