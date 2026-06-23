#r "nuget: Sprache, 2.3.1"
#r "nuget: devo6.SSC, 0.3.1-pre"
#load "xml-like-parser.csx"

#nullable enable

using SSC;

const string leftXml = """
<1root 2id="left" stable="same">
  <2item 3code="A">left text</2item>
  <group>
    <4leaf 5attr="left">10</4leaf>
  </group>
</1root>
""";

const string rightXml = """
<1root 2id="right" stable="same">
  <2item 3code="B">right text</2item>
  <group>
    <4leaf 5attr="right">20</4leaf>
  </group>
</1root>
""";

XmlElement leftParsed = XmlLikeParser.ParseDocument(leftXml);
XmlElement rightParsed = XmlLikeParser.ParseDocument(rightXml);

Require(leftParsed.Name == "1root", "element names may start with a digit");
Require(leftParsed.Attributes.Any(attribute => attribute.Name == "2id"), "attribute names may start with a digit");

XmlDocumentModel[] models =
[
    new XmlDocumentModel { Root = XmlModelBuilder.ToModel(leftParsed) },
    new XmlDocumentModel { Root = XmlModelBuilder.ToModel(rightParsed) },
];

CompareResult<XmlDocumentModel> result = ParallelCompareApi.Compare(models);
IReadOnlyList<ParallelDiffEntry> diffs = result.GetDiffEntries();

Require(diffs.Any(entry => entry.Path == "Root.Attributes[2id].Value"), "numeric attribute diff is present");
Require(diffs.Any(entry => entry.Path == "Root.Children[1root/2item#0].Text"), "numeric element text diff is present");

foreach (ParallelDiffEntry diff in diffs)
{
    Console.WriteLine(diff);
}

public static class XmlModelBuilder
{
    public static XmlElementModel ToModel(XmlElement element)
    {
        return ToModel(element, element.Name, ordinal: 0);
    }

    private static XmlElementModel ToModel(XmlElement element, string parentPath, int ordinal)
    {
        string path = ordinal == 0 && string.Equals(parentPath, element.Name, StringComparison.Ordinal)
            ? element.Name
            : $"{parentPath}/{element.Name}#{ordinal}";

        var text = string.Concat(element.Children
            .Where(child => child.Text is not null)
            .Select(child => child.Text!.Trim())
            .Where(textPart => textPart.Length > 0));

        var elementOrdinalByName = new Dictionary<string, int>(StringComparer.Ordinal);
        var children = new List<XmlElementModel>();
        foreach (XmlElement childElement in element.Children
            .Where(child => child.Element is not null)
            .Select(child => child.Element!))
        {
            elementOrdinalByName.TryGetValue(childElement.Name, out var childOrdinal);
            elementOrdinalByName[childElement.Name] = childOrdinal + 1;
            children.Add(ToModel(childElement, path, childOrdinal));
        }

        return new XmlElementModel
        {
            Path = path,
            Name = element.Name,
            Text = text,
            Attributes = element.Attributes
                .OrderBy(attribute => attribute.Name, StringComparer.Ordinal)
                .Select(attribute => new XmlAttributeModel
                {
                    Name = attribute.Name,
                    Value = attribute.Value,
                })
                .ToList(),
            Children = children,
        };
    }
}

public sealed class XmlDocumentModel
{
    public XmlElementModel Root { get; init; } = new();
}

public sealed class XmlElementModel
{
    [CompareKey]
    public string Path { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public List<XmlAttributeModel> Attributes { get; init; } = [];

    public List<XmlElementModel> Children { get; init; } = [];
}

public sealed class XmlAttributeModel
{
    [CompareKey]
    public string Name { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
