using System;

namespace IssueDb.Querying.Ranges
{
    public sealed class BinaryRangeSyntax<T> : RangeSyntax<T>
        where T : IComparable<T>
    {
        public BinaryRangeSyntax(T lowerBound, T upperBound)
        {
            LowerBound = lowerBound;
            UpperBound = upperBound;
        }

        public T LowerBound { get; }
        public T UpperBound { get; }

        public override bool Contains(T value)
        {
            var c1 = LowerBound.CompareTo(value);
            var c2 = value.CompareTo(UpperBound);
            return c1 <= 0 && c2 <= 0;
        }

        public override string ToString()
        {
            return $"{LowerBound}..{UpperBound}";
        }
    }
}
