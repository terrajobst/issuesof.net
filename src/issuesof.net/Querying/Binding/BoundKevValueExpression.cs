namespace IssuesOfDotNet.Querying
{
    public sealed class BoundKevValueExpression : BoundExpression
    {
        public BoundKevValueExpression(bool isNegated, string key, string value)
        {
            IsNegated = isNegated;
            Key = key;
            Value = value;
        }

        public bool IsNegated { get; }
        public string Key { get; }
        public string Value { get; }
    }
}
