using System;
using System.Collections.Generic;
using HLAS.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class ProjectIntegrityCheckReportTests
    {
        [TestMethod]
        public void StatusValues_PreserveExactOutcomes()
        {
            Assert.AreEqual("VERIFIED",
                ProjectIntegrityCheckStatus.Verified.Value);
            Assert.AreEqual("PENDING",
                ProjectIntegrityCheckStatus.Pending.Value);
            Assert.AreEqual("INTEGRITY_FAILURE",
                ProjectIntegrityCheckStatus.IntegrityFailure.Value);
            Assert.AreEqual("NOT_CHECKED",
                ProjectIntegrityCheckStatus.NotChecked.Value);
        }

        [TestMethod]
        public void Item_InvalidValues_AreRejected()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new ProjectIntegrityCheckItem(
                    "",
                    ProjectIntegrityCheckStatus.Verified,
                    "Valid detail"));

            Assert.ThrowsExactly<ArgumentException>(
                () => new ProjectIntegrityCheckItem(
                    "CUSTODY",
                    default,
                    "Valid detail"));

            Assert.ThrowsExactly<ArgumentException>(
                () => new ProjectIntegrityCheckItem(
                    "CUSTODY",
                    ProjectIntegrityCheckStatus.Verified,
                    ""));
        }

        [TestMethod]
        public void Report_ValidCoverage_PreservesSnapshot()
        {
            ProjectId projectId = ProjectId.CreateNew();

            List<ProjectIntegrityCheckItem> items = new()
            {
                new("CUSTODY",
                    ProjectIntegrityCheckStatus.Verified,
                    "Custody verified"),

                new("REVIEW",
                    ProjectIntegrityCheckStatus.Pending,
                    "Review pending"),

                new("HISTORY",
                    ProjectIntegrityCheckStatus.NotChecked,
                    "History not checked")
            };

            ProjectIntegrityCheckReport report =
                new(projectId, items);

            items.Clear();

            Assert.AreEqual(projectId, report.ProjectId);
            Assert.HasCount(3, report.Items);
            Assert.AreEqual("CUSTODY",
                report.Items[0].CheckCode);
            Assert.AreEqual(
                ProjectIntegrityCheckStatus.Pending,
                report.Items[1].Status);
            Assert.AreEqual(
                ProjectIntegrityCheckStatus.NotChecked,
                report.Items[2].Status);
        }

        [TestMethod]
        public void Report_EmptyCoverage_IsRejected()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new ProjectIntegrityCheckReport(
                    ProjectId.CreateNew(),
                    Array.Empty<ProjectIntegrityCheckItem>()));
        }

        [TestMethod]
        public void Report_EmptyProjectId_IsRejected()
        {
            ProjectIntegrityCheckItem[] items =
            {
                new("CUSTODY",
                    ProjectIntegrityCheckStatus.Verified,
                    "Verified")
            };

            Assert.ThrowsExactly<ArgumentException>(
                () => new ProjectIntegrityCheckReport(
                    new ProjectId(Guid.Empty),
                    items));
        }
    }
}