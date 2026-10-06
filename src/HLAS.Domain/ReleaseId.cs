using System;

namespace HLAS.Domain
{
    public readonly record struct ReleaseId(Guid Value)
    {
        public static ReleaseId CreateNew()
        {
            return new ReleaseId(Guid.NewGuid());
        }
    }
}