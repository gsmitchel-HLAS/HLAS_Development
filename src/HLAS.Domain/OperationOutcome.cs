namespace HLAS.Domain
{
    public readonly record struct OperationOutcome
    {
        public string Value { get; }

        private OperationOutcome(string value)
        {
            Value = value;
        }

        public static OperationOutcome Success { get; } = new("SUCCESS");
        public static OperationOutcome SafeStop { get; } = new("SAFE-STOP");
        public static OperationOutcome TechnicalFailure { get; } = new("TECHNICAL FAILURE");
    }
}