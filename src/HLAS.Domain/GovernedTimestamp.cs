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

        public static GovernedTimestamp FromRecorded(
            DateTimeOffset value)
        {
            if (value == default)
            {
                throw new ArgumentException(
                    "Recorded governed timestamp must be populated.",
                    nameof(value));
            }

            return new GovernedTimestamp(value);
        }
    }
}