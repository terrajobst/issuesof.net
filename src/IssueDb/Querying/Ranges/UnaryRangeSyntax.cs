namespace IssueDb.Querying.Ranges;

public sealed class UnaryRangeSyntax<T> : RangeSyntax<T>
    where T : IComparable<T>
{
    public UnaryRangeSyntax(UnaryRangeOperator op, T operand)
    {
        Op = op;
        Operand = operand;
    }

    public UnaryRangeOperator Op { get; }
    public T Operand { get; }

    public override bool Contains(T value)
    {
        var c = value.CompareTo(Operand);
        return Op switch
        {
            UnaryRangeOperator.EqualTo => c == 0,
            UnaryRangeOperator.LessThan => c < 0,
            UnaryRangeOperator.LessThanOrEqual => c <= 0,
            UnaryRangeOperator.GreaterThan => c > 0,
            UnaryRangeOperator.GreaterThanOrEqual => c >= 0,
            _ => throw new Exception($"Unexpected operator {Op}")
        };
    }

    public override string ToString()
    {
        return Op switch
        {
            UnaryRangeOperator.EqualTo => $"{Operand}",
            UnaryRangeOperator.LessThan => $"<{Operand}",
            UnaryRangeOperator.LessThanOrEqual => $"<={Operand}",
            UnaryRangeOperator.GreaterThan => $">{Operand}",
            UnaryRangeOperator.GreaterThanOrEqual => $">={Operand}",
            _ => throw new Exception($"Unexpected operator {Op}")
        };
    }
}
