namespace IssuesOfDotNet.Querying
{
    public sealed class BoundTextExpression : BoundExpression
    {
        public BoundTextExpression(bool isNegated, string text)
        {
            IsNegated = isNegated;
            Text = text;
        }

        public bool IsNegated { get; }
        public string Text { get; }
    }
}
