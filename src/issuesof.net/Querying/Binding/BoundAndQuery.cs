namespace IssuesOfDotNet.Querying
{
    public sealed class BoundAndQuery : BoundQuery
    {
        public BoundAndQuery(BoundQuery left, BoundQuery right)
        {
            Left = left;
            Right = right;
        }

        public BoundQuery Left { get; }
        public BoundQuery Right { get; }
    }
}
