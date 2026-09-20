using System;

namespace HLAS.Domain
{
    public readonly record struct FreezeId(Guid Value)
    {
        public static FreezeId CreateNew()
        {
            return new FreezeId(Guid.NewGuid());
        }
    }
}