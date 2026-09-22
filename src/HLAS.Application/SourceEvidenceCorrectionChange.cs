namespace HLAS.Application
{
    public sealed record SourceEvidenceCorrectionChange(
        string? DisplayLabel,
        string? AdministrativeDescription,
        string CorrectionReason);
}