using System;

namespace HLAS.Domain
{
    public readonly record struct UserId(Guid Value)
    {
        public static UserId CreateNew()
        {
            return new UserId(Guid.NewGuid());
        }
    }
}