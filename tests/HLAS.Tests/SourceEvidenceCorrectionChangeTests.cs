using System;
using HLAS.Application;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class SourceEvidenceCorrectionChangeTests
    {
        [TestMethod]
        public void MetadataFieldChange_Keep_IsExplicitAndValueless()
        {
            SourceEvidenceMetadataFieldChange change =
                SourceEvidenceMetadataFieldChange.Keep();

            Assert.AreEqual(
                SourceEvidenceMetadataChangeAction.Keep,
                change.Action);

            Assert.IsNull(change.Value);
        }

        [TestMethod]
        public void MetadataFieldChange_Set_PreservesExactValue()
        {
            SourceEvidenceMetadataFieldChange change =
                SourceEvidenceMetadataFieldChange.Set(
                    "Exact label value");

            Assert.AreEqual(
                SourceEvidenceMetadataChangeAction.Set,
                change.Action);

            Assert.AreEqual(
                "Exact label value",
                change.Value);
        }

        [TestMethod]
        public void MetadataFieldChange_Clear_IsExplicitAndValueless()
        {
            SourceEvidenceMetadataFieldChange change =
                SourceEvidenceMetadataFieldChange.Clear();

            Assert.AreEqual(
                SourceEvidenceMetadataChangeAction.Clear,
                change.Action);

            Assert.IsNull(change.Value);
        }

        [TestMethod]
        public void MetadataFieldChange_SetBlank_Rejects()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => SourceEvidenceMetadataFieldChange.Set(" "));
        }

        [TestMethod]
        public void CorrectionChange_BlankReason_Rejects()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new SourceEvidenceCorrectionChange(
                    SourceEvidenceMetadataFieldChange.Keep(),
                    SourceEvidenceMetadataFieldChange.Keep(),
                    " "));
        }

        [TestMethod]
        public void CorrectionChange_PreservesExplicitActionsAndReason()
        {
            SourceEvidenceCorrectionChange change =
                new(
                    SourceEvidenceMetadataFieldChange.Keep(),
                    SourceEvidenceMetadataFieldChange.Clear(),
                    "Authorized correction reason");

            Assert.AreEqual(
                SourceEvidenceMetadataChangeAction.Keep,
                change.DisplayLabel.Action);

            Assert.AreEqual(
                SourceEvidenceMetadataChangeAction.Clear,
                change.AdministrativeDescription.Action);

            Assert.AreEqual(
                "Authorized correction reason",
                change.CorrectionReason);
        }
    }
}