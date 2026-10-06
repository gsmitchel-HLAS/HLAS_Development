using System.Collections.Generic;
using HLAS.Application;
using HLAS.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class ReleaseServiceTests
    {
        [TestMethod]
        public void RequestRelease_AdminReady_DelegatesFreshReadinessBasis()
        {
            UserId userId = UserId.CreateNew();

            AuthenticatedIdentity authenticatedIdentity =
                Authenticate(userId);

            ProjectAuthorizationService authorizationService =
                new(new FakeProjectAuthorizationGateway(
                    ProjectAuthorizationGatewayResult.Authorized(
                        ProjectRole.Admin)));

            FakeReadinessGateway readinessGateway =
                new(ReadinessStatus.Ready);

            FakeReleaseGateway releaseGateway =
                new();

            ReleaseService service =
                new(
                    authorizationService,
                    readinessGateway,
                    releaseGateway);

            _ = service.RequestRelease(
                @"C:\DevelopmentalProject",
                authenticatedIdentity,
                SeriesId.V);

            Assert.IsTrue(readinessGateway.EvaluateCalled);
            Assert.IsTrue(releaseGateway.RequestReleaseCalled);

            Assert.AreEqual(
                readinessGateway.LastReadinessCheckId,
                releaseGateway.ReceivedReadinessCheckId);

            Assert.AreEqual(
                userId,
                releaseGateway.ReceivedAuthorizedIdentity!.UserId);

            Assert.AreEqual(
                ProjectRole.Admin,
                releaseGateway.ReceivedAuthorizedIdentity.ProjectRole);
        }
        [TestMethod]
        public void RequestRelease_SeniorReady_DelegatesRelease()
        {
            AuthenticatedIdentity authenticatedIdentity =
                Authenticate(UserId.CreateNew());

            ProjectAuthorizationService authorizationService =
                new(new FakeProjectAuthorizationGateway(
                    ProjectAuthorizationGatewayResult.Authorized(
                        ProjectRole.Senior)));

            FakeReadinessGateway readinessGateway =
                new(ReadinessStatus.Ready);

            FakeReleaseGateway releaseGateway =
                new();

            ReleaseService service =
                new(
                    authorizationService,
                    readinessGateway,
                    releaseGateway);

            _ = service.RequestRelease(
                @"C:\DevelopmentalProject",
                authenticatedIdentity,
                SeriesId.V);

            Assert.IsTrue(readinessGateway.EvaluateCalled);
            Assert.IsTrue(releaseGateway.RequestReleaseCalled);
        }

        [TestMethod]
        public void RequestRelease_Technician_SafeStopsBeforeReadinessAndRelease()
        {
            AuthenticatedIdentity authenticatedIdentity =
                Authenticate(UserId.CreateNew());

            ProjectAuthorizationService authorizationService =
                new(new FakeProjectAuthorizationGateway(
                    ProjectAuthorizationGatewayResult.Authorized(
                        ProjectRole.Technician)));

            FakeReadinessGateway readinessGateway =
                new(ReadinessStatus.Ready);

            FakeReleaseGateway releaseGateway =
                new();

            ReleaseService service =
                new(
                    authorizationService,
                    readinessGateway,
                    releaseGateway);

            InvalidOperationException exception =
                Assert.ThrowsExactly<InvalidOperationException>(
                    () => service.RequestRelease(
                        @"C:\DevelopmentalProject",
                        authenticatedIdentity,
                        SeriesId.V));

            StringAssert.Contains(
                exception.Message,
                "SAFE-STOP");

            Assert.IsFalse(readinessGateway.EvaluateCalled);
            Assert.IsFalse(releaseGateway.RequestReleaseCalled);
        }
        [TestMethod]
        public void RequestRelease_ProjectUnauthorized_SafeStopsBeforeReadinessAndRelease()
        {
            AuthenticatedIdentity authenticatedIdentity =
                Authenticate(UserId.CreateNew());

            ProjectAuthorizationService authorizationService =
                new(new FakeProjectAuthorizationGateway(
                    ProjectAuthorizationGatewayResult.Rejected(
                        "User is not authorized for this project.")));

            FakeReadinessGateway readinessGateway =
                new(ReadinessStatus.Ready);

            FakeReleaseGateway releaseGateway =
                new();

            ReleaseService service =
                new(
                    authorizationService,
                    readinessGateway,
                    releaseGateway);

            InvalidOperationException exception =
                Assert.ThrowsExactly<InvalidOperationException>(
                    () => service.RequestRelease(
                        @"C:\DevelopmentalProject",
                        authenticatedIdentity,
                        SeriesId.V));

            StringAssert.Contains(
                exception.Message,
                "SAFE-STOP");

            Assert.IsFalse(readinessGateway.EvaluateCalled);
            Assert.IsFalse(releaseGateway.RequestReleaseCalled);
        }
        [TestMethod]
        public void RequestRelease_NotReady_SafeStopsBeforeReleaseRecord()
        {
            AuthenticatedIdentity authenticatedIdentity =
                Authenticate(UserId.CreateNew());

            ProjectAuthorizationService authorizationService =
                new(new FakeProjectAuthorizationGateway(
                    ProjectAuthorizationGatewayResult.Authorized(
                        ProjectRole.Senior)));

            FakeReadinessGateway readinessGateway =
                new(ReadinessStatus.NotReady);

            FakeReleaseGateway releaseGateway =
                new();

            ReleaseService service =
                new(
                    authorizationService,
                    readinessGateway,
                    releaseGateway);

            InvalidOperationException exception =
                Assert.ThrowsExactly<InvalidOperationException>(
                    () => service.RequestRelease(
                        @"C:\DevelopmentalProject",
                        authenticatedIdentity,
                        SeriesId.V));

            StringAssert.Contains(
                exception.Message,
                "SAFE-STOP");

            Assert.IsTrue(readinessGateway.EvaluateCalled);
            Assert.IsFalse(releaseGateway.RequestReleaseCalled);
        }

        [TestMethod]
        public void RequestRelease_NonVSeries_SafeStopsBeforeReadinessAndRelease()
        {
            AuthenticatedIdentity authenticatedIdentity =
                Authenticate(UserId.CreateNew());

            ProjectAuthorizationService authorizationService =
                new(new FakeProjectAuthorizationGateway(
                    ProjectAuthorizationGatewayResult.Authorized(
                        ProjectRole.Admin)));

            FakeReadinessGateway readinessGateway =
                new(ReadinessStatus.Ready);

            FakeReleaseGateway releaseGateway =
                new();

            ReleaseService service =
                new(
                    authorizationService,
                    readinessGateway,
                    releaseGateway);

            InvalidOperationException exception =
                Assert.ThrowsExactly<InvalidOperationException>(
                    () => service.RequestRelease(
                        @"C:\DevelopmentalProject",
                        authenticatedIdentity,
                        SeriesId.A));

            StringAssert.Contains(
                exception.Message,
                "SAFE-STOP");

            Assert.IsFalse(readinessGateway.EvaluateCalled);
            Assert.IsFalse(releaseGateway.RequestReleaseCalled);
        }

        private static AuthenticatedIdentity Authenticate(
            UserId userId)
        {
            AuthenticationService authenticationService =
                new(new FakeAuthenticationGateway(
                    AuthenticationGatewayResult.Succeeded(userId)));

            return authenticationService.Authenticate(
                "development-user",
                "development-secret");
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

            public AuthenticationGatewayResult Authenticate(
                string loginName,
                string secret)
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

        private sealed class FakeReadinessGateway
            : IReadinessGateway
        {
            private readonly ReadinessStatus _status;

            public bool EvaluateCalled { get; private set; }

            public ReadinessCheckId LastReadinessCheckId { get; private set; }

            public FakeReadinessGateway(
                ReadinessStatus status)
            {
                _status = status;
            }

            public ReadinessCheckResult Evaluate(
                string projectRoot,
                AuthorizedProjectIdentity authorizedIdentity)
            {
                EvaluateCalled = true;

                ReadinessCheckId checkId =
                    ReadinessCheckId.CreateNew();

                LastReadinessCheckId = checkId;

                ReadinessCheckRecord check =
                    new(
                        checkId,
                        OperationId.CreateNew(),
                        _status,
                        GovernedTimestamp.CreateNow());

                ReadinessGateStatus gateStatus =
                    _status == ReadinessStatus.Ready
                        ? ReadinessGateStatus.Pass
                        : ReadinessGateStatus.Blocked;

                List<ReadinessCheckItemRecord> items =
                    new()
                    {
                        new(checkId, 1, ReadinessGateCode.ProjectIdentity, gateStatus, null),
                        new(checkId, 2, ReadinessGateCode.SourceEvidence, ReadinessGateStatus.Pass, null),
                        new(checkId, 3, ReadinessGateCode.ProjectJmfTruth, ReadinessGateStatus.Pass, null),
                        new(checkId, 4, ReadinessGateCode.Maintenance, ReadinessGateStatus.Pass, null),
                        new(checkId, 5, ReadinessGateCode.ProjectJmfReview, ReadinessGateStatus.Pass, null),
                        new(checkId, 6, ReadinessGateCode.Lineage, ReadinessGateStatus.Pass, null),
                        new(checkId, 7, ReadinessGateCode.FreezeCapability, ReadinessGateStatus.Pass, null)
                    };

                return new ReadinessCheckResult(
                    check,
                    items);
            }
        }

        private sealed class FakeReleaseGateway
            : IReleaseGateway
        {
            public bool RequestReleaseCalled { get; private set; }

            public AuthorizedProjectIdentity?
                ReceivedAuthorizedIdentity
            { get; private set; }

            public ReadinessCheckId
                ReceivedReadinessCheckId
            { get; private set; }

            public ReleaseRecord RequestRelease(
                string projectRoot,
                AuthorizedProjectIdentity authorizedIdentity,
                ReadinessCheckId readinessCheckId)
            {
                RequestReleaseCalled = true;
                ReceivedAuthorizedIdentity = authorizedIdentity;
                ReceivedReadinessCheckId = readinessCheckId;

                return new ReleaseRecord(
                    ReleaseId.CreateNew(),
                    readinessCheckId,
                    OperationId.CreateNew(),
                    GovernedTimestamp.CreateNow());
            }
        }
    }
}