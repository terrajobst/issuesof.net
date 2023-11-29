using System;

namespace IssueDb.Querying.Ranges;

public abstract class RangeSyntax<T>
    where T: IComparable<T>
{
    public delegate bool ScalarParser(string text, out T result);

    public static RangeSyntax<T> Parse(string text, ScalarParser scalarParser)
    {
        if (text is null)
            return null;

        if (scalarParser is null)
            throw new ArgumentNullException(nameof(scalarParser));

        if (scalarParser(text, out var simpleOperand))
            return new UnaryRangeSyntax<T>(UnaryRangeOperator.EqualTo, simpleOperand);

        var ops = new[] { "<", "<=", ">", ">=" };

        for (var i = 0; i < ops.Length; i++)
        {
            var opText = ops[i];

            if (text.StartsWith(opText, StringComparison.Ordinal))
            {
                var op = (UnaryRangeOperator)(i + 1);
                var operandText = text.Substring(opText.Length).Trim();
                if (scalarParser(operandText, out var operand))
                    return new UnaryRangeSyntax<T>(op, operand);
            }
        }

        var indexOfDotDot = text.IndexOf("..", StringComparison.Ordinal);
        if (indexOfDotDot > 0)
        {
            var leftText = text.Substring(0, indexOfDotDot).Trim();
            var rightText = text.Substring(indexOfDotDot + 2).Trim();
            if (scalarParser(leftText, out var left) && scalarParser(rightText, out var right))
                return new BinaryRangeSyntax<T>(left, right);
        }

        return null;
    }

    public abstract bool Contains(T value);

    public RangeSyntax<T> Negate(bool negate = true)
    {
        if (negate)
            return new NegatedRangeSyntax<T>(this);

        return this;
    }
}
