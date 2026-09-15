namespace HLAS.Tests
{
    public sealed record EquivalenceCoverageSummary(
        int TotalItems,
        int EvidenceRecorded,
        int EvidenceNotRecorded);

    public sealed class EquivalenceHarness
    {
        private readonly List<EquivalenceTestDefinition> _definitions = new();
        private readonly List<EquivalenceTestAttempt> _attempts = new();
        private readonly List<EquivalenceCoverageItem> _coverageItems = new();

        public IReadOnlyList<EquivalenceTestDefinition> Definitions =>
            _definitions;

        public IReadOnlyList<EquivalenceTestAttempt> Attempts =>
            _attempts;

        public IReadOnlyList<EquivalenceCoverageItem> CoverageItems =>
            _coverageItems;

        public void RegisterTest(
            EquivalenceTestDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);

            if (_definitions.Any(
                existing => existing.TestId == definition.TestId))
            {
                throw new InvalidOperationException(
                    $"Equivalence test '{definition.TestId}' is already registered.");
            }

            _definitions.Add(definition);
        }

        public void RecordAttempt(
            EquivalenceTestAttempt attempt)
        {
            ArgumentNullException.ThrowIfNull(attempt);

            if (!_definitions.Any(
                definition => definition.TestId == attempt.TestId))
            {
                throw new InvalidOperationException(
                    $"Equivalence test '{attempt.TestId}' is not registered.");
            }

            if (_attempts.Any(
                existing =>
                    existing.TestId == attempt.TestId &&
                    existing.AttemptNumber == attempt.AttemptNumber))
            {
                throw new InvalidOperationException(
                    $"Attempt {attempt.AttemptNumber} for equivalence test '{attempt.TestId}' is already recorded.");
            }

            _attempts.Add(attempt);
        }

        public void RegisterCoverageItem(
            EquivalenceCoverageItem item)
        {
            ArgumentNullException.ThrowIfNull(item);

            if (!_definitions.Any(
                definition => definition.TestId == item.TestId))
            {
                throw new InvalidOperationException(
                    $"Equivalence test '{item.TestId}' is not registered.");
            }

            if (_coverageItems.Any(
                existing => existing.CoverageItemId == item.CoverageItemId))
            {
                throw new InvalidOperationException(
                    $"Coverage item '{item.CoverageItemId}' is already registered.");
            }

            _coverageItems.Add(item);
        }

        public IReadOnlyList<EquivalenceTestAttempt> GetAttempts(
            string testId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(testId);

            return _attempts
                .Where(attempt => attempt.TestId == testId)
                .OrderBy(attempt => attempt.AttemptNumber)
                .ToList();
        }

        public EquivalenceCoverageSummary GetCoverageSummary()
        {
            int recorded = _coverageItems.Count(
                item =>
                    item.EvidenceStatus ==
                    DevelopmentalEvidenceStatus.Recorded);

            int notRecorded = _coverageItems.Count(
                item =>
                    item.EvidenceStatus ==
                    DevelopmentalEvidenceStatus.NotRecorded);

            return new EquivalenceCoverageSummary(
                _coverageItems.Count,
                recorded,
                notRecorded);
        }
    }
}
