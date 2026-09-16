using HLAS.Application;
using HLAS.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class ProductionSourceEvidenceIntakeServiceTests
    {
        [TestMethod]
        public void Intake_VSeriesRequest_UsesCustodyGatewayAndReturnsProjectLinkedResult()
        {
            ProjectId projectId = ProjectId.CreateNew();
            UserId userId = UserId.CreateNew();

            EvidenceCustodyRecord expectedRecord = new(
                EvidenceId.CreateNew(),
                "Developmental Production Source.pdf",
                @"HLAS_Source_Evidence\evidence-id\Developmental Production Source.pdf",
                12345,
                "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                GovernedTimestamp.CreateNow());

            FakeEvidenceCustodyGateway gateway = new(
                expectedRecord);

            ProductionSourceEvidenceIntakeService service = new(
                gateway);

            ProductionSourceEvidenceIntakeRequest request = new(
                projectId,
                userId,
                ProjectRole.Admin,
                SeriesId.V,
                @"C:\Developmental\Production Source.pdf");

            ProductionSourceEvidenceIntakeResult result =
                service.Intake(
                    @"C:\Developmental\HLAS_Project",
                    request);

            Assert.AreEqual(
                @"C:\Developmental\HLAS_Project",
                gateway.ReceivedProjectRoot);

            Assert.AreEqual(
                @"C:\Developmental\Production Source.pdf",
                gateway.ReceivedSourceFilePath);

            Assert.AreEqual(
                projectId,
                result.ProjectId);

            Assert.AreSame(
                expectedRecord,
                result.EvidenceRecord);

            Assert.AreEqual(
                "Developmental Production Source Evidence custody completed.",
                result.Message);
        }

        private sealed class FakeEvidenceCustodyGateway
            : IEvidenceCustodyGateway
        {
            private readonly EvidenceCustodyRecord _record;

            public string? ReceivedProjectRoot { get; private set; }

            public string? ReceivedSourceFilePath { get; private set; }

            public FakeEvidenceCustodyGateway(
                EvidenceCustodyRecord record)
            {
                _record = record;
            }

            public EvidenceCustodyRecord Accept(
                string projectRoot,
                string sourceFilePath)
            {
                ReceivedProjectRoot = projectRoot;
                ReceivedSourceFilePath = sourceFilePath;

                return _record;
            }
        }
    }
}