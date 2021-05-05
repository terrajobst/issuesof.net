namespace IssuesOfDotNet.Querying
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
