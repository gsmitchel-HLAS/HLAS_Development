using System;
using HLAS.Application;
using HLAS.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class AuthenticationServiceTests
    {
        [TestMethod]
        public void Authenticate_AuthoritySucceeds_ReturnsAuthenticatedIdentity()
        {
            UserId userId = UserId.CreateNew();

            AuthenticationService service =
                new(new FakeAuthenticationGateway(
                    AuthenticationGatewayResult.Succeeded(userId)));

            AuthenticatedIdentity identity = service.Authenticate();

            Assert.AreEqual(userId, identity.UserId);
        }

        [TestMethod]
        public void Authenticate_AuthorityRejects_SafeStops()
        {
            AuthenticationService service =
                new(new FakeAuthenticationGateway(
                    AuthenticationGatewayResult.Rejected(
                        "Authentication rejected.")));

            InvalidOperationException exception =
                Assert.ThrowsExactly<InvalidOperationException>(
                    () => service.Authenticate());

            StringAssert.Contains(exception.Message, "SAFE-STOP");
        }

        [TestMethod]
        public void Authenticate_EmptyUserId_SafeStops()
        {
            AuthenticationService service =
                new(new FakeAuthenticationGateway(
                    AuthenticationGatewayResult.Succeeded(
                        new UserId(Guid.Empty))));

            InvalidOperationException exception =
                Assert.ThrowsExactly<InvalidOperationException>(
                    () => service.Authenticate());

            StringAssert.Contains(exception.Message, "SAFE-STOP");
        }

        [TestMethod]
        public void AuthenticatedIdentity_HasNoPublicConstructor()
        {
            Assert.HasCount(
                0,
                typeof(AuthenticatedIdentity).GetConstructors());
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
    }
}