using HLAS.Application;
using HLAS.Domain;
using Microsoft.Win32;
using System.Diagnostics;
using System.Windows;

namespace HLAS.Desktop
{
    public partial class MainWindow : Window
    {
        private readonly DevelopmentalProjectSession _developmentalSession;
        private readonly ShellContext _context;
        private readonly ShellCommandRouter _router = new();
        private readonly ProductionSourceEvidenceIntakeService _productionIntakeService;
        private readonly ProductionSourceEvidenceRetrievalService _productionRetrievalService;
        private readonly DevelopmentalProjectHistoryProofService _projectHistoryProofService;
        private EvidenceId? _lastProductionEvidenceId;
        public MainWindow()
        {
            InitializeComponent();

            _developmentalSession =
                DevelopmentalProjectSession.Create();

            _context = new ShellContext(
                _developmentalSession.Manifest.ProjectId,
                UserId.CreateNew(),
                ProjectRole.Admin,
                SeriesId.V);

            _productionIntakeService =
                new ProductionSourceEvidenceIntakeService(
             new ProductionSourceEvidenceIntakeGatewayAdapter());

            _productionRetrievalService =
    new ProductionSourceEvidenceRetrievalService(
        new EvidenceCustodyGatewayAdapter());

            _projectHistoryProofService =
      new DevelopmentalProjectHistoryProofService(
          new DevelopmentalProjectHistoryProofGatewayAdapter());
            ProjectContextText.Text =
                $"DEVELOPMENTAL: {_context.ProjectId}";

            UserContextText.Text =
                $"DEVELOPMENTAL: {_context.UserId}";

            RoleContextText.Text =
                _context.ProjectRole?.ToString() ?? "None";

            SeriesContextText.Text =
                _context.SeriesId?.ToString() ?? "None";
        }

        private void ShowContextButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShellCommandResult result =
                _router.Route(
                    ShellCommand.ShowContext,
                    _context);

            CommandResultText.Text = result.Message;
        }

        private void SelectProductionSourceButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            OpenFileDialog dialog = new()
            {
                Title = "Select Developmental Production Source"
            };

            if (dialog.ShowDialog() != true)
            {
                CommandResultText.Text =
                    "Developmental Production Source selection cancelled.";

                return;
            }

            try
            {
                ProjectId projectId =
      _context.ProjectId
      ?? throw new InvalidOperationException(
          "Developmental ProjectId is not available.");

                UserId userId =
                    _context.UserId
                    ?? throw new InvalidOperationException(
                        "Developmental UserId is not available.");

                ProjectRole projectRole =
                    _context.ProjectRole
                    ?? throw new InvalidOperationException(
                        "Developmental ProjectRole is not available.");

                SeriesId seriesId =
                    _context.SeriesId
                    ?? throw new InvalidOperationException(
                        "Developmental SeriesId is not available.");

                ProductionSourceEvidenceIntakeRequest request =
                    new(
                        projectId,
                        userId,
                        projectRole,
                        seriesId,
                        dialog.FileName);

                ProductionSourceEvidenceIntakeResult result =
                    _productionIntakeService.Intake(
                        _developmentalSession.ProjectRoot,
                        request);

                _lastProductionEvidenceId =
    result.EvidenceRecord.EvidenceId;

                CommandResultText.Text =
                    $"{result.Message}\n" +
                    $"Evidence ID: {result.EvidenceRecord.EvidenceId}\n" +
                    $"Original file: {result.EvidenceRecord.OriginalFileName}\n" +
                    $"Custody path: {result.EvidenceRecord.RelativeCustodyPath}\n" +
                    $"SHA-256: {result.EvidenceRecord.Sha256Hex}";
            }
            catch (Exception ex)
            {
                CommandResultText.Text =
                    $"Developmental Production Source intake failed: {ex.Message}";
            }
        }

      private void ViewProductionSourceButton_Click(
    object sender,
    RoutedEventArgs e)
{
    try
    {
        EvidenceId evidenceId =
            _lastProductionEvidenceId
            ?? throw new InvalidOperationException(
                "No developmental Production Source Evidence has been accepted in this session.");

        ProjectId projectId =
            _context.ProjectId
            ?? throw new InvalidOperationException(
                "Developmental ProjectId is not available.");

        UserId userId =
            _context.UserId
            ?? throw new InvalidOperationException(
                "Developmental UserId is not available.");

        ProjectRole projectRole =
            _context.ProjectRole
            ?? throw new InvalidOperationException(
                "Developmental ProjectRole is not available.");

        SeriesId seriesId =
            _context.SeriesId
            ?? throw new InvalidOperationException(
                "Developmental SeriesId is not available.");

        ProductionSourceEvidenceRetrievalRequest request =
            new(
                projectId,
                userId,
                projectRole,
                seriesId,
                evidenceId);

        EvidenceCustodyRetrievalResult result =
            _productionRetrievalService.Retrieve(
                _developmentalSession.ProjectRoot,
                request);

        Process.Start(
            new ProcessStartInfo
            {
                FileName = result.ControlledFilePath,
                UseShellExecute = true
            });

        CommandResultText.Text =
            $"Developmental Production Source Evidence retrieval completed.\n" +
            $"Evidence ID: {result.EvidenceRecord.EvidenceId}\n" +
            $"Controlled file: {result.ControlledFilePath}";
    }
    catch (Exception ex)
    {
        CommandResultText.Text =
            $"Developmental Production Source retrieval failed: {ex.Message}";
    }
}
       
        private void RunDevelopmentalHistoryProofButton_Click(
    object sender,
    RoutedEventArgs e)
        {
            OpenFolderDialog dialog = new()
            {
                Title = "Select Persistent Developmental HLAS Project Folder"
            };

            if (dialog.ShowDialog() != true)
            {
                CommandResultText.Text =
                    "Persistent developmental project selection cancelled.";

                return;
            }

            try
            {
                throw new InvalidOperationException(
    "SAFE-STOP: Persistent developmental project-history proof now requires authenticated project authorization.");
            }
            catch (Exception ex)
            {
                CommandResultText.Text =
                    $"Persistent developmental project-history proof failed: {ex.Message}";
            }
        }
        protected override void OnClosed(EventArgs e)
        {
            _developmentalSession.Dispose();
            base.OnClosed(e);
        }
    }
}