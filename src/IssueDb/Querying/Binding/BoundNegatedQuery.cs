namespace IssueDb.Querying.Binding
{
    public sealed class BoundNegatedQuery : BoundQuery
    {
        public BoundNegatedQuery(BoundQuery query)
        {
            Query = query;
        }

        public BoundQuery Query { get; }
    }
}
