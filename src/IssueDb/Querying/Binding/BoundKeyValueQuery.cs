namespace IssueDb.Querying.Binding
{
    public sealed class BoundKeyValueQuery : BoundQuery
    {
        public BoundKeyValueQuery(bool isNegated, string key, string value)
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
