using System;

namespace HLAS.Domain
{
    public readonly record struct EvidenceId(Guid Value)
    {
        public static EvidenceId CreateNew()
        {
            return new EvidenceId(Guid.NewGuid());
        }
    }
}