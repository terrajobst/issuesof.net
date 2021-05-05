namespace IssuesOfDotNet.Querying
{
    public sealed class BoundNegatedExpression : BoundExpression
    {
        public BoundNegatedExpression(BoundExpression matcher)
        {
            Expression = matcher;
        }

        public BoundExpression Expression { get; }
    }
}
