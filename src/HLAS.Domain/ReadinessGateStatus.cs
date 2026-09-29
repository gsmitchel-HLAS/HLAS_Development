namespace HLAS.Domain
{
    public readonly record struct ReadinessGateStatus
    {
        public string Value { get; }

        private ReadinessGateStatus(string value)
        {
            Value = value;
        }

        public static ReadinessGateStatus Pass { get; } = new("PASS");
        public static ReadinessGateStatus Blocked { get; } = new("BLOCKED");
    }
}