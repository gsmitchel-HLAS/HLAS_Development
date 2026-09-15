using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class EquivalenceCoverageItemTests
    {
        [TestMethod]
        public void CoverageItem_PreservesDevelopmentalEvidenceStatus()
        {
            EquivalenceCoverageItem item = new(
                "COV-001",
                "EQ-001",
                "Approved equivalence requirement",
                DevelopmentalEvidenceStatus.Recorded);

            Assert.AreEqual(
                "COV-001",
                item.CoverageItemId);

            Assert.AreEqual(
                "EQ-001",
                item.TestId);

            Assert.AreEqual(
                "Approved equivalence requirement",
                item.RequirementReference);

            Assert.AreEqual(
                DevelopmentalEvidenceStatus.Recorded,
                item.EvidenceStatus);
        }
    }
}