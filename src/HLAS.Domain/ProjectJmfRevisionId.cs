using System;

namespace HLAS.Domain
{
    public readonly record struct ProjectJmfRevisionId(Guid Value)
    {
        public static ProjectJmfRevisionId CreateNew()
        {
            return new ProjectJmfRevisionId(Guid.NewGuid());
        }
    }
}