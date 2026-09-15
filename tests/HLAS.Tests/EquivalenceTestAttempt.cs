namespace HLAS.Tests
{
    public enum EquivalenceAttemptType
    {
        Initial,
        Retest
    }

    public enum EquivalenceOutcome
    {
        Pass,
        Fail
    }

    public sealed record EquivalenceTestAttempt
    {
        public string TestId { get; }
        public int AttemptNumber { get; }
        public EquivalenceAttemptType AttemptType { get; }
        public EquivalenceOutcome Outcome { get; }
        public string ObservedResult { get; }
        public DateTimeOffset RecordedAtUtc { get; }

        public EquivalenceTestAttempt(
            string testId,
            int attemptNumber,
            EquivalenceAttemptType attemptType,
            EquivalenceOutcome outcome,
            string observedResult,
            DateTimeOffset recordedAtUtc)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(testId);
            ArgumentException.ThrowIfNullOrWhiteSpace(observedResult);

            if (attemptNumber < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(attemptNumber),
                    attemptNumber,
                    "Attempt number must be at least 1.");
            }

            TestId = testId;
            AttemptNumber = attemptNumber;
            AttemptType = attemptType;
            Outcome = outcome;
            ObservedResult = observedResult;
            RecordedAtUtc = recordedAtUtc;
        }
    }
}