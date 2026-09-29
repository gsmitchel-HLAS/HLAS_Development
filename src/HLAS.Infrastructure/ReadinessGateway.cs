using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    public static class ReadinessGateway
    {
        public static ReadinessCheckResult Evaluate(
            string projectRoot,
            UserId userId,
            ProjectRole projectRole)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

            string fullProjectRoot =
                Path.GetFullPath(projectRoot);

            ProjectManifest manifest =
                ProjectPackageReader.Open(fullProjectRoot);

            GovernedOperationRecord operation =
                GovernedOperationService.Begin(
                    fullProjectRoot,
                    manifest.ProjectId,
                    userId,
                    SeriesId.V,
                    projectRole);

            try
            {
                ReadinessCheckId readinessCheckId =
                    ReadinessCheckId.CreateNew();

                GovernedTimestamp evaluatedUtc =
                    GovernedTimestamp.CreateNow();

                List<ReadinessCheckItemRecord> items =
                [
                    new ReadinessCheckItemRecord(
                        readinessCheckId,
                        1,
                        ReadinessGateCode.ProjectIdentity,
                        ReadinessGateStatus.Pass,
                        "Governed HLAS project identity opened successfully."),

                  EvaluateSourceEvidence(
    fullProjectRoot,
    readinessCheckId),

                    new ReadinessCheckItemRecord(
                        readinessCheckId,
                        3,
                        ReadinessGateCode.ProjectJmfTruth,
                        ReadinessGateStatus.Blocked,
                        "Project/JMF governed truth is not yet represented completely in the C# project database."),

                    new ReadinessCheckItemRecord(
                        readinessCheckId,
                        4,
                        ReadinessGateCode.Maintenance,
                        ReadinessGateStatus.Blocked,
                        "Unresolved governed maintenance evaluation is not yet implemented."),

                    new ReadinessCheckItemRecord(
                        readinessCheckId,
                        5,
                        ReadinessGateCode.ProjectJmfReview,
                        ReadinessGateStatus.Blocked,
                        "Project/JMF review state is not yet represented completely in the C# project database."),

                    new ReadinessCheckItemRecord(
                        readinessCheckId,
                        6,
                        ReadinessGateCode.Lineage,
                        ReadinessGateStatus.Blocked,
                        "Current-lineage consistency evaluation is not yet implemented."),

                  EvaluateFreezeCapability(
    fullProjectRoot,
    readinessCheckId)
                ];

                ReadinessCheckRecord check =
                    new(
                        readinessCheckId,
                        operation.OperationId,
                      items.Exists(
    item => item.GateStatus == ReadinessGateStatus.Blocked)
        ? ReadinessStatus.NotReady
        : ReadinessStatus.Ready,
                        evaluatedUtc);

                ReadinessCheckResult result =
                    new(
                        check,
                        items);

                using SqliteConnection connection =
                    OpenDatabase(fullProjectRoot);

                connection.Open();

                using SqliteTransaction transaction =
                    connection.BeginTransaction();

                InsertReadinessCheck(
                    connection,
                    transaction,
                    result.Check);

                foreach (ReadinessCheckItemRecord item in result.Items)
                {
                    InsertReadinessCheckItem(
                        connection,
                        transaction,
                        item);
                }

                GovernedOperationService.FinalizeSuccessInTransaction(
                    connection,
                    transaction,
                    operation.OperationId);

                transaction.Commit();

                return result;
            }
            catch (Exception exception)
            {
                DecisionRecord decisionRecord =
                    new(
                        IsSafeStopException(exception)
                            ? "READINESS SAFE-STOP"
                            : "READINESS TECHNICAL FAILURE",
                        exception.Message);

                if (IsSafeStopException(exception))
                {
                    GovernedOperationService.FinalizeSafeStop(
                        fullProjectRoot,
                        operation.OperationId,
                        decisionRecord);
                }
                else
                {
                    GovernedOperationService.FinalizeTechnicalFailure(
                        fullProjectRoot,
                        operation.OperationId,
                        decisionRecord);
                }

                throw;
            }
        }
        private static ReadinessCheckItemRecord EvaluateSourceEvidence(
    string fullProjectRoot,
    ReadinessCheckId readinessCheckId)
        {
            string databasePath =
                Path.Combine(
                    fullProjectRoot,
                    ProjectPackageCreator.DatabaseFileName);

            SqliteConnectionStringBuilder builder =
                new()
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadOnly,
                    Pooling = false
                };

            using SqliteConnection connection =
                new(builder.ToString());

            connection.Open();

            string requirementRevisionId;

            using (SqliteCommand revisionCommand =
                connection.CreateCommand())
            {
                revisionCommand.CommandText =
                    """
            SELECT
                revision.RequirementRevisionId,
                operation.CompletedUtc,
                operation.Outcome
            FROM HLAS_Source_Evidence_Requirement_Revisions AS revision
            LEFT JOIN HLAS_Governed_Operations AS operation
                ON operation.OperationId = revision.OperationId
            ORDER BY revision.RevisionNumber DESC
            LIMIT 1;
            """;

                using SqliteDataReader revisionReader =
                    revisionCommand.ExecuteReader();

                if (!revisionReader.Read())
                {
                    return new ReadinessCheckItemRecord(
                        readinessCheckId,
                        2,
                        ReadinessGateCode.SourceEvidence,
                        ReadinessGateStatus.Blocked,
                        "No governed Source Evidence Requirement revision exists.");
                }

                requirementRevisionId =
                    revisionReader.GetString(0);

                if (revisionReader.IsDBNull(1) ||
                    revisionReader.IsDBNull(2) ||
                    !string.Equals(
                        revisionReader.GetString(2),
                        "SUCCESS",
                        StringComparison.Ordinal))
                {
                    return new ReadinessCheckItemRecord(
                        readinessCheckId,
                        2,
                        ReadinessGateCode.SourceEvidence,
                        ReadinessGateStatus.Blocked,
                        "The current Source Evidence Requirement revision is not backed by a completed successful governed operation.");
                }
            }

            using SqliteCommand itemCommand =
                connection.CreateCommand();

            itemCommand.CommandText =
                """
        SELECT
            item.RequirementKey,
            item.RequirementState,
            item.EvidenceId,
            catalog.LifecycleState,
            custody.RelativeCustodyPath,
            custody.FileSizeBytes,
            custody.Sha256Hex
        FROM HLAS_Source_Evidence_Requirement_Items AS item
        LEFT JOIN HLAS_Source_Evidence_Catalog AS catalog
            ON catalog.EvidenceId = item.EvidenceId
        LEFT JOIN HLAS_Evidence_Custody AS custody
            ON custody.EvidenceId = item.EvidenceId
        WHERE item.RequirementRevisionId = $requirementRevisionId
        ORDER BY item.RequirementKey;
        """;

            itemCommand.Parameters.AddWithValue(
                "$requirementRevisionId",
                requirementRevisionId);

            using SqliteDataReader itemReader =
                itemCommand.ExecuteReader();

            bool foundRequirementItem = false;

            string sourceEvidenceRoot =
                Path.GetFullPath(
                    Path.Combine(
                        fullProjectRoot,
                        EvidenceCustodyService.SourceEvidenceDirectoryName));

            while (itemReader.Read())
            {
                foundRequirementItem = true;

                string requirementKey =
                    itemReader.GetString(0);

                string requirementState =
                    itemReader.GetString(1);

                if (string.Equals(
                        requirementState,
                        "PENDING",
                        StringComparison.Ordinal))
                {
                    return new ReadinessCheckItemRecord(
                        readinessCheckId,
                        2,
                        ReadinessGateCode.SourceEvidence,
                        ReadinessGateStatus.Blocked,
                        $"Required Source Evidence remains pending: {requirementKey}.");
                }

                if (string.Equals(
                        requirementState,
                        "NOT_REQUIRED",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.Equals(
                        requirementState,
                        "RECEIVED",
                        StringComparison.Ordinal))
                {
                    return new ReadinessCheckItemRecord(
                        readinessCheckId,
                        2,
                        ReadinessGateCode.SourceEvidence,
                        ReadinessGateStatus.Blocked,
                        $"Source Evidence requirement has an unsupported state: {requirementKey}.");
                }

                if (itemReader.IsDBNull(2) ||
                    itemReader.IsDBNull(3) ||
                    itemReader.IsDBNull(4) ||
                    itemReader.IsDBNull(5) ||
                    itemReader.IsDBNull(6))
                {
                    return new ReadinessCheckItemRecord(
                        readinessCheckId,
                        2,
                        ReadinessGateCode.SourceEvidence,
                        ReadinessGateStatus.Blocked,
                        $"Received Source Evidence does not resolve to complete governed custody: {requirementKey}.");
                }

                if (!string.Equals(
                        itemReader.GetString(3),
                        "Active",
                        StringComparison.Ordinal))
                {
                    return new ReadinessCheckItemRecord(
                        readinessCheckId,
                        2,
                        ReadinessGateCode.SourceEvidence,
                        ReadinessGateStatus.Blocked,
                        $"Received Source Evidence is not current active evidence: {requirementKey}.");
                }

                string relativeCustodyPath =
                    itemReader.GetString(4);

                string controlledFilePath =
                    Path.GetFullPath(
                        Path.Combine(
                            fullProjectRoot,
                            relativeCustodyPath));

                string relativeToEvidenceRoot =
                    Path.GetRelativePath(
                        sourceEvidenceRoot,
                        controlledFilePath);

                if (Path.IsPathRooted(relativeToEvidenceRoot) ||
                    relativeToEvidenceRoot == ".." ||
                    relativeToEvidenceRoot.StartsWith(
                        ".." + Path.DirectorySeparatorChar,
                        StringComparison.Ordinal) ||
                    relativeToEvidenceRoot.StartsWith(
                        ".." + Path.AltDirectorySeparatorChar,
                        StringComparison.Ordinal))
                {
                    return new ReadinessCheckItemRecord(
                        readinessCheckId,
                        2,
                        ReadinessGateCode.SourceEvidence,
                        ReadinessGateStatus.Blocked,
                        $"Received Source Evidence custody path is invalid: {requirementKey}.");
                }

                if (!File.Exists(controlledFilePath))
                {
                    return new ReadinessCheckItemRecord(
                        readinessCheckId,
                        2,
                        ReadinessGateCode.SourceEvidence,
                        ReadinessGateStatus.Blocked,
                        $"Received Source Evidence controlled file is missing: {requirementKey}.");
                }

                FileInfo controlledFile =
                    new(controlledFilePath);

                long governedFileSize =
                    itemReader.GetInt64(5);

                if (controlledFile.Length != governedFileSize)
                {
                    return new ReadinessCheckItemRecord(
                        readinessCheckId,
                        2,
                        ReadinessGateCode.SourceEvidence,
                        ReadinessGateStatus.Blocked,
                        $"Received Source Evidence size does not match governed custody: {requirementKey}.");
                }

                string actualSha256;

                using (FileStream stream =
                    new(
                        controlledFilePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read))
                {
                    using SHA256 hasher =
                        SHA256.Create();

                    byte[] hash =
                        hasher.ComputeHash(stream);

                    actualSha256 =
                        Convert
                            .ToHexString(hash)
                            .ToLowerInvariant();
                }

                if (!string.Equals(
                        actualSha256,
                        itemReader.GetString(6),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return new ReadinessCheckItemRecord(
                        readinessCheckId,
                        2,
                        ReadinessGateCode.SourceEvidence,
                        ReadinessGateStatus.Blocked,
                        $"Received Source Evidence SHA-256 does not match governed custody: {requirementKey}.");
                }
            }

            if (!foundRequirementItem)
            {
                return new ReadinessCheckItemRecord(
                    readinessCheckId,
                    2,
                    ReadinessGateCode.SourceEvidence,
                    ReadinessGateStatus.Blocked,
                    "The current Source Evidence Requirement revision contains no governed requirement items.");
            }

            return new ReadinessCheckItemRecord(
                readinessCheckId,
                2,
                ReadinessGateCode.SourceEvidence,
                ReadinessGateStatus.Pass,
                "All governed Source Evidence requirements are resolved and all received evidence passed controlled-custody integrity verification.");
        }
        private static ReadinessCheckItemRecord EvaluateFreezeCapability(
    string fullProjectRoot,
    ReadinessCheckId readinessCheckId)
        {
            string databasePath =
                Path.Combine(
                    fullProjectRoot,
                    ProjectPackageCreator.DatabaseFileName);

            SqliteConnectionStringBuilder builder =
                new()
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadOnly,
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
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE
                type = 'table'
                AND name = 'HLAS_Frozen_States';
            """;

                long tableCount =
                    Convert.ToInt64(
                        command.ExecuteScalar());

                if (tableCount != 1)
                {
                    return new ReadinessCheckItemRecord(
                        readinessCheckId,
                        7,
                        ReadinessGateCode.FreezeCapability,
                        ReadinessGateStatus.Blocked,
                        "Required HLAS_Frozen_States schema is not available.");
                }
            }

            string frozenStatesRoot =
                Path.Combine(
                    fullProjectRoot,
                    PreChangeFreezeService.FrozenStatesDirectoryName);

            string probeParent =
                Directory.Exists(frozenStatesRoot)
                    ? frozenStatesRoot
                    : fullProjectRoot;

            string probeDirectory =
                Path.Combine(
                    probeParent,
                    $".HLAS_Readiness_FreezeProbe_{Guid.NewGuid():N}");

            bool probeDirectoryCreated = false;

            try
            {
                Directory.CreateDirectory(
                    probeDirectory);

                probeDirectoryCreated = true;

                string probeFilePath =
                    Path.Combine(
                        probeDirectory,
                        "probe.tmp");

                File.WriteAllText(
                    probeFilePath,
                    "HLAS readiness freeze-capability probe.");

                File.Delete(
                    probeFilePath);

                Directory.Delete(
                    probeDirectory);

                probeDirectoryCreated = false;

                return new ReadinessCheckItemRecord(
                    readinessCheckId,
                    7,
                    ReadinessGateCode.FreezeCapability,
                    ReadinessGateStatus.Pass,
                    "Freeze schema exists and the controlled-store location passed a non-destructive write/delete capability probe.");
            }
            catch (UnauthorizedAccessException)
            {
                return new ReadinessCheckItemRecord(
                    readinessCheckId,
                    7,
                    ReadinessGateCode.FreezeCapability,
                    ReadinessGateStatus.Blocked,
                    "The controlled freeze-store location is not writable.");
            }
            catch (IOException)
            {
                return new ReadinessCheckItemRecord(
                    readinessCheckId,
                    7,
                    ReadinessGateCode.FreezeCapability,
                    ReadinessGateStatus.Blocked,
                    "The controlled freeze-store location could not complete the required write/delete capability probe.");
            }
            finally
            {
                if (probeDirectoryCreated &&
                    Directory.Exists(probeDirectory))
                {
                    try
                    {
                        Directory.Delete(
                            probeDirectory,
                            recursive: true);
                    }
                    catch
                    {
                        // Best-effort cleanup only.
                        // A failed probe never becomes a governed freeze event.
                    }
                }
            }
        }
        private static void InsertReadinessCheck(
            SqliteConnection connection,
            SqliteTransaction transaction,
            ReadinessCheckRecord record)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;

            command.CommandText =
                """
                INSERT INTO HLAS_Readiness_Checks
                (
                    ReadinessCheckId,
                    OperationId,
                    OverallStatus,
                    EvaluatedUtc
                )
                VALUES
                (
                    $readinessCheckId,
                    $operationId,
                    $overallStatus,
                    $evaluatedUtc
                );
                """;

            command.Parameters.AddWithValue(
                "$readinessCheckId",
                record.ReadinessCheckId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$operationId",
                record.OperationId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$overallStatus",
                record.OverallStatus.Value);

            command.Parameters.AddWithValue(
                "$evaluatedUtc",
                record.EvaluatedUtc.Value.ToString("O"));

            command.ExecuteNonQuery();
        }

        private static void InsertReadinessCheckItem(
            SqliteConnection connection,
            SqliteTransaction transaction,
            ReadinessCheckItemRecord record)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.Transaction = transaction;

            command.CommandText =
                """
                INSERT INTO HLAS_Readiness_Check_Items
                (
                    ReadinessCheckId,
                    GateSequence,
                    GateCode,
                    GateStatus,
                    Detail
                )
                VALUES
                (
                    $readinessCheckId,
                    $gateSequence,
                    $gateCode,
                    $gateStatus,
                    $detail
                );
                """;

            command.Parameters.AddWithValue(
                "$readinessCheckId",
                record.ReadinessCheckId.Value.ToString("D"));

            command.Parameters.AddWithValue(
                "$gateSequence",
                record.GateSequence);

            command.Parameters.AddWithValue(
                "$gateCode",
                record.GateCode.Value);

            command.Parameters.AddWithValue(
                "$gateStatus",
                record.GateStatus.Value);

            command.Parameters.AddWithValue(
                "$detail",
                (object?)record.Detail ?? DBNull.Value);

            command.ExecuteNonQuery();
        }

        private static SqliteConnection OpenDatabase(
            string fullProjectRoot)
        {
            string databasePath =
                Path.Combine(
                    fullProjectRoot,
                    ProjectPackageCreator.DatabaseFileName);

            SqliteConnectionStringBuilder builder =
                new()
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadWrite,
                    Pooling = false
                };

            return new SqliteConnection(
                builder.ToString());
        }

        private static bool IsSafeStopException(
            Exception exception)
        {
            return exception.Message.StartsWith(
                "SAFE-STOP:",
                StringComparison.Ordinal);
        }
    }
}