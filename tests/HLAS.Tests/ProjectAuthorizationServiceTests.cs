using HLAS.Application;
using HLAS.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class ProjectAuthorizationServiceTests
    {
        [TestMethod]
        public void Authorize_ProjectAuthorizesIdentity_ReturnsProjectRole()
        {
            UserId userId = UserId.CreateNew();

            AuthenticationService authenticationService =
                new(new FakeAuthenticationGateway(
                    AuthenticationGatewayResult.Succeeded(userId)));

            AuthenticatedIdentity authenticatedIdentity =
                authenticationService.Authenticate();

            ProjectAuthorizationService service =
                new(new FakeProjectAuthorizationGateway(
                    ProjectAuthorizationGatewayResult.Authorized(
                        ProjectRole.Senior)));

            AuthorizedProjectIdentity result =
                service.Authorize(
                    @"C:\DevelopmentalProject",
                    authenticatedIdentity);

            Assert.AreEqual(userId, result.UserId);
            Assert.AreEqual(ProjectRole.Senior, result.ProjectRole);
        }

        [TestMethod]
        public void Authorize_ProjectRejectsIdentity_SafeStops()
        {
            UserId userId = UserId.CreateNew();

            AuthenticationService authenticationService =
                new(new FakeAuthenticationGateway(
                    AuthenticationGatewayResult.Succeeded(userId)));

            AuthenticatedIdentity authenticatedIdentity =
                authenticationService.Authenticate();

            ProjectAuthorizationService service =
                new(new FakeProjectAuthorizationGateway(
                    ProjectAuthorizationGatewayResult.Rejected(
                        "User is not authorized for this project.")));

            InvalidOperationException exception =
                Assert.ThrowsExactly<InvalidOperationException>(
                    () => service.Authorize(
                        @"C:\DevelopmentalProject",
                        authenticatedIdentity));

            StringAssert.Contains(
                exception.Message,
                "SAFE-STOP");
        }

        [TestMethod]
        public void Authorize_UsesAuthenticatedUserIdForProjectLookup()
        {
            UserId userId = UserId.CreateNew();

            AuthenticationService authenticationService =
                new(new FakeAuthenticationGateway(
                    AuthenticationGatewayResult.Succeeded(userId)));

            AuthenticatedIdentity authenticatedIdentity =
                authenticationService.Authenticate();

            FakeProjectAuthorizationGateway gateway =
                new(
                    ProjectAuthorizationGatewayResult.Authorized(
                        ProjectRole.Admin));

            ProjectAuthorizationService service =
                new(gateway);

            service.Authorize(
                @"C:\DevelopmentalProject",
                authenticatedIdentity);

            Assert.AreEqual(userId, gateway.ReceivedUserId);
        }
        [TestMethod]
        public void AuthorizedProjectIdentity_HasNoPublicConstructor()
        {
            Assert.HasCount(
                0,
                typeof(AuthorizedProjectIdentity).GetConstructors());
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

            public UserId? ReceivedUserId { get; private set; }

            public FakeProjectAuthorizationGateway(
                ProjectAuthorizationGatewayResult result)
            {
                _result = result;
            }

            public ProjectAuthorizationGatewayResult Resolve(
                string projectRoot,
                UserId userId)
            {
                ReceivedUserId = userId;
                return _result;
            }
        }
    }
}