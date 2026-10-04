using System;
using System.Collections.Generic;
using System.IO;
using HLAS.Domain;
using Microsoft.Data.Sqlite;

namespace HLAS.Infrastructure
{
    public static class LineageReadinessEvaluator
    {
        public static ReadinessCheckItemRecord Evaluate(
            string projectRoot,
            ReadinessCheckId readinessCheckId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

            string fullProjectRoot =
                Path.GetFullPath(projectRoot);

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

            string? evidenceLifecycleProblem =
                FindEvidenceSuccessorLifecycleProblem(
                    connection);

            if (evidenceLifecycleProblem is not null)
            {
                return Blocked(
                    readinessCheckId,
                    evidenceLifecycleProblem);
            }

            string? maintenanceMetadataProblem =
                FindMaintenanceFreezeMetadataProblem(
                    connection);

            if (maintenanceMetadataProblem is not null)
            {
                return Blocked(
                    readinessCheckId,
                    maintenanceMetadataProblem);
            }
            string? requirementLineageProblem =
    FindSourceEvidenceRequirementLineageProblem(
        connection);

            if (requirementLineageProblem is not null)
            {
                return Blocked(
                    readinessCheckId,
                    requirementLineageProblem);
            }
            string? projectJmfLineageProblem =
     FindProjectJmfLineageProblem(
         connection);

            if (projectJmfLineageProblem is not null)
            {
                return Blocked(
                    readinessCheckId,
                    projectJmfLineageProblem);
            }

            string? projectJmfReviewLineageProblem =
                FindProjectJmfReviewLineageProblem(
                    connection);

            if (projectJmfReviewLineageProblem is not null)
            {
                return Blocked(
                    readinessCheckId,
                    projectJmfReviewLineageProblem);
            }
            string? temporalOrderingProblem =
    FindTemporalOrderingProblem(
        connection);

            if (temporalOrderingProblem is not null)
            {
                return Blocked(
                    readinessCheckId,
                    temporalOrderingProblem);
            }
            return new ReadinessCheckItemRecord(
                readinessCheckId,
                6,
                ReadinessGateCode.Lineage,
                ReadinessGateStatus.Pass,
                "Source Evidence successor/lifecycle and maintenance/freeze/metadata lineage are structurally consistent.");
        }

        private static string? FindEvidenceSuccessorLifecycleProblem(
            SqliteConnection connection)
        {
            Dictionary<string, string> outgoingReplacement =
                new(StringComparer.OrdinalIgnoreCase);

            HashSet<string> incomingReplacement =
                new(StringComparer.OrdinalIgnoreCase);

            using (SqliteCommand replaceCommand =
                connection.CreateCommand())
            {
                replaceCommand.CommandText =
                    """
                    SELECT
                        maintenance.PriorEvidenceId,
                        maintenance.ResultingEvidenceId,
                        operation.CompletedUtc,
                        operation.Outcome,
                        priorCatalog.SourceClass,
                        resultingCatalog.SourceClass
                    FROM HLAS_Source_Evidence_Maintenance AS maintenance
                    LEFT JOIN HLAS_Governed_Operations AS operation
                        ON operation.OperationId = maintenance.OperationId
                    LEFT JOIN HLAS_Source_Evidence_Catalog AS priorCatalog
                        ON priorCatalog.EvidenceId = maintenance.PriorEvidenceId
                    LEFT JOIN HLAS_Source_Evidence_Catalog AS resultingCatalog
                        ON resultingCatalog.EvidenceId = maintenance.ResultingEvidenceId
                    WHERE maintenance.MaintenanceType = 'REPLACE';
                    """;

                using SqliteDataReader reader =
                    replaceCommand.ExecuteReader();

                while (reader.Read())
                {
                    if (reader.IsDBNull(0) ||
                        reader.IsDBNull(1) ||
                        reader.IsDBNull(2) ||
                        reader.IsDBNull(3) ||
                        reader.IsDBNull(4) ||
                        reader.IsDBNull(5))
                    {
                        return "A governed REPLACE lineage edge is incomplete.";
                    }

                    string priorEvidenceId =
                        reader.GetString(0);

                    string resultingEvidenceId =
                        reader.GetString(1);

                    if (!string.Equals(
                            reader.GetString(3),
                            "SUCCESS",
                            StringComparison.Ordinal))
                    {
                        return "A governed REPLACE maintenance record is not backed by a completed successful operation.";
                    }

                    if (string.Equals(
                            priorEvidenceId,
                            resultingEvidenceId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return "A governed REPLACE lineage edge points to the same EvidenceId.";
                    }

                    if (!string.Equals(
                            reader.GetString(4),
                            reader.GetString(5),
                            StringComparison.Ordinal))
                    {
                        return "A governed REPLACE successor changed SourceClass across the evidence lineage.";
                    }

                    if (outgoingReplacement.ContainsKey(
                            priorEvidenceId))
                    {
                        return "Source Evidence replacement lineage contains more than one successor from the same predecessor.";
                    }

                    if (!incomingReplacement.Add(
                            resultingEvidenceId))
                    {
                        return "Source Evidence replacement lineage contains more than one predecessor for the same successor.";
                    }

                    outgoingReplacement.Add(
                        priorEvidenceId,
                        resultingEvidenceId);
                }
            }

            foreach (string startingEvidenceId in
                outgoingReplacement.Keys)
            {
                HashSet<string> path =
                    new(StringComparer.OrdinalIgnoreCase);

                string currentEvidenceId =
                    startingEvidenceId;

                while (outgoingReplacement.TryGetValue(
                    currentEvidenceId,
                    out string? nextEvidenceId))
                {
                    if (!path.Add(
                            currentEvidenceId))
                    {
                        return "Source Evidence replacement lineage contains a cycle.";
                    }

                    currentEvidenceId =
                        nextEvidenceId;
                }
            }

            using (SqliteCommand correctCommand =
                connection.CreateCommand())
            {
                correctCommand.CommandText =
                    """
                    SELECT COUNT(*)
                    FROM HLAS_Source_Evidence_Maintenance
                    WHERE
                        MaintenanceType = 'CORRECT'
                        AND PriorEvidenceId <> ResultingEvidenceId;
                    """;

                if (Convert.ToInt64(
                        correctCommand.ExecuteScalar()) > 0)
                {
                    return "CORRECT maintenance created an invalid Source Evidence successor edge.";
                }
            }

            using SqliteCommand lifecycleCommand =
                connection.CreateCommand();

            lifecycleCommand.CommandText =
                """
                SELECT
                    EvidenceId,
                    LifecycleState
                FROM HLAS_Source_Evidence_Catalog;
                """;

            using SqliteDataReader lifecycleReader =
                lifecycleCommand.ExecuteReader();

            while (lifecycleReader.Read())
            {
                string evidenceId =
                    lifecycleReader.GetString(0);

                string lifecycleState =
                    lifecycleReader.GetString(1);

                bool hasSuccessor =
                    outgoingReplacement.ContainsKey(
                        evidenceId);

                bool isReplacementSuccessor =
                    incomingReplacement.Contains(
                        evidenceId);

                if (hasSuccessor &&
                    !string.Equals(
                        lifecycleState,
                        "Superseded",
                        StringComparison.Ordinal))
                {
                    return "Source Evidence with a successful outgoing REPLACE is not Superseded.";
                }

                if (isReplacementSuccessor &&
                    !hasSuccessor &&
                    !string.Equals(
                        lifecycleState,
                        "Active",
                        StringComparison.Ordinal))
                {
                    return "The terminal Source Evidence in a replacement chain is not Active.";
                }

                if (string.Equals(
                        lifecycleState,
                        "Superseded",
                        StringComparison.Ordinal) &&
                    !hasSuccessor)
                {
                    return "Superseded Source Evidence has no successful governed replacement successor.";
                }
            }

            return null;
        }

        private static string? FindMaintenanceFreezeMetadataProblem(
            SqliteConnection connection)
        {
            using (SqliteCommand maintenanceCommand =
                connection.CreateCommand())
            {
                maintenanceCommand.CommandText =
                    """
                    SELECT
                        maintenance.OperationId,
                        maintenance.FreezeId,
                        maintenance.PriorEvidenceId,
                        maintenance.ResultingEvidenceId,
                        maintenance.MaintenanceType,
                        operation.CompletedUtc,
                        operation.Outcome,
                        operation.OperationKind,
                        freeze.OperationId,
                        freeze.TargetEvidenceId,
                        freeze.FreezeType
                    FROM HLAS_Source_Evidence_Maintenance AS maintenance
                    LEFT JOIN HLAS_Governed_Operations AS operation
                        ON operation.OperationId = maintenance.OperationId
                    LEFT JOIN HLAS_Frozen_States AS freeze
                        ON freeze.FreezeId = maintenance.FreezeId
                    ORDER BY maintenance.MaintainedUtc,
                             maintenance.OperationId;
                    """;

                using SqliteDataReader reader =
                    maintenanceCommand.ExecuteReader();

                while (reader.Read())
                {
                    if (reader.IsDBNull(0) ||
                        reader.IsDBNull(1) ||
                        reader.IsDBNull(2) ||
                        reader.IsDBNull(3) ||
                        reader.IsDBNull(4) ||
                        reader.IsDBNull(5) ||
                        reader.IsDBNull(6) ||
                        reader.IsDBNull(8) ||
                        reader.IsDBNull(9) ||
                        reader.IsDBNull(10))
                    {
                        return "Governed maintenance does not resolve to complete operation and Pre-Change Freeze lineage.";
                    }

                    string operationId =
                        reader.GetString(0);

                    string priorEvidenceId =
                        reader.GetString(2);

                    string resultingEvidenceId =
                        reader.GetString(3);

                    string maintenanceType =
                        reader.GetString(4);

                    if (!string.Equals(
                            reader.GetString(6),
                            "SUCCESS",
                            StringComparison.Ordinal))
                    {
                        return "An accepted Source Evidence maintenance record is not backed by a successful governed operation.";
                    }

                    if (!reader.IsDBNull(7))
                    {
                        string operationKind =
                            reader.GetString(7);

                        string expectedOperationKind =
                            string.Equals(
                                maintenanceType,
                                "CORRECT",
                                StringComparison.Ordinal)
                                ? "SOURCE_EVIDENCE_CORRECT"
                                : "SOURCE_EVIDENCE_REPLACE";

                        if (!string.Equals(
                                operationKind,
                                expectedOperationKind,
                                StringComparison.Ordinal))
                        {
                            return "Source Evidence maintenance OperationKind conflicts with its governed maintenance type.";
                        }
                    }

                    if (!string.Equals(
                            reader.GetString(8),
                            operationId,
                            StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(
                            reader.GetString(9),
                            priorEvidenceId,
                            StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(
                            reader.GetString(10),
                            "PRE-CHANGE",
                            StringComparison.Ordinal))
                    {
                        return "Source Evidence maintenance Pre-Change Freeze lineage is inconsistent.";
                    }

                    using SqliteCommand metadataCommand =
                        connection.CreateCommand();

                    metadataCommand.CommandText =
                        """
                        SELECT
                            EvidenceId,
                            VersionNumber,
                            PriorMetadataVersionId
                        FROM HLAS_Source_Evidence_Metadata_Versions
                        WHERE OperationId = $operationId;
                        """;

                    metadataCommand.Parameters.AddWithValue(
                        "$operationId",
                        operationId);

                    using SqliteDataReader metadataReader =
                        metadataCommand.ExecuteReader();

                    if (!metadataReader.Read())
                    {
                        return "Source Evidence maintenance has no metadata version created by its governed operation.";
                    }

                    string metadataEvidenceId =
                        metadataReader.GetString(0);

                    long metadataVersionNumber =
                        metadataReader.GetInt64(1);

                    bool metadataHasPrior =
                        !metadataReader.IsDBNull(2);

                    if (!string.Equals(
                            metadataEvidenceId,
                            resultingEvidenceId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return "Source Evidence maintenance metadata lineage points to the wrong resulting EvidenceId.";
                    }

                    if (string.Equals(
                            maintenanceType,
                            "REPLACE",
                            StringComparison.Ordinal) &&
                        (metadataVersionNumber != 1 ||
                         metadataHasPrior))
                    {
                        return "REPLACE successor metadata does not begin as version 1 with no prior metadata version.";
                    }

                    if (metadataReader.Read())
                    {
                        return "A single Source Evidence maintenance operation created more than one metadata version.";
                    }
                }
            }

            using SqliteCommand metadataChainCommand =
                connection.CreateCommand();

            metadataChainCommand.CommandText =
                """
                SELECT
                    metadata.MetadataVersionId,
                    metadata.EvidenceId,
                    metadata.VersionNumber,
                    metadata.PriorMetadataVersionId,
                    metadata.OperationId,
                    maintenance.ResultingEvidenceId,
                    operation.CompletedUtc,
                    operation.Outcome
                FROM HLAS_Source_Evidence_Metadata_Versions AS metadata
                LEFT JOIN HLAS_Source_Evidence_Maintenance AS maintenance
                    ON maintenance.OperationId = metadata.OperationId
                LEFT JOIN HLAS_Governed_Operations AS operation
                    ON operation.OperationId = metadata.OperationId
                ORDER BY
                    metadata.EvidenceId,
                    metadata.VersionNumber;
                """;

            using SqliteDataReader metadataChainReader =
                metadataChainCommand.ExecuteReader();

            string? currentEvidenceId = null;
            string? priorMetadataVersionId = null;
            long expectedVersionNumber = 0;

            while (metadataChainReader.Read())
            {
                if (metadataChainReader.IsDBNull(0) ||
                    metadataChainReader.IsDBNull(1) ||
                    metadataChainReader.IsDBNull(2) ||
                    metadataChainReader.IsDBNull(4) ||
                    metadataChainReader.IsDBNull(5) ||
                    metadataChainReader.IsDBNull(6) ||
                    metadataChainReader.IsDBNull(7))
                {
                    return "Source Evidence metadata lineage contains an incomplete governed relationship.";
                }

                string metadataVersionId =
                    metadataChainReader.GetString(0);

                string evidenceId =
                    metadataChainReader.GetString(1);

                long versionNumber =
                    metadataChainReader.GetInt64(2);

                string operationId =
                    metadataChainReader.GetString(4);

                string maintenanceResultingEvidenceId =
                    metadataChainReader.GetString(5);

                if (!string.Equals(
                        metadataChainReader.GetString(7),
                        "SUCCESS",
                        StringComparison.Ordinal))
                {
                    return "Source Evidence metadata version is not backed by a successful governed maintenance operation.";
                }

                if (!string.Equals(
                        evidenceId,
                        maintenanceResultingEvidenceId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return "Source Evidence metadata version does not belong to the resulting evidence of its maintenance operation.";
                }

                if (!string.Equals(
                        currentEvidenceId,
                        evidenceId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    currentEvidenceId =
                        evidenceId;

                    priorMetadataVersionId =
                        null;

                    expectedVersionNumber =
                        1;
                }
                else
                {
                    expectedVersionNumber++;
                }

                if (versionNumber != expectedVersionNumber)
                {
                    return "Source Evidence metadata version numbers are not a contiguous 1..N sequence.";
                }

                if (versionNumber == 1)
                {
                    if (!metadataChainReader.IsDBNull(3))
                    {
                        return "Source Evidence metadata version 1 incorrectly points to a prior metadata version.";
                    }
                }
                else
                {
                    if (metadataChainReader.IsDBNull(3) ||
                        !string.Equals(
                            metadataChainReader.GetString(3),
                            priorMetadataVersionId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return "Source Evidence metadata prior-version lineage does not point to the immediately preceding version.";
                    }
                }

                priorMetadataVersionId =
                    metadataVersionId;

                _ = operationId;
            }

            return null;
        }
        private static string? FindSourceEvidenceRequirementLineageProblem(
    SqliteConnection connection)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
        SELECT
            revision.RequirementRevisionId,
            revision.RevisionNumber,
            revision.PriorRequirementRevisionId,
            revision.OperationId,
            operation.CompletedUtc,
            operation.Outcome
        FROM HLAS_Source_Evidence_Requirement_Revisions AS revision
        LEFT JOIN HLAS_Governed_Operations AS operation
            ON operation.OperationId = revision.OperationId
        ORDER BY revision.RevisionNumber;
        """;

            using SqliteDataReader reader =
                command.ExecuteReader();

            long expectedRevisionNumber = 1;
            string? priorRevisionId = null;

            while (reader.Read())
            {
                if (reader.IsDBNull(0) ||
                    reader.IsDBNull(1) ||
                    reader.IsDBNull(3) ||
                    reader.IsDBNull(4) ||
                    reader.IsDBNull(5))
                {
                    return "Source Evidence Requirement revision lineage contains an incomplete governed relationship.";
                }

                string revisionId =
                    reader.GetString(0);

                long revisionNumber =
                    reader.GetInt64(1);

                if (revisionNumber != expectedRevisionNumber)
                {
                    return "Source Evidence Requirement revision numbers are not a contiguous 1..N sequence.";
                }

                if (revisionNumber == 1)
                {
                    if (!reader.IsDBNull(2))
                    {
                        return "Source Evidence Requirement revision 1 incorrectly points to a prior revision.";
                    }
                }
                else
                {
                    if (reader.IsDBNull(2) ||
                        !string.Equals(
                            reader.GetString(2),
                            priorRevisionId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return "Source Evidence Requirement prior-revision lineage does not point to the immediately preceding revision.";
                    }
                }

                if (!string.Equals(
                        reader.GetString(5),
                        "SUCCESS",
                        StringComparison.Ordinal))
                {
                    return "Source Evidence Requirement revision is not backed by a completed successful governed operation.";
                }

                priorRevisionId =
                    revisionId;

                expectedRevisionNumber++;
            }

            return null;
        }
        private static string? FindProjectJmfLineageProblem(
    SqliteConnection connection)
        {
            List<(
                string RevisionId,
                long RevisionNumber,
                string? PriorRevisionId,
                string ApprovedUtc,
                string? CompletedUtc,
                string? Outcome)> revisions =
                [];

            using (SqliteCommand revisionCommand =
                connection.CreateCommand())
            {
                revisionCommand.CommandText =
                    """
            SELECT
                revision.ProjectJmfRevisionId,
                revision.RevisionNumber,
                revision.PriorProjectJmfRevisionId,
                revision.ApprovedUtc,
                operation.CompletedUtc,
                operation.Outcome
            FROM HLAS_Project_JMF_Revisions AS revision
            LEFT JOIN HLAS_Governed_Operations AS operation
                ON operation.OperationId = revision.OperationId
            ORDER BY revision.RevisionNumber;
            """;

                using SqliteDataReader reader =
                    revisionCommand.ExecuteReader();

                while (reader.Read())
                {
                    revisions.Add(
                        (
                            reader.GetString(0),
                            reader.GetInt64(1),
                            reader.IsDBNull(2)
                                ? null
                                : reader.GetString(2),
                            reader.GetString(3),
                            reader.IsDBNull(4)
                                ? null
                                : reader.GetString(4),
                            reader.IsDBNull(5)
                                ? null
                                : reader.GetString(5)
                        ));
                }
            }

            HashSet<string> requiredFieldIds =
            [
                "PJT-001",
        "PJT-002",
        "PJT-003",
        "PJT-004",
        "PJT-005",
        "PJT-006",
        "PJT-007",
        "JMF-001",
        "JMF-002",
        "JMF-003",
        "JMF-004",
        "JMF-005",
        "JMF-006",
        "JMF-007",
        "JMF-008",
        "JMF-009",
        "JMF-010",
        "JMF-011",
        "JMF-012",
        "JMF-013"
            ];

            long expectedRevisionNumber = 1;
            string? priorRevisionId = null;

            foreach (var revision in revisions)
            {
                if (revision.RevisionNumber != expectedRevisionNumber)
                {
                    return "Project/JMF revision numbers are not a contiguous 1..N sequence.";
                }

                if (revision.RevisionNumber == 1)
                {
                    if (revision.PriorRevisionId is not null)
                    {
                        return "Project/JMF revision 1 incorrectly points to a prior revision.";
                    }
                }
                else
                {
                    if (!string.Equals(
                            revision.PriorRevisionId,
                            priorRevisionId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return "Project/JMF prior-revision lineage does not point to the immediately preceding revision.";
                    }
                }

                if (revision.CompletedUtc is null ||
                    !string.Equals(
                        revision.Outcome,
                        "SUCCESS",
                        StringComparison.Ordinal))
                {
                    return "Project/JMF revision is not backed by a completed successful governed operation.";
                }

                if (!DateTimeOffset.TryParse(
                        revision.ApprovedUtc,
                        out _))
                {
                    return "Project/JMF revision contains an invalid governed approval timestamp.";
                }

                using SqliteCommand fieldCommand =
                    connection.CreateCommand();

                fieldCommand.CommandText =
                    """
            SELECT
                FieldId,
                FieldValue,
                SourceEvidenceId
            FROM HLAS_Project_JMF_Revision_Fields
            WHERE ProjectJmfRevisionId = $revisionId;
            """;

                fieldCommand.Parameters.AddWithValue(
                    "$revisionId",
                    revision.RevisionId);

                using SqliteDataReader fieldReader =
                    fieldCommand.ExecuteReader();

                HashSet<string> foundFieldIds =
                    new(StringComparer.Ordinal);

                while (fieldReader.Read())
                {
                    string fieldId =
                        fieldReader.GetString(0);

                    if (!requiredFieldIds.Contains(fieldId) ||
                        !foundFieldIds.Add(fieldId))
                    {
                        return "A historical Project/JMF revision contains an unexpected or duplicate governed field.";
                    }

                    if (fieldReader.IsDBNull(1) ||
                        fieldReader.IsDBNull(2) ||
                        string.IsNullOrWhiteSpace(
                            fieldReader.GetString(2)))
                    {
                        return "A historical Project/JMF revision contains incomplete field/source lineage.";
                    }
                }

                if (foundFieldIds.Count != requiredFieldIds.Count ||
                    !foundFieldIds.SetEquals(requiredFieldIds))
                {
                    return "A historical Project/JMF revision does not contain the complete governed 20-field set.";
                }

                priorRevisionId =
                    revision.RevisionId;

                expectedRevisionNumber++;
            }

            return null;
        }
        private static string? FindProjectJmfReviewLineageProblem(
    SqliteConnection connection)
        {
            using SqliteCommand caseCommand =
                connection.CreateCommand();

            caseCommand.CommandText =
                """
        SELECT
            reviewCase.ReviewCaseId,
            reviewCase.SourceEvidenceId,
            reviewCase.BaselineProjectJmfRevisionId,
            reviewCase.OpenedOperationId,
            reviewCase.ResolvedOperationId,
            openedOperation.Outcome,
            resolvedOperation.Outcome
        FROM HLAS_Project_JMF_Review_Cases AS reviewCase
        LEFT JOIN HLAS_Governed_Operations AS openedOperation
            ON openedOperation.OperationId = reviewCase.OpenedOperationId
        LEFT JOIN HLAS_Governed_Operations AS resolvedOperation
            ON resolvedOperation.OperationId = reviewCase.ResolvedOperationId;
        """;

            using SqliteDataReader caseReader =
                caseCommand.ExecuteReader();

            while (caseReader.Read())
            {
                string reviewCaseId =
                    caseReader.GetString(0);

                if (caseReader.IsDBNull(5) ||
                    !string.Equals(
                        caseReader.GetString(5),
                        "SUCCESS",
                        StringComparison.Ordinal))
                {
                    return "Project/JMF review case is not backed by a successful opening operation.";
                }

                bool isResolved =
                    !caseReader.IsDBNull(4);

                if (isResolved &&
                    (caseReader.IsDBNull(6) ||
                     !string.Equals(
                         caseReader.GetString(6),
                         "SUCCESS",
                         StringComparison.Ordinal)))
                {
                    return "Resolved Project/JMF review case is not backed by a successful resolution operation.";
                }

                using SqliteCommand itemCommand =
                    connection.CreateCommand();

                itemCommand.CommandText =
                    """
            SELECT
                Decision,
                DecisionReason,
                DecidedOperationId,
                DecidedUtc
            FROM HLAS_Project_JMF_Review_Items
            WHERE ReviewCaseId = $reviewCaseId;
            """;

                itemCommand.Parameters.AddWithValue(
                    "$reviewCaseId",
                    reviewCaseId);

                using SqliteDataReader itemReader =
                    itemCommand.ExecuteReader();

                bool foundItem = false;

                while (itemReader.Read())
                {
                    foundItem = true;

                    if (isResolved &&
                        (itemReader.IsDBNull(0) ||
                         itemReader.IsDBNull(1) ||
                         itemReader.IsDBNull(2) ||
                         itemReader.IsDBNull(3)))
                    {
                        return "Resolved Project/JMF review case contains an incomplete governed review decision.";
                    }
                }
                if (isResolved)
                {
                    using SqliteCommand acceptedNewCommand =
                        connection.CreateCommand();

                    acceptedNewCommand.CommandText =
                        """
        SELECT COUNT(*)
        FROM HLAS_Project_JMF_Review_Items
        WHERE
            ReviewCaseId = $reviewCaseId
            AND Decision = 'ACCEPT NEW';
        """;

                    acceptedNewCommand.Parameters.AddWithValue(
                        "$reviewCaseId",
                        reviewCaseId);

                    long acceptedNewCount =
                        Convert.ToInt64(
                            acceptedNewCommand.ExecuteScalar());

                    string resolvedOperationId =
                        caseReader.GetString(4);

                    using SqliteCommand resultingRevisionCommand =
                        connection.CreateCommand();

                    resultingRevisionCommand.CommandText =
                        """
        SELECT
            ProjectJmfRevisionId,
            PriorProjectJmfRevisionId,
            SourceEvidenceId
        FROM HLAS_Project_JMF_Revisions
        WHERE OperationId = $resolvedOperationId;
        """;

                    resultingRevisionCommand.Parameters.AddWithValue(
                        "$resolvedOperationId",
                        resolvedOperationId);

                    if (acceptedNewCount == 0)
                    {
                        using SqliteDataReader resultingRevisionReader =
                            resultingRevisionCommand.ExecuteReader();

                        if (resultingRevisionReader.Read())
                        {
                            return "All KEEP CURRENT Project/JMF review created an unexpected resulting revision.";
                        }
                    }
                    else
                    {
                        string resultingRevisionId;

                        using (SqliteDataReader resultingRevisionReader =
                            resultingRevisionCommand.ExecuteReader())
                        {
                            if (!resultingRevisionReader.Read())
                            {
                                return "Project/JMF review accepted new values but has no resulting governed Project/JMF revision.";
                            }

                            resultingRevisionId =
                                resultingRevisionReader.GetString(0);

                            if (resultingRevisionReader.IsDBNull(1) ||
                                !string.Equals(
                                    resultingRevisionReader.GetString(1),
                                    caseReader.GetString(2),
                                    StringComparison.OrdinalIgnoreCase))
                            {
                                return "Project/JMF review resulting revision does not point to the review baseline revision.";
                            }

                            if (resultingRevisionReader.IsDBNull(2) ||
                                !string.Equals(
                                    resultingRevisionReader.GetString(2),
                                    caseReader.GetString(1),
                                    StringComparison.OrdinalIgnoreCase))
                            {
                                return "Project/JMF review resulting revision does not identify the review official Source Evidence.";
                            }

                            if (resultingRevisionReader.Read())
                            {
                                return "Project/JMF review resolution operation created more than one resulting revision.";
                            }
                        }

                        string? resultingRevisionProblem =
                            FindProjectJmfReviewResultingRevisionProblem(
                                connection,
                                reviewCaseId,
                                caseReader.GetString(1),
                                caseReader.GetString(2),
                                resultingRevisionId);

                        if (resultingRevisionProblem is not null)
                        {
                            return resultingRevisionProblem;
                        }
                    }

                    
                    }
                
                if (!foundItem)
                {
                    return "Project/JMF review case contains no governed review items.";
                }
            }

            return null;
        }
        private static string? FindProjectJmfReviewResultingRevisionProblem(
    SqliteConnection connection,
    string reviewCaseId,
    string reviewSourceEvidenceId,
    string baselineRevisionId,
    string resultingRevisionId)
        {
            Dictionary<string, (string Value, string SourceEvidenceId)> baselineFields =
                ReadProjectJmfRevisionFields(
                    connection,
                    baselineRevisionId);

            Dictionary<string, (string Value, string SourceEvidenceId)> resultingFields =
                ReadProjectJmfRevisionFields(
                    connection,
                    resultingRevisionId);

            HashSet<string> reviewedFieldIds =
                new(StringComparer.Ordinal);

            using SqliteCommand reviewItemCommand =
                connection.CreateCommand();

            reviewItemCommand.CommandText =
                """
        SELECT
            FieldId,
            CurrentApprovedValue,
            NewOfficialSourceValue,
            Decision
        FROM HLAS_Project_JMF_Review_Items
        WHERE ReviewCaseId = $reviewCaseId;
        """;

            reviewItemCommand.Parameters.AddWithValue(
                "$reviewCaseId",
                reviewCaseId);

            using SqliteDataReader reviewItemReader =
                reviewItemCommand.ExecuteReader();

            while (reviewItemReader.Read())
            {
                string fieldId =
                    reviewItemReader.GetString(0);

                string currentApprovedValue =
                    reviewItemReader.GetString(1);

                string newOfficialSourceValue =
                    reviewItemReader.GetString(2);

                string decision =
                    reviewItemReader.GetString(3);

                reviewedFieldIds.Add(
                    fieldId);

                if (!baselineFields.TryGetValue(
                        fieldId,
                        out var baselineField))
                {
                    return "Project/JMF review item does not resolve to its baseline revision field.";
                }

                if (!string.Equals(
                        currentApprovedValue,
                        baselineField.Value,
                        StringComparison.Ordinal))
                {
                    return "Project/JMF review item current value does not match its baseline revision.";
                }

                if (!resultingFields.TryGetValue(
                        fieldId,
                        out var resultingField))
                {
                    return "Project/JMF resulting revision is missing a reviewed field.";
                }

                if (string.Equals(
                        decision,
                        "KEEP CURRENT",
                        StringComparison.Ordinal))
                {
                    if (!string.Equals(
                            resultingField.Value,
                            baselineField.Value,
                            StringComparison.Ordinal) ||
                        !string.Equals(
                            resultingField.SourceEvidenceId,
                            baselineField.SourceEvidenceId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return "KEEP CURRENT Project/JMF review field was not carried forward unchanged.";
                    }
                }
                else if (string.Equals(
                             decision,
                             "ACCEPT NEW",
                             StringComparison.Ordinal))
                {
                    if (!string.Equals(
                            resultingField.Value,
                            newOfficialSourceValue,
                            StringComparison.Ordinal) ||
                        !string.Equals(
                            resultingField.SourceEvidenceId,
                            reviewSourceEvidenceId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return "ACCEPT NEW Project/JMF review field was not applied from the official review source.";
                    }
                }
                else
                {
                    return "Project/JMF review item contains an unsupported governed decision.";
                }
            }

            foreach (var baselineField in baselineFields)
            {
                if (reviewedFieldIds.Contains(
                        baselineField.Key))
                {
                    continue;
                }

                if (!resultingFields.TryGetValue(
                        baselineField.Key,
                        out var resultingField) ||
                    !string.Equals(
                        resultingField.Value,
                        baselineField.Value.Value,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        resultingField.SourceEvidenceId,
                        baselineField.Value.SourceEvidenceId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return "Project/JMF resulting revision did not carry forward an unchanged baseline field.";
                }
            }

            return null;
        }

        private static Dictionary<string, (string Value, string SourceEvidenceId)>
            ReadProjectJmfRevisionFields(
                SqliteConnection connection,
                string revisionId)
        {
            Dictionary<string, (string Value, string SourceEvidenceId)> fields =
                new(StringComparer.Ordinal);

            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                """
        SELECT
            FieldId,
            FieldValue,
            SourceEvidenceId
        FROM HLAS_Project_JMF_Revision_Fields
        WHERE ProjectJmfRevisionId = $revisionId;
        """;

            command.Parameters.AddWithValue(
                "$revisionId",
                revisionId);

            using SqliteDataReader reader =
                command.ExecuteReader();

            while (reader.Read())
            {
                fields.Add(
                    reader.GetString(0),
                    (
                        reader.GetString(1),
                        reader.GetString(2)
                    ));
            }

            return fields;
        }
        private static string? FindTemporalOrderingProblem(
    SqliteConnection connection)
        {
            using (SqliteCommand metadataCommand =
                connection.CreateCommand())
            {
                metadataCommand.CommandText =
                    """
            SELECT
                later.VersionedUtc,
                prior.VersionedUtc
            FROM HLAS_Source_Evidence_Metadata_Versions AS later
            INNER JOIN HLAS_Source_Evidence_Metadata_Versions AS prior
                ON prior.MetadataVersionId = later.PriorMetadataVersionId;
            """;

                using SqliteDataReader reader =
                    metadataCommand.ExecuteReader();

                while (reader.Read())
                {
                    if (!DateTimeOffset.TryParse(
                            reader.GetString(0),
                            out DateTimeOffset laterUtc) ||
                        !DateTimeOffset.TryParse(
                            reader.GetString(1),
                            out DateTimeOffset priorUtc))
                    {
                        return "Source Evidence metadata lineage contains an invalid governed timestamp.";
                    }

                    if (priorUtc > laterUtc)
                    {
                        return "Source Evidence metadata lineage runs backward in governed time.";
                    }
                }
            }

            using (SqliteCommand requirementCommand =
                connection.CreateCommand())
            {
                requirementCommand.CommandText =
                    """
            SELECT
                later.ApprovedUtc,
                prior.ApprovedUtc
            FROM HLAS_Source_Evidence_Requirement_Revisions AS later
            INNER JOIN HLAS_Source_Evidence_Requirement_Revisions AS prior
                ON prior.RequirementRevisionId =
                   later.PriorRequirementRevisionId;
            """;

                using SqliteDataReader reader =
                    requirementCommand.ExecuteReader();

                while (reader.Read())
                {
                    if (!DateTimeOffset.TryParse(
                            reader.GetString(0),
                            out DateTimeOffset laterUtc) ||
                        !DateTimeOffset.TryParse(
                            reader.GetString(1),
                            out DateTimeOffset priorUtc))
                    {
                        return "Source Evidence Requirement lineage contains an invalid governed timestamp.";
                    }

                    if (priorUtc > laterUtc)
                    {
                        return "Source Evidence Requirement lineage runs backward in governed time.";
                    }
                }
            }

            using (SqliteCommand projectJmfCommand =
                connection.CreateCommand())
            {
                projectJmfCommand.CommandText =
                    """
            SELECT
                later.ApprovedUtc,
                prior.ApprovedUtc
            FROM HLAS_Project_JMF_Revisions AS later
            INNER JOIN HLAS_Project_JMF_Revisions AS prior
                ON prior.ProjectJmfRevisionId =
                   later.PriorProjectJmfRevisionId;
            """;

                using SqliteDataReader reader =
                    projectJmfCommand.ExecuteReader();

                while (reader.Read())
                {
                    if (!DateTimeOffset.TryParse(
                            reader.GetString(0),
                            out DateTimeOffset laterUtc) ||
                        !DateTimeOffset.TryParse(
                            reader.GetString(1),
                            out DateTimeOffset priorUtc))
                    {
                        return "Project/JMF revision lineage contains an invalid governed timestamp.";
                    }

                    if (priorUtc > laterUtc)
                    {
                        return "Project/JMF revision lineage runs backward in governed time.";
                    }
                }
            }

            using (SqliteCommand reviewCommand =
                connection.CreateCommand())
            {
                reviewCommand.CommandText =
                    """
            SELECT
                OpenedUtc,
                ResolvedUtc
            FROM HLAS_Project_JMF_Review_Cases
            WHERE ResolvedUtc IS NOT NULL;
            """;

                using SqliteDataReader reader =
                    reviewCommand.ExecuteReader();

                while (reader.Read())
                {
                    if (!DateTimeOffset.TryParse(
                            reader.GetString(0),
                            out DateTimeOffset openedUtc) ||
                        !DateTimeOffset.TryParse(
                            reader.GetString(1),
                            out DateTimeOffset resolvedUtc))
                    {
                        return "Project/JMF review lineage contains an invalid governed timestamp.";
                    }

                    if (openedUtc > resolvedUtc)
                    {
                        return "Project/JMF review lineage resolves before it was opened.";
                    }
                }
            }

            using (SqliteCommand freezeMaintenanceCommand =
                connection.CreateCommand())
            {
                freezeMaintenanceCommand.CommandText =
                    """
            SELECT
                freeze.FrozenUtc,
                maintenance.MaintainedUtc
            FROM HLAS_Source_Evidence_Maintenance AS maintenance
            INNER JOIN HLAS_Frozen_States AS freeze
                ON freeze.FreezeId = maintenance.FreezeId;
            """;

                using SqliteDataReader reader =
                    freezeMaintenanceCommand.ExecuteReader();

                while (reader.Read())
                {
                    if (!DateTimeOffset.TryParse(
                            reader.GetString(0),
                            out DateTimeOffset frozenUtc) ||
                        !DateTimeOffset.TryParse(
                            reader.GetString(1),
                            out DateTimeOffset maintainedUtc))
                    {
                        return "Source Evidence maintenance lineage contains an invalid governed timestamp.";
                    }

                    if (frozenUtc > maintainedUtc)
                    {
                        return "Source Evidence maintenance occurred before its required Pre-Change Freeze.";
                    }
                }
            }

            using SqliteCommand resolutionCommand =
                connection.CreateCommand();

            resolutionCommand.CommandText =
                """
        SELECT
            maintenance.MaintainedUtc,
            resolution.ResolvedUtc
        FROM HLAS_Source_Evidence_Maintenance_Resolutions AS resolution
        INNER JOIN HLAS_Source_Evidence_Maintenance AS maintenance
            ON maintenance.OperationId =
               resolution.MaintenanceOperationId;
        """;

            using SqliteDataReader resolutionReader =
                resolutionCommand.ExecuteReader();

            while (resolutionReader.Read())
            {
                if (!DateTimeOffset.TryParse(
                        resolutionReader.GetString(0),
                        out DateTimeOffset maintainedUtc) ||
                    !DateTimeOffset.TryParse(
                        resolutionReader.GetString(1),
                        out DateTimeOffset resolvedUtc))
                {
                    return "Source Evidence maintenance resolution contains an invalid governed timestamp.";
                }

                if (maintainedUtc > resolvedUtc)
                {
                    return "Source Evidence maintenance was resolved before the maintenance occurred.";
                }
            }

            return null;
        }
        private static ReadinessCheckItemRecord Blocked(
            ReadinessCheckId readinessCheckId,
            string detail)
        {
            return new ReadinessCheckItemRecord(
                readinessCheckId,
                6,
                ReadinessGateCode.Lineage,
                ReadinessGateStatus.Blocked,
                detail);
        }
    }
}