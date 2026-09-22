namespace HLAS.Application
{
    public sealed record SourceEvidenceCorrectionChange(
        string FieldName,
        string? PriorValue,
        string? ResultingValue);
}