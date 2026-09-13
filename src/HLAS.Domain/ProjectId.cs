using System;

namespace HLAS.Domain
{
    public readonly record struct ProjectId(Guid Value)
    {
        public static ProjectId CreateNew()
        {
            return new ProjectId(Guid.NewGuid());
        }
    }
}