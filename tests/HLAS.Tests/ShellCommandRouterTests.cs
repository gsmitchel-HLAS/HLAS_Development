using HLAS.Application;
using HLAS.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class ShellCommandRouterTests
    {
        [TestMethod]
        public void Route_ShowContext_PreservesContextAndReturnsApplicationMessage()
        {
            ProjectId projectId = ProjectId.CreateNew();
            UserId userId = UserId.CreateNew();

            ShellContext context = new(
                projectId,
                userId,
                ProjectRole.Admin,
                SeriesId.V);

            ShellCommandRouter router = new();

            ShellCommandResult result =
                router.Route(
                    ShellCommand.ShowContext,
                    context);

            Assert.AreEqual(
                ShellCommand.ShowContext,
                result.Command);

            Assert.AreEqual(
                projectId,
                result.Context.ProjectId);

            Assert.AreEqual(
                userId,
                result.Context.UserId);

            Assert.AreEqual(
                ProjectRole.Admin,
                result.Context.ProjectRole);

            Assert.AreEqual(
                SeriesId.V,
                result.Context.SeriesId);

            Assert.AreEqual(
                "Shell context route reached HLAS.Application.",
                result.Message);
        }
    }
}