namespace HLAS.Domain
{
    public readonly record struct ProjectIntegrityCheckStatus
    {
        public string Value { get; }

        private ProjectIntegrityCheckStatus(string value)
        {
            Value = value;
        }

        public static ProjectIntegrityCheckStatus Verified { get; } =
            new("VERIFIED");

        public static ProjectIntegrityCheckStatus Pending { get; } =
            new("PENDING");

        public static ProjectIntegrityCheckStatus IntegrityFailure { get; } =
            new("INTEGRITY_FAILURE");

        public static ProjectIntegrityCheckStatus NotChecked { get; } =
            new("NOT_CHECKED");
    }
}