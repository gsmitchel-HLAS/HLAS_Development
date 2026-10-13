using System;
using HLAS.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class InternalSeriesTransitionRecordTests
    {
        [TestMethod]
        public void Constructor_ValidTransition_PreservesReleasedBasis()
        {
            InternalSeriesTransitionId transitionId =
                InternalSeriesTransitionId.CreateNew();
            ProjectId projectId = ProjectId.CreateNew();
            OperationId operationId = OperationId.CreateNew();
            ReleaseId releaseId = ReleaseId.CreateNew();
            FreezeId freezeId = FreezeId.CreateNew();
            ReadinessCheckId readinessId =
                ReadinessCheckId.CreateNew();
            GovernedTimestamp receivedUtc =
                GovernedTimestamp.CreateNow();

            InternalSeriesTransitionRecord record = new(
                transitionId,
                projectId,
                operationId,
                SeriesId.A,
                releaseId,
                freezeId,
                readinessId,
                receivedUtc);

            Assert.AreEqual(transitionId, record.TransitionId);
            Assert.AreEqual(projectId, record.ProjectId);
            Assert.AreEqual(operationId, record.OperationId);
            Assert.AreEqual(SeriesId.V, record.SourceSeriesId);
            Assert.AreEqual(SeriesId.A, record.ReceivingSeriesId);
            Assert.AreEqual(releaseId, record.ReleaseId);
            Assert.AreEqual(freezeId, record.ReleaseFreezeId);
            Assert.AreEqual(readinessId, record.ReadinessCheckId);
            Assert.AreEqual(receivedUtc, record.ReceivedUtc);
        }

        [TestMethod]
        public void Constructor_ReceivingVSeries_IsRejected()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new InternalSeriesTransitionRecord(
                    InternalSeriesTransitionId.CreateNew(),
                    ProjectId.CreateNew(),
                    OperationId.CreateNew(),
                    SeriesId.V,
                    ReleaseId.CreateNew(),
                    FreezeId.CreateNew(),
                    ReadinessCheckId.CreateNew(),
                    GovernedTimestamp.CreateNow()));
        }

        [TestMethod]
        public void Constructor_EmptyReleaseId_IsRejected()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new InternalSeriesTransitionRecord(
                    InternalSeriesTransitionId.CreateNew(),
                    ProjectId.CreateNew(),
                    OperationId.CreateNew(),
                    SeriesId.A,
                    new ReleaseId(Guid.Empty),
                    FreezeId.CreateNew(),
                    ReadinessCheckId.CreateNew(),
                    GovernedTimestamp.CreateNow()));
        }
    }
}