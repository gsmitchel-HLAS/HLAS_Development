namespace HLAS.Domain
{
    public readonly record struct ReadinessStatus
    {
        public string Value { get; }

        private ReadinessStatus(string value)
        {
            Value = value;
        }

        public static ReadinessStatus Ready { get; } = new("READY");
        public static ReadinessStatus NotReady { get; } = new("NOT READY");
    }
}