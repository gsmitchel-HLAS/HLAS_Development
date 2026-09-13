namespace HLAS.Domain
{
    public readonly record struct LifecycleState
    {
        public string Value { get; }

        private LifecycleState(string value)
        {
            Value = value;
        }

        public static LifecycleState Active { get; } = new("Active");
        public static LifecycleState Superseded { get; } = new("Superseded");
        public static LifecycleState Closed { get; } = new("Closed");
    }
}