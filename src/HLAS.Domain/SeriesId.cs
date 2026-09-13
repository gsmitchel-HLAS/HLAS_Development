namespace HLAS.Domain
{
    public readonly record struct SeriesId
    {
        public string Value { get; }

        private SeriesId(string value)
        {
            Value = value;
        }

        public static SeriesId V { get; } = new("V");
        public static SeriesId A { get; } = new("A");
        public static SeriesId I { get; } = new("I");
    }
}