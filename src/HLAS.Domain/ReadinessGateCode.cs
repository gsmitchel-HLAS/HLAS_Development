namespace HLAS.Domain
{
    public readonly record struct ReadinessGateCode
    {
        public string Value { get; }

        private ReadinessGateCode(string value)
        {
            Value = value;
        }

        public static ReadinessGateCode ProjectIdentity { get; } = new("PROJECT_IDENTITY");
        public static ReadinessGateCode SourceEvidence { get; } = new("SOURCE_EVIDENCE");
        public static ReadinessGateCode ProjectJmfTruth { get; } = new("PROJECT_JMF_TRUTH");
        public static ReadinessGateCode Maintenance { get; } = new("MAINTENANCE");
        public static ReadinessGateCode ProjectJmfReview { get; } = new("PROJECT_JMF_REVIEW");
        public static ReadinessGateCode Lineage { get; } = new("LINEAGE");
        public static ReadinessGateCode FreezeCapability { get; } = new("FREEZE_CAPABILITY");
    }
}