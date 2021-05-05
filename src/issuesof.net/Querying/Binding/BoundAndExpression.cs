namespace IssuesOfDotNet.Querying
{
    public sealed class BoundAndExpression : BoundExpression
    {
        public BoundAndExpression(BoundExpression left, BoundExpression right)
        {
            Left = left;
            Right = right;
        }

        public BoundExpression Left { get; }
        public BoundExpression Right { get; }
    }
}
