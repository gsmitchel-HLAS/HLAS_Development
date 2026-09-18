using HLAS.Application;
using HLAS.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class AuthorizedDevelopmentalProjectHistoryProofServiceTests
    {
        [TestMethod]
        public void Execute_AuthorizedVSeries_UsesProjectSuppliedRole()
        {
            UserId userId = UserId.CreateNew();

            AuthenticatedIdentity authenticatedIdentity =
                new AuthenticationService(
                    new FakeAuthenticationGateway(
                        AuthenticationGatewayResult.Succeeded(userId)))
                .Authenticate();

            ProjectAuthorizationService authorizationService =
                new(new FakeProjectAuthorizationGateway(
                    ProjectAuthorizationGatewayResult.Authorized(
                        ProjectRole.Senior)));

            FakeHistoryGateway historyGateway = new();

            AuthorizedDevelopmentalProjectHistoryProofService service =
                new(
                    authorizationService,
                    new DevelopmentalProjectHistoryProofService(
                        historyGateway));

            service.Execute(
                @"C:\DevelopmentalProject",
                authenticatedIdentity,
                SeriesId.V);

            Assert.AreEqual(
                ProjectRole.Senior,
                historyGateway.ReceivedProjectRole);
        }

        [TestMethod]
        public void Execute_ProjectRejectsIdentity_SafeStopsBeforeHistory()
        {
            UserId userId = UserId.CreateNew();

            AuthenticatedIdentity authenticatedIdentity =
                new AuthenticationService(
                    new FakeAuthenticationGateway(
                        AuthenticationGatewayResult.Succeeded(userId)))
                .Authenticate();

            ProjectAuthorizationService authorizationService =
                new(new FakeProjectAuthorizationGateway(
                    ProjectAuthorizationGatewayResult.Rejected(
                        "User is not authorized.")));

            FakeHistoryGateway historyGateway = new();

            AuthorizedDevelopmentalProjectHistoryProofService service =
                new(
                    authorizationService,
                    new DevelopmentalProjectHistoryProofService(
                        historyGateway));

            Assert.ThrowsExactly<InvalidOperationException>(
                () => service.Execute(
                    @"C:\DevelopmentalProject",
                    authenticatedIdentity,
                    SeriesId.V));

            Assert.IsFalse(historyGateway.WasCalled);
        }

        [TestMethod]
        public void Execute_NonVSeries_SafeStopsBeforeHistory()
        {
            UserId userId = UserId.CreateNew();

            AuthenticatedIdentity authenticatedIdentity =
                new AuthenticationService(
                    new FakeAuthenticationGateway(
                        AuthenticationGatewayResult.Succeeded(userId)))
                .Authenticate();

            ProjectAuthorizationService authorizationService =
                new(new FakeProjectAuthorizationGateway(
                    ProjectAuthorizationGatewayResult.Authorized(
                        ProjectRole.Admin)));

            FakeHistoryGateway historyGateway = new();

            AuthorizedDevelopmentalProjectHistoryProofService service =
                new(
                    authorizationService,
                    new DevelopmentalProjectHistoryProofService(
                        historyGateway));

            Assert.ThrowsExactly<InvalidOperationException>(
                () => service.Execute(
                    @"C:\DevelopmentalProject",
                    authenticatedIdentity,
                    SeriesId.A));

            Assert.IsFalse(historyGateway.WasCalled);
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

        private sealed class FakeHistoryGateway
            : IDevelopmentalProjectHistoryProofGateway
        {
            public bool WasCalled { get; private set; }
            public ProjectRole ReceivedProjectRole { get; private set; }

            public DevelopmentalProjectHistoryProofResult Execute(
                string projectRoot,
                UserId userId,
                SeriesId seriesId,
                ProjectRole projectRole,
                DecisionRecord decisionRecord)
            {
                WasCalled = true;
                ReceivedProjectRole = projectRole;

                return new DevelopmentalProjectHistoryProofResult(
                    ProjectId.CreateNew(),
                    OperationId.CreateNew(),
                    projectRoot);
            }
        }
    }
}