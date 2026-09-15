namespace HLAS.Tests
{
    public enum EquivalenceClass
    {
        E,
        N
    }

    public sealed record EquivalenceTestDefinition
    {
        public string TestId { get; }
        public EquivalenceClass Class { get; }
        public string BaselineReference { get; }
        public string SpecificationReference { get; }
        public string GovernedStateAssertion { get; }

        public EquivalenceTestDefinition(
            string testId,
            EquivalenceClass @class,
            string baselineReference,
            string specificationReference,
            string governedStateAssertion)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(testId);
            ArgumentException.ThrowIfNullOrWhiteSpace(baselineReference);
            ArgumentException.ThrowIfNullOrWhiteSpace(specificationReference);
            ArgumentException.ThrowIfNullOrWhiteSpace(governedStateAssertion);

            TestId = testId;
            Class = @class;
            BaselineReference = baselineReference;
            SpecificationReference = specificationReference;
            GovernedStateAssertion = governedStateAssertion;
        }
    }
}