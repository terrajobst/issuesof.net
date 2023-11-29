namespace IssueDb.Querying.Binding;

public sealed class BoundTextQuery : BoundQuery
{
    public BoundTextQuery(bool isNegated, string text)
    {
        IsNegated = isNegated;
        Text = text;
    }

    public bool IsNegated { get; }
    public string Text { get; }
}
