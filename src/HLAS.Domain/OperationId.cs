using System;

namespace HLAS.Domain
{
    public readonly record struct OperationId(Guid Value)
    {
        public static OperationId CreateNew()
        {
            return new OperationId(Guid.NewGuid());
        }
    }
}