using System;

namespace HLAS.Domain
{
    public sealed record ProjectJmfRevisionFieldRecord
    {
        public ProjectJmfRevisionId ProjectJmfRevisionId { get; }
        public string FieldId { get; }
        public string FieldValue { get; }
        public EvidenceId SourceEvidenceId { get; }

        public ProjectJmfRevisionFieldRecord(
            ProjectJmfRevisionId projectJmfRevisionId,
            string fieldId,
            string fieldValue,
            EvidenceId sourceEvidenceId)
        {
            if (projectJmfRevisionId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "ProjectJmfRevisionId may not be empty.",
                    nameof(projectJmfRevisionId));
            }

            if (string.IsNullOrWhiteSpace(fieldId))
            {
                throw new ArgumentException(
                    "FieldId may not be blank.",
                    nameof(fieldId));
            }

            ArgumentNullException.ThrowIfNull(fieldValue);

            if (sourceEvidenceId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "SourceEvidenceId may not be empty.",
                    nameof(sourceEvidenceId));
            }

            ProjectJmfRevisionId = projectJmfRevisionId;
            FieldId = fieldId;
            FieldValue = fieldValue;
            SourceEvidenceId = sourceEvidenceId;
        }
    }
}