using System;

namespace HLAS.Domain
{
    public readonly record struct ReadinessCheckId(Guid Value)
    {
        public static ReadinessCheckId CreateNew()
        {
            return new ReadinessCheckId(Guid.NewGuid());
        }
    }
}