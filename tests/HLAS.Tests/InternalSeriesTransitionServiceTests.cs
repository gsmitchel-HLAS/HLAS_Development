using System;
using HLAS.Application;
using HLAS.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class InternalSeriesTransitionServiceTests
    {
        [TestMethod]
        public void CreateTransition_AdminAuthorized_DelegatesCorrectAuthority()
        {
            UserId userId = UserId.CreateNew();

            AuthenticatedIdentity identity =
                Authenticate(userId);

            ProjectAuthorizationService authorization =
                new(new FakeAuthorizationGateway(
                    ProjectAuthorizationGatewayResult.Authorized(
                        ProjectRole.Admin)));

            FakeTransitionGateway gateway = new();

            InternalSeriesTransitionService service =
                new(authorization, gateway);

            ReleaseId releaseId = ReleaseId.CreateNew();

            _ = service.CreateTransition(
                @"C:\DevelopmentalProject",
                identity,
                SeriesId.A,
                releaseId);

            Assert.IsTrue(gateway.Called);
            Assert.AreEqual(
                userId,
                gateway.ReceivedIdentity!.UserId);
            Assert.AreEqual(
                ProjectRole.Admin,
                gateway.ReceivedIdentity.ProjectRole);
            Assert.AreEqual(
                SeriesId.A,
                gateway.ReceivedSeriesId);
            Assert.AreEqual(
                releaseId,
                gateway.ReceivedReleaseId);
        }
        [TestMethod]
        public void CreateTransition_SeniorAuthorized_DelegatesCorrectAuthority()
        {
            UserId userId = UserId.CreateNew();

            AuthenticatedIdentity identity =
                Authenticate(userId);

            ProjectAuthorizationService authorization =
                new(new FakeAuthorizationGateway(
                    ProjectAuthorizationGatewayResult.Authorized(
                        ProjectRole.Senior)));

            FakeTransitionGateway gateway = new();

            InternalSeriesTransitionService service =
                new(authorization, gateway);

            ReleaseId releaseId = ReleaseId.CreateNew();

            _ = service.CreateTransition(
                @"C:\DevelopmentalProject",
                identity,
                SeriesId.A,
                releaseId);

            Assert.IsTrue(gateway.Called);
            Assert.AreEqual(
                userId,
                gateway.ReceivedIdentity!.UserId);
            Assert.AreEqual(
                ProjectRole.Senior,
                gateway.ReceivedIdentity.ProjectRole);
            Assert.AreEqual(
                releaseId,
                gateway.ReceivedReleaseId);
        }
        [TestMethod]
        public void CreateTransition_Technician_SafeStops()
        {
            AuthenticatedIdentity identity =
                Authenticate(UserId.CreateNew());

            ProjectAuthorizationService authorization =
                new(new FakeAuthorizationGateway(
                    ProjectAuthorizationGatewayResult.Authorized(
                        ProjectRole.Technician)));

            FakeTransitionGateway gateway = new();

            InternalSeriesTransitionService service =
                new(authorization, gateway);

            InvalidOperationException exception =
                Assert.ThrowsExactly<InvalidOperationException>(
                    () => service.CreateTransition(
                        @"C:\DevelopmentalProject",
                        identity,
                        SeriesId.A,
                        ReleaseId.CreateNew()));

            StringAssert.Contains(
                exception.Message,
                "SAFE-STOP");

            Assert.IsFalse(gateway.Called);
        }
        [TestMethod]
        public void CreateTransition_ProjectUnauthorized_SafeStops()
        {
            AuthenticatedIdentity identity =
                Authenticate(UserId.CreateNew());

            ProjectAuthorizationService authorization =
                new(new FakeAuthorizationGateway(
                    ProjectAuthorizationGatewayResult.Rejected(
                        "User is not authorized for this project.")));

            FakeTransitionGateway gateway = new();

            InternalSeriesTransitionService service =
                new(authorization, gateway);

            InvalidOperationException exception =
                Assert.ThrowsExactly<InvalidOperationException>(
                    () => service.CreateTransition(
                        @"C:\DevelopmentalProject",
                        identity,
                        SeriesId.A,
                        ReleaseId.CreateNew()));

            StringAssert.Contains(
                exception.Message,
                "SAFE-STOP");

            Assert.IsFalse(gateway.Called);
        }
        private static AuthenticatedIdentity Authenticate(
             UserId userId)
        {
            AuthenticationService service =
                new(new FakeAuthenticationGateway(userId));

            return service.Authenticate(
                "development-user",
                "development-secret");
        }

        private sealed class FakeAuthenticationGateway
            : IAuthenticationGateway
        {
            private readonly UserId _userId;

            public FakeAuthenticationGateway(UserId userId)
            {
                _userId = userId;
            }

            public AuthenticationGatewayResult Authenticate(
                string loginName,
                string secret)
            {
                return AuthenticationGatewayResult.Succeeded(
                    _userId);
            }
        }

               private sealed class FakeAuthorizationGateway
            : IProjectAuthorizationGateway
        {
            private readonly ProjectAuthorizationGatewayResult
                _result;

            public FakeAuthorizationGateway(
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

        private sealed class FakeTransitionGateway
            : IInternalSeriesTransitionGateway
        {
            public bool Called { get; private set; }

            public InternalSeriesTransitionAuthority
                ReceivedIdentity
            { get; private set; }

            public SeriesId ReceivedSeriesId { get; private set; }

            public ReleaseId ReceivedReleaseId { get; private set; }

            public InternalSeriesTransitionRecord CreateTransition(
                string projectRoot,
                InternalSeriesTransitionAuthority authorizedIdentity,
                SeriesId receivingSeriesId,
                ReleaseId releaseId)
            {
                Called = true;
                ReceivedIdentity = authorizedIdentity;
                ReceivedSeriesId = receivingSeriesId;
                ReceivedReleaseId = releaseId;

                return new InternalSeriesTransitionRecord(
                    InternalSeriesTransitionId.CreateNew(),
                    ProjectId.CreateNew(),
                    OperationId.CreateNew(),
                    receivingSeriesId,
                    releaseId,
                    FreezeId.CreateNew(),
                    ReadinessCheckId.CreateNew(),
                    GovernedTimestamp.CreateNow());
            }
        }
    }
}