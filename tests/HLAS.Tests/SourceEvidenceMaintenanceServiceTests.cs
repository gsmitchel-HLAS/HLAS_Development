using System;
using System.Collections.Generic;
using HLAS.Application;
using HLAS.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class SourceEvidenceMaintenanceServiceTests
    {
        [TestMethod]
        public void Correct_SeniorAuthorized_DelegatesAuthorizedIdentity()
        {
            UserId userId = UserId.CreateNew();

            AuthenticationService authenticationService =
                new(new FakeAuthenticationGateway(
                    AuthenticationGatewayResult.Succeeded(userId)));

            AuthenticatedIdentity authenticatedIdentity =
                authenticationService.Authenticate();

            ProjectAuthorizationService authorizationService =
                new(new FakeProjectAuthorizationGateway(
                    ProjectAuthorizationGatewayResult.Authorized(
                        ProjectRole.Senior)));

            EvidenceId evidenceId =
                EvidenceId.CreateNew();

            IReadOnlyList<SourceEvidenceCorrectionChange> changes =
                new List<SourceEvidenceCorrectionChange>
                {
                    new(
                        "OriginalFileName",
                        "old.pdf",
                        "corrected.pdf")
                };

            FakeMaintenanceGateway gateway =
                new();

            SourceEvidenceMaintenanceService service =
                new(
                    authorizationService,
                    gateway);

            _ = service.Correct(
                @"C:\DevelopmentalProject",
                authenticatedIdentity,
                SeriesId.V,
                evidenceId,
                changes);

            Assert.IsTrue(gateway.CorrectCalled);
            Assert.AreEqual(
                userId,
                gateway.ReceivedAuthorizedIdentity!.UserId);
            Assert.AreEqual(
                ProjectRole.Senior,
                gateway.ReceivedAuthorizedIdentity.ProjectRole);
            Assert.AreEqual(
                evidenceId,
                gateway.ReceivedEvidenceId);
        }
        [TestMethod]
        public void Correct_TechnicianAuthorized_SafeStopsBeforeGateway()
        {
            UserId userId = UserId.CreateNew();

            AuthenticationService authenticationService =
                new(new FakeAuthenticationGateway(
                    AuthenticationGatewayResult.Succeeded(userId)));

            AuthenticatedIdentity authenticatedIdentity =
                authenticationService.Authenticate();

            ProjectAuthorizationService authorizationService =
                new(new FakeProjectAuthorizationGateway(
                    ProjectAuthorizationGatewayResult.Authorized(
                        ProjectRole.Technician)));

            FakeMaintenanceGateway gateway =
                new();

            SourceEvidenceMaintenanceService service =
                new(
                    authorizationService,
                    gateway);

            InvalidOperationException exception =
                Assert.ThrowsExactly<InvalidOperationException>(
                    () => service.Correct(
                        @"C:\DevelopmentalProject",
                        authenticatedIdentity,
                        SeriesId.V,
                        EvidenceId.CreateNew(),
                        new List<SourceEvidenceCorrectionChange>
                        {
                    new(
                        "OriginalFileName",
                        "old.pdf",
                        "corrected.pdf")
                        }));

            StringAssert.Contains(
                exception.Message,
                "SAFE-STOP");

            Assert.IsFalse(
                gateway.CorrectCalled);
        }
        [TestMethod]
        public void Correct_AdminAuthorized_DelegatesAuthorizedIdentity()
        {
            UserId userId = UserId.CreateNew();

            AuthenticationService authenticationService =
                new(new FakeAuthenticationGateway(
                    AuthenticationGatewayResult.Succeeded(userId)));

            AuthenticatedIdentity authenticatedIdentity =
                authenticationService.Authenticate();

            ProjectAuthorizationService authorizationService =
                new(new FakeProjectAuthorizationGateway(
                    ProjectAuthorizationGatewayResult.Authorized(
                        ProjectRole.Admin)));

            FakeMaintenanceGateway gateway =
                new();

            SourceEvidenceMaintenanceService service =
                new(
                    authorizationService,
                    gateway);

            EvidenceId evidenceId =
                EvidenceId.CreateNew();

            _ = service.Correct(
                @"C:\DevelopmentalProject",
                authenticatedIdentity,
                SeriesId.V,
                evidenceId,
                new List<SourceEvidenceCorrectionChange>
                {
            new(
                "OriginalFileName",
                "old.pdf",
                "corrected.pdf")
                });

            Assert.IsTrue(gateway.CorrectCalled);
            Assert.AreEqual(
                ProjectRole.Admin,
                gateway.ReceivedAuthorizedIdentity!.ProjectRole);
        }
        [TestMethod]
        public void Correct_NonVSeries_SafeStopsBeforeGateway()
        {
            UserId userId = UserId.CreateNew();

            AuthenticationService authenticationService =
                new(new FakeAuthenticationGateway(
                    AuthenticationGatewayResult.Succeeded(userId)));

            AuthenticatedIdentity authenticatedIdentity =
                authenticationService.Authenticate();

            ProjectAuthorizationService authorizationService =
                new(new FakeProjectAuthorizationGateway(
                    ProjectAuthorizationGatewayResult.Authorized(
                        ProjectRole.Admin)));

            FakeMaintenanceGateway gateway =
                new();

            SourceEvidenceMaintenanceService service =
                new(
                    authorizationService,
                    gateway);

            InvalidOperationException exception =
                Assert.ThrowsExactly<InvalidOperationException>(
                    () => service.Correct(
                        @"C:\DevelopmentalProject",
                        authenticatedIdentity,
                        SeriesId.A,
                        EvidenceId.CreateNew(),
                        new List<SourceEvidenceCorrectionChange>
                        {
                    new(
                        "OriginalFileName",
                        "old.pdf",
                        "corrected.pdf")
                        }));

            StringAssert.Contains(
                exception.Message,
                "SAFE-STOP");

            Assert.IsFalse(
                gateway.CorrectCalled);
        }
        [TestMethod]
        public void Correct_ProjectAuthorizationRejected_SafeStopsBeforeGateway()
        {
            UserId userId = UserId.CreateNew();

            AuthenticationService authenticationService =
                new(new FakeAuthenticationGateway(
                    AuthenticationGatewayResult.Succeeded(userId)));

            AuthenticatedIdentity authenticatedIdentity =
                authenticationService.Authenticate();

            ProjectAuthorizationService authorizationService =
                new(new FakeProjectAuthorizationGateway(
                    ProjectAuthorizationGatewayResult.Rejected(
                        "User is not authorized for this project.")));

            FakeMaintenanceGateway gateway =
                new();

            SourceEvidenceMaintenanceService service =
                new(
                    authorizationService,
                    gateway);

            InvalidOperationException exception =
                Assert.ThrowsExactly<InvalidOperationException>(
                    () => service.Correct(
                        @"C:\DevelopmentalProject",
                        authenticatedIdentity,
                        SeriesId.V,
                        EvidenceId.CreateNew(),
                        new List<SourceEvidenceCorrectionChange>
                        {
                    new(
                        "OriginalFileName",
                        "old.pdf",
                        "corrected.pdf")
                        }));

            StringAssert.Contains(
                exception.Message,
                "SAFE-STOP");

            Assert.IsFalse(
                gateway.CorrectCalled);
        }
        [TestMethod]
        public void Replace_AdminAuthorized_DelegatesAuthorizedIdentityAndReplacement()
        {
            UserId userId = UserId.CreateNew();

            AuthenticationService authenticationService =
                new(new FakeAuthenticationGateway(
                    AuthenticationGatewayResult.Succeeded(userId)));

            AuthenticatedIdentity authenticatedIdentity =
                authenticationService.Authenticate();

            ProjectAuthorizationService authorizationService =
                new(new FakeProjectAuthorizationGateway(
                    ProjectAuthorizationGatewayResult.Authorized(
                        ProjectRole.Admin)));

            FakeMaintenanceGateway gateway =
                new();

            SourceEvidenceMaintenanceService service =
                new(
                    authorizationService,
                    gateway);

            EvidenceId priorEvidenceId =
                EvidenceId.CreateNew();

            string replacementPath =
                @"C:\Replacement\replacement.pdf";

            _ = service.Replace(
                @"C:\DevelopmentalProject",
                authenticatedIdentity,
                SeriesId.V,
                priorEvidenceId,
                replacementPath);

            Assert.IsTrue(gateway.ReplaceCalled);

            Assert.AreEqual(
                ProjectRole.Admin,
                gateway.ReceivedAuthorizedIdentity!.ProjectRole);

            Assert.AreEqual(
                priorEvidenceId,
                gateway.ReceivedPriorEvidenceId);

            Assert.AreEqual(
                replacementPath,
                gateway.ReceivedReplacementSourceFilePath);
        }
        private sealed class FakeAuthenticationGateway
            : IAuthenticationGateway
        {
            private readonly AuthenticationGatewayResult _result;

            public FakeAuthenticationGateway(
                AuthenticationGatewayResult result)
            {
                _result = result;
            }

            public AuthenticationGatewayResult Authenticate()
            {
                return _result;
            }
        }

        private sealed class FakeProjectAuthorizationGateway
            : IProjectAuthorizationGateway
        {
            private readonly ProjectAuthorizationGatewayResult _result;

            public FakeProjectAuthorizationGateway(
                ProjectAuthorizationGatewayResult result)
            {
                _result = result;
            }

            public ProjectAuthorizationGatewayResult Resolve(
                string projectRoot,
                UserId userId)
            {
                return _result;
            }
        }

        private sealed class FakeMaintenanceGateway
            : ISourceEvidenceMaintenanceGateway
        {
            public bool CorrectCalled { get; private set; }
            public bool ReplaceCalled { get; private set; }

            public EvidenceId?
                ReceivedPriorEvidenceId
            { get; private set; }

            public string?
                ReceivedReplacementSourceFilePath
            { get; private set; }
            public AuthorizedProjectIdentity?
                ReceivedAuthorizedIdentity
            { get; private set; }

            public EvidenceId?
                ReceivedEvidenceId
            { get; private set; }

            public SourceEvidenceMaintenanceRecord Correct(
                string projectRoot,
                AuthorizedProjectIdentity authorizedIdentity,
                EvidenceId evidenceId,
                IReadOnlyList<SourceEvidenceCorrectionChange> changes)
            {
                CorrectCalled = true;
                ReceivedAuthorizedIdentity = authorizedIdentity;
                ReceivedEvidenceId = evidenceId;

                return new SourceEvidenceMaintenanceRecord(
                    OperationId.CreateNew(),
                    FreezeId.CreateNew(),
                    evidenceId,
                    evidenceId,
                    SourceEvidenceMaintenanceRecord.CorrectMaintenanceType,
                    GovernedTimestamp.CreateNow());
            }

            public SourceEvidenceMaintenanceRecord Replace(
    string projectRoot,
    AuthorizedProjectIdentity authorizedIdentity,
    EvidenceId priorEvidenceId,
    string selectedReplacementSourceFilePath)
            {
                ReplaceCalled = true;
                ReceivedAuthorizedIdentity = authorizedIdentity;
                ReceivedPriorEvidenceId = priorEvidenceId;
                ReceivedReplacementSourceFilePath =
                    selectedReplacementSourceFilePath;
                EvidenceId resultingEvidenceId =
                    EvidenceId.CreateNew();

                return new SourceEvidenceMaintenanceRecord(
                    OperationId.CreateNew(),
                    FreezeId.CreateNew(),
                    priorEvidenceId,
                    resultingEvidenceId,
                    SourceEvidenceMaintenanceRecord.ReplaceMaintenanceType,
                    GovernedTimestamp.CreateNow());
            }
        }
    }
}