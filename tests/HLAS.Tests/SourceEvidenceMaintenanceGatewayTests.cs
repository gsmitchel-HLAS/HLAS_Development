using System;
using System.IO;
using HLAS.Domain;
using Microsoft.Data.Sqlite;
using HLAS.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class SourceEvidenceMaintenanceGatewayTests
    {
        [TestMethod]
        public void MetadataMutation_Keep_IsExplicitAndValueless()
        {
            SourceEvidenceMetadataMutation mutation =
                SourceEvidenceMetadataMutation.Keep();

            Assert.AreEqual(
                SourceEvidenceMetadataMutationAction.Keep,
                mutation.Action);

            Assert.IsNull(mutation.Value);
        }

        [TestMethod]
        public void MetadataMutation_Set_PreservesExactValue()
        {
            SourceEvidenceMetadataMutation mutation =
                SourceEvidenceMetadataMutation.Set(
                    "Exact infrastructure value");

            Assert.AreEqual(
                SourceEvidenceMetadataMutationAction.Set,
                mutation.Action);

            Assert.AreEqual(
                "Exact infrastructure value",
                mutation.Value);
        }

        [TestMethod]
        public void MetadataMutation_Clear_IsExplicitAndValueless()
        {
            SourceEvidenceMetadataMutation mutation =
                SourceEvidenceMetadataMutation.Clear();

            Assert.AreEqual(
                SourceEvidenceMetadataMutationAction.Clear,
                mutation.Action);

            Assert.IsNull(mutation.Value);
        }

        [TestMethod]
        public void MetadataMutation_SetBlank_Rejects()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => SourceEvidenceMetadataMutation.Set(" "));
        }

        [TestMethod]
        public void CorrectionRequest_BlankReason_Rejects()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new SourceEvidenceCorrectionRequest(
                    SourceEvidenceMetadataMutation.Keep(),
                    SourceEvidenceMetadataMutation.Keep(),
                    " "));
        }

        [TestMethod]
        public void CorrectionRequest_PreservesActionsAndReason()
        {
            SourceEvidenceCorrectionRequest request =
                new(
                    SourceEvidenceMetadataMutation.Keep(),
                    SourceEvidenceMetadataMutation.Clear(),
                    "Governed correction reason");

            Assert.AreEqual(
                SourceEvidenceMetadataMutationAction.Keep,
                request.DisplayLabel.Action);

            Assert.AreEqual(
                SourceEvidenceMetadataMutationAction.Clear,
                request.AdministrativeDescription.Action);

            Assert.AreEqual(
                "Governed correction reason",
                request.CorrectionReason);
        }
        [TestMethod]
        public void ReplacementRequest_BlankReason_Rejects()
        {
            Assert.ThrowsExactly<ArgumentException>(
                () => new SourceEvidenceReplacementRequest(
                    @"C:\Replacement\replacement.pdf",
                    SourceEvidenceMetadataMutation.Keep(),
                    SourceEvidenceMetadataMutation.Keep(),
                    " "));
        }

        [TestMethod]
        public void ReplacementRequest_PreservesPathActionsAndReason()
        {
            SourceEvidenceReplacementRequest request =
                new(
                    @"C:\Replacement\replacement.pdf",
                    SourceEvidenceMetadataMutation.Set(
                        "Successor display label"),
                    SourceEvidenceMetadataMutation.Clear(),
                    "Governed replacement reason");

            Assert.AreEqual(
                @"C:\Replacement\replacement.pdf",
                request.SelectedReplacementSourceFilePath);

            Assert.AreEqual(
                SourceEvidenceMetadataMutationAction.Set,
                request.DisplayLabel.Action);

            Assert.AreEqual(
                SourceEvidenceMetadataMutationAction.Clear,
                request.AdministrativeDescription.Action);

            Assert.AreEqual(
                "Governed replacement reason",
                request.ReplacementReason);
        }
        [TestMethod]
        public void Replace_DevelopmentalSource_PersistsGovernedSuccessorTransaction()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string projectRoot =
                Path.Combine(
                    testRoot,
                    "Project");

            string sourceDirectory =
                Path.Combine(
                    testRoot,
                    "Original");

            Directory.CreateDirectory(
                sourceDirectory);

            string priorSourcePath =
                Path.Combine(
                    sourceDirectory,
                    "Developmental REPLACE Prior.txt");

            string replacementSourcePath =
                Path.Combine(
                    sourceDirectory,
                    "Developmental REPLACE Successor.txt");

            File.WriteAllText(
                priorSourcePath,
                "HLAS developmental prior Source Evidence.");

            File.WriteAllText(
                replacementSourcePath,
                "HLAS developmental successor Source Evidence.");

            try
            {
                ProjectPackageCreator.CreateNew(
                    projectRoot);

                EvidenceCustodyRecord priorEvidence =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        priorSourcePath);

                SourceEvidenceReplacementRequest request =
                    new(
                        replacementSourcePath,
                        SourceEvidenceMetadataMutation.Set(
                            "Successor display label"),
                        SourceEvidenceMetadataMutation.Set(
                            "Successor administrative description"),
                        "Developmental REPLACE integration proof");

                SourceEvidenceMaintenanceRecord result =
                    SourceEvidenceMaintenanceGateway.Replace(
                        projectRoot,
                        UserId.CreateNew(),
                        ProjectRole.Admin,
                        priorEvidence.EvidenceId,
                        request);

                Assert.AreEqual(
                    priorEvidence.EvidenceId,
                    result.PriorEvidenceId);

                Assert.AreNotEqual(
                    priorEvidence.EvidenceId,
                    result.ResultingEvidenceId);

                Assert.AreEqual(
                    SourceEvidenceMaintenanceRecord.ReplaceMaintenanceType,
                    result.MaintenanceType);

                Assert.AreEqual(
                    "Developmental REPLACE integration proof",
                    result.ReplacementReason);

                Assert.AreEqual(
                    2L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Evidence_Custody"));

                Assert.AreEqual(
                    1L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Frozen_States"));

                Assert.AreEqual(
                    1L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Maintenance"));

                Assert.AreEqual(
                    2L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Maintenance_Changes"));

                Assert.AreEqual(
                    1L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Metadata_Versions"));

                VerifyStoredReplaceState(
                    projectRoot,
                    result);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void Replace_KeepAndClear_PreservesPriorMetadataAndCreatesExplicitSuccessorState()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string projectRoot =
                Path.Combine(
                    testRoot,
                    "Project");

            string sourceDirectory =
                Path.Combine(
                    testRoot,
                    "Original");

            Directory.CreateDirectory(
                sourceDirectory);

            string priorSourcePath =
                Path.Combine(
                    sourceDirectory,
                    "Developmental REPLACE Metadata Prior.txt");

            string replacementSourcePath =
                Path.Combine(
                    sourceDirectory,
                    "Developmental REPLACE Metadata Successor.txt");

            File.WriteAllText(
                priorSourcePath,
                "HLAS developmental prior metadata Source Evidence.");

            File.WriteAllText(
                replacementSourcePath,
                "HLAS developmental successor metadata Source Evidence.");

            try
            {
                ProjectPackageCreator.CreateNew(
                    projectRoot);

                EvidenceCustodyRecord priorEvidence =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        priorSourcePath);

                _ = SourceEvidenceMaintenanceGateway.Correct(
                    projectRoot,
                    UserId.CreateNew(),
                    ProjectRole.Admin,
                    priorEvidence.EvidenceId,
                    new SourceEvidenceCorrectionRequest(
                        SourceEvidenceMetadataMutation.Set(
                            "Prior display label"),
                        SourceEvidenceMetadataMutation.Set(
                            "Prior administrative description"),
                        "Establish prior metadata state"));

                SourceEvidenceMaintenanceRecord replaceResult =
                    SourceEvidenceMaintenanceGateway.Replace(
                        projectRoot,
                        UserId.CreateNew(),
                        ProjectRole.Admin,
                        priorEvidence.EvidenceId,
                        new SourceEvidenceReplacementRequest(
                            replacementSourcePath,
                            SourceEvidenceMetadataMutation.Keep(),
                            SourceEvidenceMetadataMutation.Clear(),
                            "KEEP CLEAR replacement proof"));

                VerifyReplaceKeepClearTemporalState(
                    projectRoot,
                    priorEvidence.EvidenceId,
                    replaceResult.ResultingEvidenceId);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void Replace_MaintenanceWriteFailure_RollsBackSuccessorAndCleansCustody()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string projectRoot =
                Path.Combine(
                    testRoot,
                    "Project");

            string sourceDirectory =
                Path.Combine(
                    testRoot,
                    "Original");

            Directory.CreateDirectory(
                sourceDirectory);

            string priorSourcePath =
                Path.Combine(
                    sourceDirectory,
                    "Developmental Failed REPLACE Prior.txt");

            string replacementSourcePath =
                Path.Combine(
                    sourceDirectory,
                    "Developmental Failed REPLACE Successor.txt");

            File.WriteAllText(
                priorSourcePath,
                "HLAS developmental prior failed REPLACE Source Evidence.");

            File.WriteAllText(
                replacementSourcePath,
                "HLAS developmental successor failed REPLACE Source Evidence.");

            try
            {
                ProjectPackageCreator.CreateNew(
                    projectRoot);

                EvidenceCustodyRecord priorEvidence =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        priorSourcePath);

                string databasePath =
                    Path.Combine(
                        projectRoot,
                        ProjectPackageCreator.DatabaseFileName);

                SqliteConnectionStringBuilder builder = new()
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadWrite,
                    Pooling = false
                };

                using (SqliteConnection connection =
                    new(builder.ToString()))
                {
                    connection.Open();

                    using SqliteCommand command =
                        connection.CreateCommand();

                    command.CommandText =
                        """
                CREATE TRIGGER HLAS_Test_ForceReplaceMaintenanceFailure
                BEFORE INSERT ON HLAS_Source_Evidence_Maintenance
                BEGIN
                    SELECT RAISE(
                        ABORT,
                        'forced developmental REPLACE maintenance failure');
                END;
                """;

                    command.ExecuteNonQuery();
                }

                Assert.ThrowsExactly<SqliteException>(
                    () => SourceEvidenceMaintenanceGateway.Replace(
                        projectRoot,
                        UserId.CreateNew(),
                        ProjectRole.Admin,
                        priorEvidence.EvidenceId,
                        new SourceEvidenceReplacementRequest(
                            replacementSourcePath,
                            SourceEvidenceMetadataMutation.Set(
                                "Failed successor label"),
                            SourceEvidenceMetadataMutation.Set(
                                "Failed successor description"),
                            "Forced REPLACE maintenance failure proof")));

                Assert.AreEqual(
                    1L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Evidence_Custody"));

                Assert.AreEqual(
                    1L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Catalog"));

                Assert.AreEqual(
                    1L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Frozen_States"));

                Assert.AreEqual(
                    0L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Maintenance"));

                Assert.AreEqual(
                    0L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Maintenance_Changes"));

                Assert.AreEqual(
                    0L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Metadata_Versions"));

                string sourceEvidenceRoot =
                    Path.Combine(
                        projectRoot,
                        EvidenceCustodyService.SourceEvidenceDirectoryName);

                Assert.HasCount(
    1,
    Directory.GetDirectories(
        sourceEvidenceRoot));

                using (SqliteConnection connection =
                    new(builder.ToString()))
                {
                    connection.Open();

                    using SqliteCommand command =
                        connection.CreateCommand();

                    command.CommandText =
                        """
                SELECT LifecycleState
                FROM HLAS_Source_Evidence_Catalog
                WHERE EvidenceId = $evidenceId;
                """;

                    command.Parameters.AddWithValue(
                        "$evidenceId",
                        priorEvidence.EvidenceId.Value.ToString("D"));

                    Assert.AreEqual(
                        "Active",
                        Convert.ToString(
                            command.ExecuteScalar()));
                }

                VerifySingleOperationOutcome(
                    projectRoot,
                    "TECHNICAL FAILURE",
                    "REPLACE TECHNICAL FAILURE");
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void Replace_MissingSelectedSource_SafeStopsBeforeGovernedHistory()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string projectRoot =
                Path.Combine(
                    testRoot,
                    "Project");

            string sourceDirectory =
                Path.Combine(
                    testRoot,
                    "Original");

            Directory.CreateDirectory(
                sourceDirectory);

            string priorSourcePath =
                Path.Combine(
                    sourceDirectory,
                    "Developmental Missing REPLACE Prior.txt");

            File.WriteAllText(
                priorSourcePath,
                "HLAS developmental prior Source Evidence.");

            try
            {
                ProjectPackageCreator.CreateNew(
                    projectRoot);

                EvidenceCustodyRecord priorEvidence =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        priorSourcePath);

                string missingReplacementPath =
                    Path.Combine(
                        sourceDirectory,
                        "Does Not Exist.txt");

                FileNotFoundException exception =
                    Assert.ThrowsExactly<FileNotFoundException>(
                        () => SourceEvidenceMaintenanceGateway.Replace(
                            projectRoot,
                            UserId.CreateNew(),
                            ProjectRole.Admin,
                            priorEvidence.EvidenceId,
                            new SourceEvidenceReplacementRequest(
                                missingReplacementPath,
                                SourceEvidenceMetadataMutation.Keep(),
                                SourceEvidenceMetadataMutation.Keep(),
                                "Missing replacement source proof")));

                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");

                Assert.AreEqual(
                    0L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Governed_Operations"));

                Assert.AreEqual(
                    0L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Frozen_States"));

                Assert.AreEqual(
                    0L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Maintenance"));

                Assert.AreEqual(
                    0L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Metadata_Versions"));

                Assert.AreEqual(
                    1L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Evidence_Custody"));

                Assert.AreEqual(
                    1L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Catalog"));
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void Replace_SupersededPriorEvidence_SafeStopsBeforeNewGovernedHistory()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string projectRoot =
                Path.Combine(
                    testRoot,
                    "Project");

            string sourceDirectory =
                Path.Combine(
                    testRoot,
                    "Original");

            Directory.CreateDirectory(
                sourceDirectory);

            string priorSourcePath =
                Path.Combine(
                    sourceDirectory,
                    "Developmental Superseded REPLACE Prior.txt");

            string firstReplacementPath =
                Path.Combine(
                    sourceDirectory,
                    "Developmental Superseded REPLACE First.txt");

            string secondReplacementPath =
                Path.Combine(
                    sourceDirectory,
                    "Developmental Superseded REPLACE Second.txt");

            File.WriteAllText(
                priorSourcePath,
                "HLAS developmental original Source Evidence.");

            File.WriteAllText(
                firstReplacementPath,
                "HLAS developmental first replacement.");

            File.WriteAllText(
                secondReplacementPath,
                "HLAS developmental second replacement.");

            try
            {
                ProjectPackageCreator.CreateNew(
                    projectRoot);

                EvidenceCustodyRecord priorEvidence =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        priorSourcePath);

                _ = SourceEvidenceMaintenanceGateway.Replace(
                    projectRoot,
                    UserId.CreateNew(),
                    ProjectRole.Admin,
                    priorEvidence.EvidenceId,
                    new SourceEvidenceReplacementRequest(
                        firstReplacementPath,
                        SourceEvidenceMetadataMutation.Keep(),
                        SourceEvidenceMetadataMutation.Keep(),
                        "First replacement proof"));

                long operationCountBeforeSecondAttempt =
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Governed_Operations");

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => SourceEvidenceMaintenanceGateway.Replace(
                            projectRoot,
                            UserId.CreateNew(),
                            ProjectRole.Admin,
                            priorEvidence.EvidenceId,
                            new SourceEvidenceReplacementRequest(
                                secondReplacementPath,
                                SourceEvidenceMetadataMutation.Keep(),
                                SourceEvidenceMetadataMutation.Keep(),
                                "Invalid second replacement proof")));

                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");

                Assert.AreEqual(
                    operationCountBeforeSecondAttempt,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Governed_Operations"));

                Assert.AreEqual(
                    1L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Maintenance"));

                Assert.AreEqual(
                    2L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Evidence_Custody"));
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void Replace_IdenticalPhysicalEvidence_SafeStopsBeforeGovernedHistory()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string projectRoot =
                Path.Combine(
                    testRoot,
                    "Project");

            string sourceDirectory =
                Path.Combine(
                    testRoot,
                    "Original");

            Directory.CreateDirectory(
                sourceDirectory);

            string priorSourcePath =
                Path.Combine(
                    sourceDirectory,
                    "Developmental Identical REPLACE Prior.txt");

            string replacementSourcePath =
                Path.Combine(
                    sourceDirectory,
                    "Developmental Identical REPLACE Successor.txt");

            const string identicalContents =
                "HLAS identical physical Source Evidence.";

            File.WriteAllText(
                priorSourcePath,
                identicalContents);

            File.WriteAllText(
                replacementSourcePath,
                identicalContents);

            try
            {
                ProjectPackageCreator.CreateNew(
                    projectRoot);

                EvidenceCustodyRecord priorEvidence =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        priorSourcePath);

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => SourceEvidenceMaintenanceGateway.Replace(
                            projectRoot,
                            UserId.CreateNew(),
                            ProjectRole.Admin,
                            priorEvidence.EvidenceId,
                            new SourceEvidenceReplacementRequest(
                                replacementSourcePath,
                                SourceEvidenceMetadataMutation.Keep(),
                                SourceEvidenceMetadataMutation.Keep(),
                                "Identical replacement proof")));

                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");

                Assert.AreEqual(
                    0L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Governed_Operations"));

                Assert.AreEqual(
                    0L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Frozen_States"));

                Assert.AreEqual(
                    0L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Maintenance"));

                Assert.AreEqual(
                    1L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Evidence_Custody"));

                Assert.AreEqual(
                    1L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Catalog"));
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void Correct_DevelopmentalSource_PersistsGovernedTransaction()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string projectRoot =
                Path.Combine(
                    testRoot,
                    "Project");

            string sourceDirectory =
                Path.Combine(
                    testRoot,
                    "Original");

            Directory.CreateDirectory(
                sourceDirectory);

            string sourcePath =
                Path.Combine(
                    sourceDirectory,
                    "Developmental CORRECT Source.txt");

            File.WriteAllText(
                sourcePath,
                "HLAS developmental CORRECT Source Evidence.");

            try
            {
                ProjectPackageCreator.CreateNew(
                    projectRoot);

                EvidenceCustodyRecord evidenceRecord =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        sourcePath);

                SourceEvidenceCorrectionRequest request =
                    new(
                        SourceEvidenceMetadataMutation.Set(
                            "Corrected display label"),
                        SourceEvidenceMetadataMutation.Set(
                            "Corrected administrative description"),
                        "Developmental CORRECT integration proof");

                SourceEvidenceMaintenanceRecord result =
                    SourceEvidenceMaintenanceGateway.Correct(
                        projectRoot,
                        UserId.CreateNew(),
                        ProjectRole.Admin,
                        evidenceRecord.EvidenceId,
                        request);

                Assert.AreEqual(
                    evidenceRecord.EvidenceId,
                    result.PriorEvidenceId);

                Assert.AreEqual(
                    evidenceRecord.EvidenceId,
                    result.ResultingEvidenceId);

                Assert.AreEqual(
                    SourceEvidenceMaintenanceRecord.CorrectMaintenanceType,
                    result.MaintenanceType);

                Assert.AreEqual(
                    1L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Frozen_States"));

                Assert.AreEqual(
                    1L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Maintenance"));

                Assert.AreEqual(
                    2L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Maintenance_Changes"));

                Assert.AreEqual(
                    1L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Metadata_Versions"));

                VerifyStoredCorrectState(
                    projectRoot,
                    result.OperationId);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void Correct_SecondCorrection_PreservesPriorVersionAndCarriesForwardState()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string projectRoot =
                Path.Combine(
                    testRoot,
                    "Project");

            string sourceDirectory =
                Path.Combine(
                    testRoot,
                    "Original");

            Directory.CreateDirectory(
                sourceDirectory);

            string sourcePath =
                Path.Combine(
                    sourceDirectory,
                    "Developmental Temporal CORRECT Source.txt");

            File.WriteAllText(
                sourcePath,
                "HLAS developmental Temporal CORRECT Source Evidence.");

            try
            {
                ProjectPackageCreator.CreateNew(
                    projectRoot);

                EvidenceCustodyRecord evidenceRecord =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        sourcePath);

                SourceEvidenceMaintenanceRecord firstResult =
                    SourceEvidenceMaintenanceGateway.Correct(
                        projectRoot,
                        UserId.CreateNew(),
                        ProjectRole.Admin,
                        evidenceRecord.EvidenceId,
                        new SourceEvidenceCorrectionRequest(
                            SourceEvidenceMetadataMutation.Set(
                                "Version 1 display label"),
                            SourceEvidenceMetadataMutation.Set(
                                "Version 1 administrative description"),
                            "First developmental correction"));

                SourceEvidenceMaintenanceRecord secondResult =
                    SourceEvidenceMaintenanceGateway.Correct(
                        projectRoot,
                        UserId.CreateNew(),
                        ProjectRole.Admin,
                        evidenceRecord.EvidenceId,
                        new SourceEvidenceCorrectionRequest(
                            SourceEvidenceMetadataMutation.Keep(),
                            SourceEvidenceMetadataMutation.Clear(),
                            "Second developmental correction"));

                Assert.AreEqual(
                    evidenceRecord.EvidenceId,
                    secondResult.ResultingEvidenceId);

                Assert.AreNotEqual(
                    firstResult.OperationId,
                    secondResult.OperationId);

                Assert.AreEqual(
                    2L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Frozen_States"));

                Assert.AreEqual(
                    2L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Maintenance"));

                Assert.AreEqual(
                    3L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Maintenance_Changes"));

                Assert.AreEqual(
                    2L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Metadata_Versions"));

                VerifyTwoVersionTemporalState(
                    projectRoot,
                    firstResult.OperationId,
                    secondResult.OperationId);
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void Correct_NoMetadataChange_SafeStopsBeforeGovernedHistory()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string projectRoot =
                Path.Combine(
                    testRoot,
                    "Project");

            string sourceDirectory =
                Path.Combine(
                    testRoot,
                    "Original");

            Directory.CreateDirectory(
                sourceDirectory);

            string sourcePath =
                Path.Combine(
                    sourceDirectory,
                    "Developmental NoOp CORRECT Source.txt");

            File.WriteAllText(
                sourcePath,
                "HLAS developmental no-op CORRECT Source Evidence.");

            try
            {
                ProjectPackageCreator.CreateNew(
                    projectRoot);

                EvidenceCustodyRecord evidenceRecord =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        sourcePath);

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => SourceEvidenceMaintenanceGateway.Correct(
                            projectRoot,
                            UserId.CreateNew(),
                            ProjectRole.Admin,
                            evidenceRecord.EvidenceId,
                            new SourceEvidenceCorrectionRequest(
                                SourceEvidenceMetadataMutation.Keep(),
                                SourceEvidenceMetadataMutation.Keep(),
                                "No-op developmental correction")));

                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");

                Assert.AreEqual(
                    0L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Governed_Operations"));

                Assert.AreEqual(
                    0L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Frozen_States"));

                Assert.AreEqual(
                    0L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Maintenance"));

                Assert.AreEqual(
                    0L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Metadata_Versions"));
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void Correct_MissingControlledEvidence_RecordsSafeStop()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string projectRoot =
                Path.Combine(
                    testRoot,
                    "Project");

            string sourceDirectory =
                Path.Combine(
                    testRoot,
                    "Original");

            Directory.CreateDirectory(
                sourceDirectory);

            string sourcePath =
                Path.Combine(
                    sourceDirectory,
                    "Developmental Missing CORRECT Source.txt");

            File.WriteAllText(
                sourcePath,
                "HLAS developmental missing CORRECT Source Evidence.");

            try
            {
                ProjectPackageCreator.CreateNew(
                    projectRoot);

                EvidenceCustodyRecord evidenceRecord =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        sourcePath);

                string controlledFilePath =
                    Path.Combine(
                        projectRoot,
                        evidenceRecord.RelativeCustodyPath);

                File.Delete(
                    controlledFilePath);

                FileNotFoundException exception =
    Assert.ThrowsExactly<FileNotFoundException>(
                        () => SourceEvidenceMaintenanceGateway.Correct(
                            projectRoot,
                            UserId.CreateNew(),
                            ProjectRole.Admin,
                            evidenceRecord.EvidenceId,
                            new SourceEvidenceCorrectionRequest(
                                SourceEvidenceMetadataMutation.Set(
                                    "Changed label"),
                                SourceEvidenceMetadataMutation.Keep(),
                                "Missing controlled evidence proof")));

                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");

                Assert.AreEqual(
                    1L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Governed_Operations"));

                Assert.AreEqual(
                    0L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Frozen_States"));

                Assert.AreEqual(
                    0L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Maintenance"));

                Assert.AreEqual(
                    0L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Metadata_Versions"));

                VerifySingleOperationOutcome(
                    projectRoot,
                    "SAFE-STOP",
                    "CORRECT SAFE-STOP");
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void Correct_TamperedControlledEvidence_RecordsSafeStop()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string projectRoot =
                Path.Combine(
                    testRoot,
                    "Project");

            string sourceDirectory =
                Path.Combine(
                    testRoot,
                    "Original");

            Directory.CreateDirectory(
                sourceDirectory);

            string sourcePath =
                Path.Combine(
                    sourceDirectory,
                    "Developmental Tampered CORRECT Source.txt");

            File.WriteAllText(
                sourcePath,
                "HLAS developmental tampered CORRECT Source Evidence.");

            try
            {
                ProjectPackageCreator.CreateNew(
                    projectRoot);

                EvidenceCustodyRecord evidenceRecord =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        sourcePath);

                string controlledFilePath =
                    Path.Combine(
                        projectRoot,
                        evidenceRecord.RelativeCustodyPath);

                File.AppendAllText(
                    controlledFilePath,
                    "TAMPERED");

                InvalidOperationException exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () => SourceEvidenceMaintenanceGateway.Correct(
                            projectRoot,
                            UserId.CreateNew(),
                            ProjectRole.Admin,
                            evidenceRecord.EvidenceId,
                            new SourceEvidenceCorrectionRequest(
                                SourceEvidenceMetadataMutation.Set(
                                    "Changed label"),
                                SourceEvidenceMetadataMutation.Keep(),
                                "Tampered controlled evidence proof")));

                StringAssert.Contains(
                    exception.Message,
                    "SAFE-STOP");

                Assert.AreEqual(
                    1L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Governed_Operations"));

                Assert.AreEqual(
                    0L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Frozen_States"));

                Assert.AreEqual(
                    0L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Maintenance"));

                Assert.AreEqual(
                    0L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Metadata_Versions"));

                VerifySingleOperationOutcome(
                    projectRoot,
                    "SAFE-STOP",
                    "CORRECT SAFE-STOP");
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        [TestMethod]
        public void Correct_MaintenanceWriteFailure_RollsBackAndRecordsTechnicalFailure()
        {
            string testRoot =
                CreateTemporaryTestRoot();

            string projectRoot =
                Path.Combine(
                    testRoot,
                    "Project");

            string sourceDirectory =
                Path.Combine(
                    testRoot,
                    "Original");

            Directory.CreateDirectory(
                sourceDirectory);

            string sourcePath =
                Path.Combine(
                    sourceDirectory,
                    "Developmental Failed CORRECT Source.txt");

            File.WriteAllText(
                sourcePath,
                "HLAS developmental failed CORRECT Source Evidence.");

            try
            {
                ProjectPackageCreator.CreateNew(
                    projectRoot);

                EvidenceCustodyRecord evidenceRecord =
                    ProductionSourceEvidenceIntakeGateway.Accept(
                        projectRoot,
                        sourcePath);

                string databasePath =
                    Path.Combine(
                        projectRoot,
                        ProjectPackageCreator.DatabaseFileName);

                SqliteConnectionStringBuilder builder = new()
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadWrite,
                    Pooling = false
                };

                using (SqliteConnection connection =
                    new(builder.ToString()))
                {
                    connection.Open();

                    using SqliteCommand command =
                        connection.CreateCommand();

                    command.CommandText =
                        """
                CREATE TRIGGER HLAS_Test_ForceMaintenanceFailure
                BEFORE INSERT ON HLAS_Source_Evidence_Maintenance
                BEGIN
                    SELECT RAISE(
                        ABORT,
                        'forced developmental maintenance failure');
                END;
                """;

                    command.ExecuteNonQuery();
                }

                Assert.ThrowsExactly<SqliteException>(
                    () => SourceEvidenceMaintenanceGateway.Correct(
                        projectRoot,
                        UserId.CreateNew(),
                        ProjectRole.Admin,
                        evidenceRecord.EvidenceId,
                        new SourceEvidenceCorrectionRequest(
                            SourceEvidenceMetadataMutation.Set(
                                "Changed label"),
                            SourceEvidenceMetadataMutation.Keep(),
                            "Forced maintenance failure proof")));

                Assert.AreEqual(
                    1L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Governed_Operations"));

                Assert.AreEqual(
                    1L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Frozen_States"));

                Assert.AreEqual(
                    0L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Maintenance"));

                Assert.AreEqual(
                    0L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Maintenance_Changes"));

                Assert.AreEqual(
                    0L,
                    ReadRecordCount(
                        projectRoot,
                        "HLAS_Source_Evidence_Metadata_Versions"));

                VerifySingleOperationOutcome(
                    projectRoot,
                    "TECHNICAL FAILURE",
                    "CORRECT TECHNICAL FAILURE");
            }
            finally
            {
                DeleteTemporaryTestRoot(
                    testRoot);
            }
        }
        private static void VerifyStoredReplaceState(
    string projectRoot,
    SourceEvidenceMaintenanceRecord result)
        {
            string databasePath =
                Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.DatabaseFileName);

            SqliteConnectionStringBuilder builder = new()
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            };

            using SqliteConnection connection =
                new(builder.ToString());

            connection.Open();

            using (SqliteCommand maintenanceCommand =
                connection.CreateCommand())
            {
                maintenanceCommand.CommandText =
                    """
            SELECT
                MaintenanceType,
                PriorEvidenceId,
                ResultingEvidenceId,
                ReplacementReason
            FROM HLAS_Source_Evidence_Maintenance
            WHERE OperationId = $operationId;
            """;

                maintenanceCommand.Parameters.AddWithValue(
                    "$operationId",
                    result.OperationId.Value.ToString("D"));

                using SqliteDataReader reader =
                    maintenanceCommand.ExecuteReader();

                Assert.IsTrue(reader.Read());

                Assert.AreEqual(
                    "REPLACE",
                    reader.GetString(0));

                Assert.AreEqual(
                    result.PriorEvidenceId.Value.ToString("D"),
                    reader.GetString(1));

                Assert.AreEqual(
                    result.ResultingEvidenceId.Value.ToString("D"),
                    reader.GetString(2));

                Assert.AreEqual(
                    "Developmental REPLACE integration proof",
                    reader.GetString(3));
            }

            using (SqliteCommand catalogCommand =
                connection.CreateCommand())
            {
                catalogCommand.CommandText =
                    """
            SELECT LifecycleState
            FROM HLAS_Source_Evidence_Catalog
            WHERE EvidenceId = $evidenceId;
            """;

                catalogCommand.Parameters.AddWithValue(
                    "$evidenceId",
                    result.PriorEvidenceId.Value.ToString("D"));

                Assert.AreEqual(
                    "Superseded",
                    Convert.ToString(
                        catalogCommand.ExecuteScalar()));

                catalogCommand.Parameters["$evidenceId"].Value =
                    result.ResultingEvidenceId.Value.ToString("D");

                Assert.AreEqual(
                    "Active",
                    Convert.ToString(
                        catalogCommand.ExecuteScalar()));
            }

            using (SqliteCommand metadataCommand =
                connection.CreateCommand())
            {
                metadataCommand.CommandText =
                    """
            SELECT
                VersionNumber,
                PriorMetadataVersionId,
                DisplayLabel,
                AdministrativeDescription,
                CorrectionReason,
                OperationId
            FROM HLAS_Source_Evidence_Metadata_Versions
            WHERE EvidenceId = $evidenceId;
            """;

                metadataCommand.Parameters.AddWithValue(
                    "$evidenceId",
                    result.ResultingEvidenceId.Value.ToString("D"));

                using SqliteDataReader reader =
                    metadataCommand.ExecuteReader();

                Assert.IsTrue(reader.Read());

                Assert.AreEqual(
                    1L,
                    reader.GetInt64(0));

                Assert.IsTrue(
                    reader.IsDBNull(1));

                Assert.AreEqual(
                    "Successor display label",
                    reader.GetString(2));

                Assert.AreEqual(
                    "Successor administrative description",
                    reader.GetString(3));

                Assert.IsTrue(
                    reader.IsDBNull(4));

                Assert.AreEqual(
                    result.OperationId.Value.ToString("D"),
                    reader.GetString(5));
            }

            using SqliteCommand operationCommand =
                connection.CreateCommand();

            operationCommand.CommandText =
                """
        SELECT Outcome
        FROM HLAS_Governed_Operations
        WHERE OperationId = $operationId;
        """;

            operationCommand.Parameters.AddWithValue(
                "$operationId",
                result.OperationId.Value.ToString("D"));

            Assert.AreEqual(
                "SUCCESS",
                Convert.ToString(
                    operationCommand.ExecuteScalar()));
        }
        private static void VerifyReplaceKeepClearTemporalState(
    string projectRoot,
    EvidenceId priorEvidenceId,
    EvidenceId successorEvidenceId)
        {
            string databasePath =
                Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.DatabaseFileName);

            SqliteConnectionStringBuilder builder = new()
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            };

            using SqliteConnection connection =
                new(builder.ToString());

            connection.Open();

            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
        SELECT
            EvidenceId,
            VersionNumber,
            PriorMetadataVersionId,
            DisplayLabel,
            AdministrativeDescription,
            CorrectionReason
        FROM HLAS_Source_Evidence_Metadata_Versions
        WHERE EvidenceId IN
        (
            $priorEvidenceId,
            $successorEvidenceId
        )
        ORDER BY VersionedUtc;
        """;

            command.Parameters.AddWithValue(
                "$priorEvidenceId",
                priorEvidenceId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$successorEvidenceId",
                successorEvidenceId.Value.ToString("D"));

            using SqliteDataReader reader =
                command.ExecuteReader();

            Assert.IsTrue(
                reader.Read());

            Assert.AreEqual(
                priorEvidenceId.Value.ToString("D"),
                reader.GetString(0));

            Assert.AreEqual(
                1L,
                reader.GetInt64(1));

            Assert.IsNull(
                reader.IsDBNull(2)
                    ? null
                    : reader.GetString(2));

            Assert.AreEqual(
                "Prior display label",
                reader.GetString(3));

            Assert.AreEqual(
                "Prior administrative description",
                reader.GetString(4));

            Assert.AreEqual(
                "Establish prior metadata state",
                reader.GetString(5));

            Assert.IsTrue(
                reader.Read());

            Assert.AreEqual(
                successorEvidenceId.Value.ToString("D"),
                reader.GetString(0));

            Assert.AreEqual(
                1L,
                reader.GetInt64(1));

            Assert.IsTrue(
                reader.IsDBNull(2));

            Assert.AreEqual(
                "Prior display label",
                reader.GetString(3));

            Assert.IsTrue(
                reader.IsDBNull(4));

            Assert.IsTrue(
                reader.IsDBNull(5));

            Assert.IsFalse(
                reader.Read());
        }
        private static void VerifySingleOperationOutcome(
    string projectRoot,
    string expectedOutcome,
    string expectedDecision)
        {
            string databasePath =
                Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.DatabaseFileName);

            SqliteConnectionStringBuilder builder = new()
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            };

            using SqliteConnection connection =
                new(builder.ToString());

            connection.Open();

            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
        SELECT
            Outcome,
            Decision
        FROM HLAS_Governed_Operations;
        """;

            using SqliteDataReader reader =
                command.ExecuteReader();

            Assert.IsTrue(
                reader.Read());

            Assert.AreEqual(
                expectedOutcome,
                reader.GetString(0));

            Assert.AreEqual(
                expectedDecision,
                reader.GetString(1));

            Assert.IsFalse(
                reader.Read());
        }
        private static void VerifyTwoVersionTemporalState(
    string projectRoot,
    OperationId firstOperationId,
    OperationId secondOperationId)
        {
            string databasePath =
                Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.DatabaseFileName);

            SqliteConnectionStringBuilder builder = new()
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            };

            using SqliteConnection connection =
                new(builder.ToString());

            connection.Open();

            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
        SELECT
            MetadataVersionId,
            VersionNumber,
            PriorMetadataVersionId,
            DisplayLabel,
            AdministrativeDescription,
            CorrectionReason,
            OperationId
        FROM HLAS_Source_Evidence_Metadata_Versions
        ORDER BY VersionNumber;
        """;

            using SqliteDataReader reader =
                command.ExecuteReader();

            Assert.IsTrue(
                reader.Read());

            string version1Id =
                reader.GetString(0);

            Assert.AreEqual(
                1L,
                reader.GetInt64(1));

            Assert.IsTrue(
                reader.IsDBNull(2));

            Assert.AreEqual(
                "Version 1 display label",
                reader.GetString(3));

            Assert.AreEqual(
                "Version 1 administrative description",
                reader.GetString(4));

            Assert.AreEqual(
                "First developmental correction",
                reader.GetString(5));

            Assert.AreEqual(
                firstOperationId.Value.ToString("D"),
                reader.GetString(6));

            Assert.IsTrue(
                reader.Read());

            Assert.AreEqual(
                2L,
                reader.GetInt64(1));

            Assert.AreEqual(
                version1Id,
                reader.GetString(2));

            Assert.AreEqual(
                "Version 1 display label",
                reader.GetString(3));

            Assert.IsTrue(
                reader.IsDBNull(4));

            Assert.AreEqual(
                "Second developmental correction",
                reader.GetString(5));

            Assert.AreEqual(
                secondOperationId.Value.ToString("D"),
                reader.GetString(6));

            Assert.IsFalse(
                reader.Read());
        }
        private static void VerifyStoredCorrectState(
    string projectRoot,
    OperationId operationId)
        {
            string databasePath =
                Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.DatabaseFileName);

            SqliteConnectionStringBuilder builder = new()
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            };

            using SqliteConnection connection =
                new(builder.ToString());

            connection.Open();

            using (SqliteCommand metadataCommand =
                connection.CreateCommand())
            {
                metadataCommand.CommandText =
                    """
            SELECT
                VersionNumber,
                DisplayLabel,
                AdministrativeDescription,
                CorrectionReason
            FROM HLAS_Source_Evidence_Metadata_Versions;
            """;

                using SqliteDataReader reader =
                    metadataCommand.ExecuteReader();

                Assert.IsTrue(
                    reader.Read());

                Assert.AreEqual(
                    1L,
                    reader.GetInt64(0));

                Assert.AreEqual(
                    "Corrected display label",
                    reader.GetString(1));

                Assert.AreEqual(
                    "Corrected administrative description",
                    reader.GetString(2));

                Assert.AreEqual(
                    "Developmental CORRECT integration proof",
                    reader.GetString(3));
            }

            using SqliteCommand operationCommand =
                connection.CreateCommand();

            operationCommand.CommandText =
                """
        SELECT Outcome
        FROM HLAS_Governed_Operations
        WHERE OperationId = $operationId;
        """;

            operationCommand.Parameters.AddWithValue(
                "$operationId",
                operationId.Value.ToString("D"));

            Assert.AreEqual(
                "SUCCESS",
                Convert.ToString(
                    operationCommand.ExecuteScalar()));
        }

        private static long ReadRecordCount(
            string projectRoot,
            string tableName)
        {
            string databasePath =
                Path.Combine(
                    projectRoot,
                    ProjectPackageCreator.DatabaseFileName);

            SqliteConnectionStringBuilder builder = new()
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            };

            using SqliteConnection connection =
                new(builder.ToString());

            connection.Open();

            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                $"SELECT COUNT(*) FROM {tableName};";

            return Convert.ToInt64(
                command.ExecuteScalar());
        }

        private static string CreateTemporaryTestRoot()
        {
            string testRoot =
                Path.Combine(
                    Path.GetTempPath(),
                    "HLAS_Tests",
                    Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(
                testRoot);

            return testRoot;
        }

        private static void DeleteTemporaryTestRoot(
            string testRoot)
        {
            SqliteConnection.ClearAllPools();

            if (Directory.Exists(testRoot))
            {
                Directory.Delete(
                    testRoot,
                    recursive: true);
            }
        }
    }
    }
