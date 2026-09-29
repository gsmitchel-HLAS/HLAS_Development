using System.Collections.Generic;
using HLAS.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class ReadinessCheckResultTests
    {
        [TestMethod]
        public void Constructor_OutOfOrderGovernedGate_SafeStops()
        {
            ReadinessCheckId checkId =
                ReadinessCheckId.CreateNew();

            ReadinessCheckRecord check =
                new(
                    checkId,
                    OperationId.CreateNew(),
                    ReadinessStatus.Ready,
                    GovernedTimestamp.CreateNow());

            List<ReadinessCheckItemRecord> items =
                new()
                {
                    new(checkId, 1, ReadinessGateCode.SourceEvidence, ReadinessGateStatus.Pass, null),
                    new(checkId, 2, ReadinessGateCode.ProjectIdentity, ReadinessGateStatus.Pass, null),
                    new(checkId, 3, ReadinessGateCode.ProjectJmfTruth, ReadinessGateStatus.Pass, null),
                    new(checkId, 4, ReadinessGateCode.Maintenance, ReadinessGateStatus.Pass, null),
                    new(checkId, 5, ReadinessGateCode.ProjectJmfReview, ReadinessGateStatus.Pass, null),
                    new(checkId, 6, ReadinessGateCode.Lineage, ReadinessGateStatus.Pass, null),
                    new(checkId, 7, ReadinessGateCode.FreezeCapability, ReadinessGateStatus.Pass, null)
                };

            Assert.ThrowsExactly<ArgumentException>(
                () => new ReadinessCheckResult(
                    check,
                    items));
        }
        [TestMethod]
        public void Constructor_ReadyWithBlockedGate_SafeStops()
        {
            ReadinessCheckId checkId =
                ReadinessCheckId.CreateNew();

            ReadinessCheckRecord check =
                new(
                    checkId,
                    OperationId.CreateNew(),
                    ReadinessStatus.Ready,
                    GovernedTimestamp.CreateNow());

            List<ReadinessCheckItemRecord> items =
                new()
                {
            new(checkId, 1, ReadinessGateCode.ProjectIdentity, ReadinessGateStatus.Pass, null),
            new(checkId, 2, ReadinessGateCode.SourceEvidence, ReadinessGateStatus.Pass, null),
            new(checkId, 3, ReadinessGateCode.ProjectJmfTruth, ReadinessGateStatus.Pass, null),
            new(checkId, 4, ReadinessGateCode.Maintenance, ReadinessGateStatus.Blocked, null),
            new(checkId, 5, ReadinessGateCode.ProjectJmfReview, ReadinessGateStatus.Pass, null),
            new(checkId, 6, ReadinessGateCode.Lineage, ReadinessGateStatus.Pass, null),
            new(checkId, 7, ReadinessGateCode.FreezeCapability, ReadinessGateStatus.Pass, null)
                };

            Assert.ThrowsExactly<ArgumentException>(
                () => new ReadinessCheckResult(
                    check,
                    items));
        }
        [TestMethod]
        public void Constructor_NotReadyWithAllPassingGates_SafeStops()
        {
            ReadinessCheckId checkId =
                ReadinessCheckId.CreateNew();

            ReadinessCheckRecord check =
                new(
                    checkId,
                    OperationId.CreateNew(),
                    ReadinessStatus.NotReady,
                    GovernedTimestamp.CreateNow());

            List<ReadinessCheckItemRecord> items =
                new()
                {
            new(checkId, 1, ReadinessGateCode.ProjectIdentity, ReadinessGateStatus.Pass, null),
            new(checkId, 2, ReadinessGateCode.SourceEvidence, ReadinessGateStatus.Pass, null),
            new(checkId, 3, ReadinessGateCode.ProjectJmfTruth, ReadinessGateStatus.Pass, null),
            new(checkId, 4, ReadinessGateCode.Maintenance, ReadinessGateStatus.Pass, null),
            new(checkId, 5, ReadinessGateCode.ProjectJmfReview, ReadinessGateStatus.Pass, null),
            new(checkId, 6, ReadinessGateCode.Lineage, ReadinessGateStatus.Pass, null),
            new(checkId, 7, ReadinessGateCode.FreezeCapability, ReadinessGateStatus.Pass, null)
                };

            Assert.ThrowsExactly<ArgumentException>(
                () => new ReadinessCheckResult(
                    check,
                    items));
        }
        [TestMethod]
        public void Constructor_AllPassingGates_AcceptsReadyResult()
        {
            ReadinessCheckId checkId =
                ReadinessCheckId.CreateNew();

            ReadinessCheckRecord check =
                new(
                    checkId,
                    OperationId.CreateNew(),
                    ReadinessStatus.Ready,
                    GovernedTimestamp.CreateNow());

            List<ReadinessCheckItemRecord> items =
                new()
                {
            new(checkId, 1, ReadinessGateCode.ProjectIdentity, ReadinessGateStatus.Pass, null),
            new(checkId, 2, ReadinessGateCode.SourceEvidence, ReadinessGateStatus.Pass, null),
            new(checkId, 3, ReadinessGateCode.ProjectJmfTruth, ReadinessGateStatus.Pass, null),
            new(checkId, 4, ReadinessGateCode.Maintenance, ReadinessGateStatus.Pass, null),
            new(checkId, 5, ReadinessGateCode.ProjectJmfReview, ReadinessGateStatus.Pass, null),
            new(checkId, 6, ReadinessGateCode.Lineage, ReadinessGateStatus.Pass, null),
            new(checkId, 7, ReadinessGateCode.FreezeCapability, ReadinessGateStatus.Pass, null)
                };

            ReadinessCheckResult result =
                new(
                    check,
                    items);

            Assert.AreEqual(
                ReadinessStatus.Ready,
                result.Check.OverallStatus);

            Assert.HasCount(
     7,
     result.Items);
        }

    }
}