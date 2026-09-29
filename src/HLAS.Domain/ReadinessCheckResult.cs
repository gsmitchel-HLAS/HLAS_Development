using System;
using System.Collections.Generic;

namespace HLAS.Domain
{
    public sealed record ReadinessCheckResult
    {
        public ReadinessCheckRecord Check { get; }
        public IReadOnlyList<ReadinessCheckItemRecord> Items { get; }

        public ReadinessCheckResult(
            ReadinessCheckRecord check,
            IReadOnlyList<ReadinessCheckItemRecord> items)
        {
            ArgumentNullException.ThrowIfNull(check);
            ArgumentNullException.ThrowIfNull(items);

            if (items.Count != 7)
            {
                throw new ArgumentException(
                    "A Readiness check must contain exactly seven gate results.",
                    nameof(items));
            }

            ReadinessGateCode[] expectedGateCodes =
{
    ReadinessGateCode.ProjectIdentity,
    ReadinessGateCode.SourceEvidence,
    ReadinessGateCode.ProjectJmfTruth,
    ReadinessGateCode.Maintenance,
    ReadinessGateCode.ProjectJmfReview,
    ReadinessGateCode.Lineage,
    ReadinessGateCode.FreezeCapability
};

            for (int index = 0; index < items.Count; index++)
            {
                ReadinessCheckItemRecord item =
                    items[index];

                if (item.ReadinessCheckId != check.ReadinessCheckId)
                {
                    throw new ArgumentException(
                        "Every readiness item must belong to the same ReadinessCheckId.",
                        nameof(items));
                }

                if (item.GateSequence != index + 1 ||
                    item.GateCode != expectedGateCodes[index])
                {
                    throw new ArgumentException(
                        "Readiness items must contain the seven governed gates in canonical sequence.",
                        nameof(items));
                }
            }
            bool hasBlockedGate = false;

            foreach (ReadinessCheckItemRecord item in items)
            {
                if (item.GateStatus == ReadinessGateStatus.Blocked)
                {
                    hasBlockedGate = true;
                    break;
                }
            }

            ReadinessStatus expectedOverallStatus =
                hasBlockedGate
                    ? ReadinessStatus.NotReady
                    : ReadinessStatus.Ready;

            if (check.OverallStatus != expectedOverallStatus)
            {
                throw new ArgumentException(
                    "Readiness overall status must match the seven governed gate results.",
                    nameof(check));
            }
            Check = check;
            Items = items;
        }
    }
}