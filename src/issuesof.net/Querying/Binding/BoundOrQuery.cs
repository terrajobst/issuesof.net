namespace IssuesOfDotNet.Querying
{
    public sealed class BoundOrQuery : BoundQuery
    {
        public BoundOrQuery(BoundQuery left, BoundQuery right)
        {
            Left = left;
            Right = right;
        }

        public BoundQuery Left { get; }
        public BoundQuery Right { get; }
    }
}
