using HLAS.Application;
using HLAS.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HLAS.Tests
{
    [TestClass]
    public sealed class ProductionSourceEvidenceRetrievalServiceTests
    {
        [TestMethod]
        public void Retrieve_VSeriesRequest_UsesCustodyGatewayAndReturnsResult()
        {
            ProjectId projectId = ProjectId.CreateNew();
            UserId userId = UserId.CreateNew();
            EvidenceId evidenceId = EvidenceId.CreateNew();

            EvidenceCustodyRecord record = new(
                evidenceId,
                "Developmental Production Source.txt",
                @"HLAS_Source_Evidence\evidence-id\Developmental Production Source.txt",
                123,
                "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                GovernedTimestamp.CreateNow());

            EvidenceCustodyRetrievalResult expected =
                new(
                    record,
                    @"C:\Developmental\HLAS_Project\HLAS_Source_Evidence\evidence-id\Developmental Production Source.txt");

            FakeEvidenceCustodyGateway gateway =
                new(expected);

            ProductionSourceEvidenceRetrievalService service =
                new(gateway);

            ProductionSourceEvidenceRetrievalRequest request =
                new(
                    projectId,
                    userId,
                    ProjectRole.Admin,
                    SeriesId.V,
                    evidenceId);

            EvidenceCustodyRetrievalResult result =
                service.Retrieve(
                    @"C:\Developmental\HLAS_Project",
                    request);

            Assert.AreEqual(
                @"C:\Developmental\HLAS_Project",
                gateway.ReceivedProjectRoot);

            Assert.AreEqual(
                evidenceId,
                gateway.ReceivedEvidenceId);

            Assert.AreSame(
                expected,
                result);
        }

        private sealed class FakeEvidenceCustodyGateway
            : IEvidenceCustodyGateway
        {
            private readonly EvidenceCustodyRetrievalResult _result;

            public string? ReceivedProjectRoot { get; private set; }

            public EvidenceId? ReceivedEvidenceId { get; private set; }

            public FakeEvidenceCustodyGateway(
                EvidenceCustodyRetrievalResult result)
            {
                _result = result;
            }

            public EvidenceCustodyRecord Accept(
                string projectRoot,
                string sourceFilePath)
            {
                throw new System.NotSupportedException(
                    "Intake is not used by this retrieval-service test.");
            }

            public EvidenceCustodyRetrievalResult Retrieve(
                string projectRoot,
                EvidenceId evidenceId)
            {
                ReceivedProjectRoot = projectRoot;
                ReceivedEvidenceId = evidenceId;

                return _result;
            }
        }
    }
}