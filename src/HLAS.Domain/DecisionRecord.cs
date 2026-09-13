using System;

namespace HLAS.Domain
{
    public readonly record struct DecisionRecord
    {
        public string Decision { get; }
        public string Reason { get; }

        public DecisionRecord(string decision, string reason)
        {
            if (string.IsNullOrWhiteSpace(decision))
            {
                throw new ArgumentException(
                    "Decision may not be blank.",
                    nameof(decision));
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException(
                    "Reason may not be blank.",
                    nameof(reason));
            }

            Decision = decision;
            Reason = reason;
        }
    }
}