namespace HLAS.Tests
{
    public enum DevelopmentalEvidenceStatus
    {
        NotRecorded,
        Recorded
    }

    public sealed record EquivalenceCoverageItem
    {
        public string CoverageItemId { get; }
        public string TestId { get; }
        public string RequirementReference { get; }
        public DevelopmentalEvidenceStatus EvidenceStatus { get; }

        public EquivalenceCoverageItem(
            string coverageItemId,
            string testId,
            string requirementReference,
            DevelopmentalEvidenceStatus evidenceStatus)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(coverageItemId);
            ArgumentException.ThrowIfNullOrWhiteSpace(testId);
            ArgumentException.ThrowIfNullOrWhiteSpace(requirementReference);

            CoverageItemId = coverageItemId;
            TestId = testId;
            RequirementReference = requirementReference;
            EvidenceStatus = evidenceStatus;
        }
    }
}