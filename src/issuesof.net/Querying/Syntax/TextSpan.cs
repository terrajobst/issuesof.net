namespace IssuesOfDotNet.Querying
{
    public readonly struct TextSpan
    {
        public TextSpan(int start, int length)
        {
            Start = start;
            Length = length;
        }

        public static TextSpan FromBounds(int start, int end)
        {
            var length = end - start;
            return new TextSpan(start, length);
        }

        public int Start { get; }
        public int End => Start + Length;
        public int Length { get; }

        public override string ToString() => $"[{Start},{End})";
    }
}
