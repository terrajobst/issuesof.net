namespace IssuesOfDotNet.Querying
{
    public sealed class BoundOrExpression : BoundExpression
    {
        public BoundOrExpression(BoundExpression left, BoundExpression right)
        {
            Left = left;
            Right = right;
        }

        public BoundExpression Left { get; }
        public BoundExpression Right { get; }
    }
}
