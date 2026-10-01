using HLAS.Domain;

public sealed record ProjectJmfReviewItemRecord
{
    public ProjectJmfReviewCaseId ReviewCaseId { get; }
    public string FieldId { get; }
    public string CurrentApprovedValue { get; }
    public string NewOfficialSourceValue { get; }
    public ProjectJmfReviewDecision? Decision { get; }
    public string? DecisionReason { get; }
    public OperationId? DecidedOperationId { get; }
    public GovernedTimestamp? DecidedUtc { get; }

    public ProjectJmfReviewItemRecord(
        ProjectJmfReviewCaseId reviewCaseId,
        string fieldId,
        string currentApprovedValue,
        string newOfficialSourceValue,
        ProjectJmfReviewDecision? decision,
        string? decisionReason,
        OperationId? decidedOperationId,
        GovernedTimestamp? decidedUtc)
    {
        if (reviewCaseId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "ReviewCaseId may not be empty.",
                nameof(reviewCaseId));
        }

        if (string.IsNullOrWhiteSpace(fieldId))
        {
            throw new ArgumentException(
                "FieldId may not be blank.",
                nameof(fieldId));
        }

        ArgumentNullException.ThrowIfNull(currentApprovedValue);
        ArgumentNullException.ThrowIfNull(newOfficialSourceValue);

        if (decision.HasValue)
        {
            if (string.IsNullOrWhiteSpace(decision.Value.Value))
            {
                throw new ArgumentException(
                    "Decision may not be blank.",
                    nameof(decision));
            }

            if (string.IsNullOrWhiteSpace(decisionReason))
            {
                throw new ArgumentException(
                    "DecisionReason is required when a review item is decided.",
                    nameof(decisionReason));
            }

            if (!decidedOperationId.HasValue ||
                decidedOperationId.Value.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "DecidedOperationId is required when a review item is decided.",
                    nameof(decidedOperationId));
            }

            if (!decidedUtc.HasValue ||
                decidedUtc.Value.Value == default)
            {
                throw new ArgumentException(
                    "DecidedUtc is required when a review item is decided.",
                    nameof(decidedUtc));
            }
        }
        else if (
            decisionReason is not null ||
            decidedOperationId.HasValue ||
            decidedUtc.HasValue)
        {
            throw new ArgumentException(
                "Undecided review items may not contain decision details.");
        }

        ReviewCaseId = reviewCaseId;
        FieldId = fieldId;
        CurrentApprovedValue = currentApprovedValue;
        NewOfficialSourceValue = newOfficialSourceValue;
        Decision = decision;
        DecisionReason = decisionReason;
        DecidedOperationId = decidedOperationId;
        DecidedUtc = decidedUtc;
    }
}
