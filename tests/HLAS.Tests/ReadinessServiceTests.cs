using System.Collections.Generic;
using HLAS.Application;
using HLAS.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class ReadinessServiceTests
    {
        [TestMethod]
        public void Evaluate_AdminAuthorized_DelegatesAuthorizedIdentity()
        {
            UserId userId =
                UserId.CreateNew();

            AuthenticationService authenticationService =
                new(new FakeAuthenticationGateway(
                    AuthenticationGatewayResult.Succeeded(userId)));

            AuthenticatedIdentity authenticatedIdentity =
                authenticationService.Authenticate(
                    "development-user",
                    "development-secret");

            ProjectAuthorizationService authorizationService =
                new(new FakeProjectAuthorizationGateway(
                    ProjectAuthorizationGatewayResult.Authorized(
                        ProjectRole.Admin)));

            FakeReadinessGateway gateway =
                new();

            ReadinessService service =
                new(
                    authorizationService,
                    gateway);

            _ = service.Evaluate(
                @"C:\DevelopmentalProject",
                authenticatedIdentity,
                SeriesId.V);

            Assert.IsTrue(
                gateway.EvaluateCalled);

            Assert.AreEqual(
                userId,
                gateway.ReceivedAuthorizedIdentity!.UserId);

            Assert.AreEqual(
                ProjectRole.Admin,
                gateway.ReceivedAuthorizedIdentity.ProjectRole);
        }
        [TestMethod]
        public void Evaluate_TechnicianAuthorized_DelegatesAuthorizedIdentity()
        {
            UserId userId =
                UserId.CreateNew();

            AuthenticationService authenticationService =
                new(new FakeAuthenticationGateway(
                    AuthenticationGatewayResult.Succeeded(userId)));

            AuthenticatedIdentity authenticatedIdentity =
                authenticationService.Authenticate(
                    "development-user",
                    "development-secret");

            ProjectAuthorizationService authorizationService =
                new(new FakeProjectAuthorizationGateway(
                    ProjectAuthorizationGatewayResult.Authorized(
                        ProjectRole.Technician)));

            FakeReadinessGateway gateway =
                new();

            ReadinessService service =
                new(
                    authorizationService,
                    gateway);

            _ = service.Evaluate(
                @"C:\DevelopmentalProject",
                authenticatedIdentity,
                SeriesId.V);

            Assert.IsTrue(
                gateway.EvaluateCalled);

            Assert.AreEqual(
                userId,
                gateway.ReceivedAuthorizedIdentity!.UserId);

            Assert.AreEqual(
                ProjectRole.Technician,
                gateway.ReceivedAuthorizedIdentity.ProjectRole);
        }
        [TestMethod]
        public void Evaluate_NonVSeries_SafeStopsBeforeGateway()
        {
            UserId userId =
                UserId.CreateNew();

            AuthenticationService authenticationService =
                new(new FakeAuthenticationGateway(
                    AuthenticationGatewayResult.Succeeded(userId)));

            AuthenticatedIdentity authenticatedIdentity =
                authenticationService.Authenticate(
                    "development-user",
                    "development-secret");

            ProjectAuthorizationService authorizationService =
                new(new FakeProjectAuthorizationGateway(
                    ProjectAuthorizationGatewayResult.Authorized(
                        ProjectRole.Admin)));

            FakeReadinessGateway gateway =
                new();

            ReadinessService service =
                new(
                    authorizationService,
                    gateway);

            InvalidOperationException exception =
                Assert.ThrowsExactly<InvalidOperationException>(
                    () => service.Evaluate(
                        @"C:\DevelopmentalProject",
                        authenticatedIdentity,
                        SeriesId.A));

            StringAssert.Contains(
                exception.Message,
                "SAFE-STOP");

            Assert.IsFalse(
                gateway.EvaluateCalled);
        }
        [TestMethod]
        public void Evaluate_ProjectAuthorizationRejected_SafeStopsBeforeGateway()
        {
            UserId userId =
                UserId.CreateNew();

            AuthenticationService authenticationService =
                new(new FakeAuthenticationGateway(
                    AuthenticationGatewayResult.Succeeded(userId)));

            AuthenticatedIdentity authenticatedIdentity =
                authenticationService.Authenticate(
                    "development-user",
                    "development-secret");

            ProjectAuthorizationService authorizationService =
                new(new FakeProjectAuthorizationGateway(
                    ProjectAuthorizationGatewayResult.Rejected(
                        "User is not authorized for this project.")));

            FakeReadinessGateway gateway =
                new();

            ReadinessService service =
                new(
                    authorizationService,
                    gateway);

            InvalidOperationException exception =
                Assert.ThrowsExactly<InvalidOperationException>(
                    () => service.Evaluate(
                        @"C:\DevelopmentalProject",
                        authenticatedIdentity,
                        SeriesId.V));

            StringAssert.Contains(
                exception.Message,
                "SAFE-STOP");

            Assert.IsFalse(
                gateway.EvaluateCalled);
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
            public bool EvaluateCalled { get; private set; }

            public AuthorizedProjectIdentity?
                ReceivedAuthorizedIdentity
            { get; private set; }

            public ReadinessCheckResult Evaluate(
                string projectRoot,
                AuthorizedProjectIdentity authorizedIdentity)
            {
                EvaluateCalled = true;
                ReceivedAuthorizedIdentity = authorizedIdentity;

                ReadinessCheckId checkId =
                    ReadinessCheckId.CreateNew();

                ReadinessCheckRecord check =
                    new(
                        checkId,
                        OperationId.CreateNew(),
                        ReadinessStatus.Ready,
                        GovernedTimestamp.CreateNow());

                List<ReadinessCheckItemRecord> items =
                    new()
                    {
                        new(checkId, 1, ReadinessGateCode.ProjectIdentity, ReadinessGateStatus.Pass, null),
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
    }
}