using HLAS.Application;
using HLAS.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class ReleaseFreezeServiceTests
    {
        [TestMethod]
        public void CreateReleaseFreeze_AdminAuthorized_DelegatesAuthenticatedProjectIdentity()
        {
            UserId userId = UserId.CreateNew();

            AuthenticatedIdentity authenticatedIdentity =
                Authenticate(userId);

            ProjectAuthorizationService authorizationService =
                new(new FakeProjectAuthorizationGateway(
                    ProjectAuthorizationGatewayResult.Authorized(
                        ProjectRole.Admin)));

            FakeReleaseFreezeGateway gateway =
                new();

            ReleaseFreezeService service =
                new(
                    authorizationService,
                    gateway);

            ReleaseId releaseId =
                ReleaseId.CreateNew();

            _ = service.CreateReleaseFreeze(
                @"C:\DevelopmentalProject",
                authenticatedIdentity,
                SeriesId.V,
                releaseId);

            Assert.IsTrue(gateway.CreateReleaseFreezeCalled);

            Assert.AreEqual(
                userId,
                gateway.ReceivedAuthorizedIdentity!.UserId);

            Assert.AreEqual(
                ProjectRole.Admin,
                gateway.ReceivedAuthorizedIdentity.ProjectRole);

            Assert.AreEqual(
                releaseId,
                gateway.ReceivedReleaseId);
        }

        [TestMethod]
        public void CreateReleaseFreeze_SeniorAuthorized_DelegatesAuthenticatedProjectIdentity()
        {
            UserId userId = UserId.CreateNew();

            AuthenticatedIdentity authenticatedIdentity =
                Authenticate(userId);

            ProjectAuthorizationService authorizationService =
                new(new FakeProjectAuthorizationGateway(
                    ProjectAuthorizationGatewayResult.Authorized(
                        ProjectRole.Senior)));

            FakeReleaseFreezeGateway gateway =
                new();

            ReleaseFreezeService service =
                new(
                    authorizationService,
                    gateway);

            _ = service.CreateReleaseFreeze(
                @"C:\DevelopmentalProject",
                authenticatedIdentity,
                SeriesId.V,
                ReleaseId.CreateNew());

            Assert.IsTrue(gateway.CreateReleaseFreezeCalled);

            Assert.AreEqual(
                userId,
                gateway.ReceivedAuthorizedIdentity!.UserId);

            Assert.AreEqual(
                ProjectRole.Senior,
                gateway.ReceivedAuthorizedIdentity.ProjectRole);
        }

        [TestMethod]
        public void CreateReleaseFreeze_TechnicianAuthorized_SafeStopsBeforeGateway()
        {
            AuthenticatedIdentity authenticatedIdentity =
                Authenticate(UserId.CreateNew());

            ProjectAuthorizationService authorizationService =
                new(new FakeProjectAuthorizationGateway(
                    ProjectAuthorizationGatewayResult.Authorized(
                        ProjectRole.Technician)));

            FakeReleaseFreezeGateway gateway =
                new();

            ReleaseFreezeService service =
                new(
                    authorizationService,
                    gateway);

            InvalidOperationException exception =
                Assert.ThrowsExactly<InvalidOperationException>(
                    () => service.CreateReleaseFreeze(
                        @"C:\DevelopmentalProject",
                        authenticatedIdentity,
                        SeriesId.V,
                        ReleaseId.CreateNew()));

            StringAssert.Contains(
                exception.Message,
                "SAFE-STOP");

            Assert.IsFalse(gateway.CreateReleaseFreezeCalled);
        }

        [TestMethod]
        public void CreateReleaseFreeze_ProjectUnauthorized_SafeStopsBeforeGateway()
        {
            AuthenticatedIdentity authenticatedIdentity =
                Authenticate(UserId.CreateNew());

            ProjectAuthorizationService authorizationService =
                new(new FakeProjectAuthorizationGateway(
                    ProjectAuthorizationGatewayResult.Rejected(
                        "User is not authorized for this project.")));

            FakeReleaseFreezeGateway gateway =
                new();

            ReleaseFreezeService service =
                new(
                    authorizationService,
                    gateway);

            InvalidOperationException exception =
                Assert.ThrowsExactly<InvalidOperationException>(
                    () => service.CreateReleaseFreeze(
                        @"C:\DevelopmentalProject",
                        authenticatedIdentity,
                        SeriesId.V,
                        ReleaseId.CreateNew()));

            StringAssert.Contains(
                exception.Message,
                "SAFE-STOP");

            Assert.IsFalse(gateway.CreateReleaseFreezeCalled);
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

        private sealed class FakeReleaseFreezeGateway
            : IReleaseFreezeGateway
        {
            public bool CreateReleaseFreezeCalled { get; private set; }

            public AuthorizedProjectIdentity?
                ReceivedAuthorizedIdentity
            { get; private set; }

            public ReleaseId ReceivedReleaseId { get; private set; }

            public HistoricalCheckpointRecord CreateReleaseFreeze(
                string projectRoot,
                AuthorizedProjectIdentity authorizedIdentity,
                ReleaseId releaseId)
            {
                CreateReleaseFreezeCalled = true;
                ReceivedAuthorizedIdentity = authorizedIdentity;
                ReceivedReleaseId = releaseId;

                return new HistoricalCheckpointRecord(
                    FreezeId.CreateNew(),
                    OperationId.CreateNew(),
                    HistoricalCheckpointRecord.ReleaseFreezeType,
                    releaseId,
                    @"HLAS_Frozen_States\release-freeze",
                    @"HLAS_Frozen_States\release-freeze\manifest.json",
                    new string('0', 64),
                    GovernedTimestamp.CreateNow());
            }
        }
    }
}