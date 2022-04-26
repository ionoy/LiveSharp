namespace LiveSharp.Runtime.Infrastructure
{
    struct Range
    {
        public int Start;
        public int End;

        public Range(int start, int end)
        {
            Start = start;
            End = end;
        }

        public bool Intersects(Range other)
        {
            if (Start <= other.Start && other.Start <= End)
                return true;
            if (Start <= other.End && other.End <= End)
                return true;
            return false;
        }

        public override string ToString()
        {
            return $"({Start},{End})";
        }
    }
}