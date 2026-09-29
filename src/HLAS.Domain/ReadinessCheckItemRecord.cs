using System;

namespace HLAS.Domain
{
    public sealed record ReadinessCheckItemRecord
    {
        public ReadinessCheckId ReadinessCheckId { get; }
        public int GateSequence { get; }
        public ReadinessGateCode GateCode { get; }
        public ReadinessGateStatus GateStatus { get; }
        public string? Detail { get; }

        public ReadinessCheckItemRecord(
            ReadinessCheckId readinessCheckId,
            int gateSequence,
            ReadinessGateCode gateCode,
            ReadinessGateStatus gateStatus,
            string? detail)
        {
            if (readinessCheckId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "ReadinessCheckId may not be empty.",
                    nameof(readinessCheckId));
            }

            if (gateSequence < 1 || gateSequence > 7)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(gateSequence),
                    "GateSequence must be between 1 and 7.");
            }

            if (string.IsNullOrWhiteSpace(gateCode.Value))
            {
                throw new ArgumentException(
                    "GateCode may not be blank.",
                    nameof(gateCode));
            }

            if (string.IsNullOrWhiteSpace(gateStatus.Value))
            {
                throw new ArgumentException(
                    "GateStatus may not be blank.",
                    nameof(gateStatus));
            }

            ReadinessCheckId = readinessCheckId;
            GateSequence = gateSequence;
            GateCode = gateCode;
            GateStatus = gateStatus;
            Detail = detail;
        }
    }
}