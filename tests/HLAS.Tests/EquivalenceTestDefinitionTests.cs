using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class EquivalenceTestDefinitionTests
    {
        [TestMethod]
        public void Constructor_PreservesStableIdentityAndReferences()
        {
            EquivalenceTestDefinition definition = new(
                "EQ-001",
                EquivalenceClass.E,
                "Excel governed baseline",
                "Approved specification reference",
                "Governed state must match the approved baseline.");

            Assert.AreEqual(
                "EQ-001",
                definition.TestId);

            Assert.AreEqual(
                EquivalenceClass.E,
                definition.Class);

            Assert.AreEqual(
                "Excel governed baseline",
                definition.BaselineReference);

            Assert.AreEqual(
                "Approved specification reference",
                definition.SpecificationReference);

            Assert.AreEqual(
                "Governed state must match the approved baseline.",
                definition.GovernedStateAssertion);
        }
    }
}