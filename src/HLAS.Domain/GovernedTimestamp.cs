using System;

namespace HLAS.Domain
{
    public readonly record struct GovernedTimestamp
    {
        public DateTimeOffset Value { get; }

        private GovernedTimestamp(DateTimeOffset value)
        {
            Value = value.ToUniversalTime();
        }

        public static GovernedTimestamp CreateNow()
        {
            return new GovernedTimestamp(DateTimeOffset.UtcNow);
        }
    }
}