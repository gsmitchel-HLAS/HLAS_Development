namespace HLAS.Domain
{
    public readonly record struct ProjectJmfReviewDecision
    {
        public string Value { get; }

        private ProjectJmfReviewDecision(string value)
        {
            Value = value;
        }

        public static ProjectJmfReviewDecision KeepCurrent { get; } =
            new("KEEP CURRENT");

        public static ProjectJmfReviewDecision AcceptNew { get; } =
            new("ACCEPT NEW");
    }
}