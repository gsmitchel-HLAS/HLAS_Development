using System;

namespace HLAS.Domain
{
    public readonly record struct InternalSeriesTransitionId(Guid Value)
    {
        public static InternalSeriesTransitionId CreateNew()
        {
            return new InternalSeriesTransitionId(Guid.NewGuid());
        }
    }
}