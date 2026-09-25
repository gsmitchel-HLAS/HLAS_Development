namespace HLAS.Application
{
    public enum SourceEvidenceMetadataChangeAction
    {
        Keep,
        Set,
        Clear
    }

    public sealed record SourceEvidenceMetadataFieldChange
    {
        public SourceEvidenceMetadataChangeAction Action { get; }
        public string? Value { get; }

        private SourceEvidenceMetadataFieldChange(
            SourceEvidenceMetadataChangeAction action,
            string? value)
        {
            Action = action;
            Value = value;
        }

        public static SourceEvidenceMetadataFieldChange Keep()
        {
            return new SourceEvidenceMetadataFieldChange(
                SourceEvidenceMetadataChangeAction.Keep,
                null);
        }

        public static SourceEvidenceMetadataFieldChange Set(
            string value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            return new SourceEvidenceMetadataFieldChange(
                SourceEvidenceMetadataChangeAction.Set,
                value);
        }

        public static SourceEvidenceMetadataFieldChange Clear()
        {
            return new SourceEvidenceMetadataFieldChange(
                SourceEvidenceMetadataChangeAction.Clear,
                null);
        }
    }

    public sealed record SourceEvidenceCorrectionChange
    {
        public SourceEvidenceMetadataFieldChange DisplayLabel { get; }
        public SourceEvidenceMetadataFieldChange AdministrativeDescription { get; }
        public string CorrectionReason { get; }

        public SourceEvidenceCorrectionChange(
            SourceEvidenceMetadataFieldChange displayLabel,
            SourceEvidenceMetadataFieldChange administrativeDescription,
            string correctionReason)
        {
            ArgumentNullException.ThrowIfNull(displayLabel);
            ArgumentNullException.ThrowIfNull(administrativeDescription);
            ArgumentException.ThrowIfNullOrWhiteSpace(correctionReason);

            DisplayLabel = displayLabel;
            AdministrativeDescription = administrativeDescription;
            CorrectionReason = correctionReason;
        }
        public sealed record SourceEvidenceReplacementChange
        {
            public string SelectedReplacementSourceFilePath { get; }
            public SourceEvidenceMetadataFieldChange DisplayLabel { get; }
            public SourceEvidenceMetadataFieldChange AdministrativeDescription { get; }
            public string ReplacementReason { get; }

            public SourceEvidenceReplacementChange(
                string selectedReplacementSourceFilePath,
                SourceEvidenceMetadataFieldChange displayLabel,
                SourceEvidenceMetadataFieldChange administrativeDescription,
                string replacementReason)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(
                    selectedReplacementSourceFilePath);
                ArgumentNullException.ThrowIfNull(displayLabel);
                ArgumentNullException.ThrowIfNull(administrativeDescription);
                ArgumentException.ThrowIfNullOrWhiteSpace(replacementReason);

                SelectedReplacementSourceFilePath =
                    selectedReplacementSourceFilePath;
                DisplayLabel = displayLabel;
                AdministrativeDescription = administrativeDescription;
                ReplacementReason = replacementReason;
            }
        }
    }
}