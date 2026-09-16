using HLAS.Application;
using HLAS.Domain;
using Microsoft.Win32;
using System.Windows;

namespace HLAS.Desktop
{
    public partial class MainWindow : Window
    {
        private readonly DevelopmentalProjectSession _developmentalSession;
        private readonly ShellContext _context;
        private readonly ShellCommandRouter _router = new();
        private readonly ProductionSourceEvidenceIntakeService _productionIntakeService;

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
                    new EvidenceCustodyGatewayAdapter());

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

        protected override void OnClosed(EventArgs e)
        {
            _developmentalSession.Dispose();
            base.OnClosed(e);
        }
    }
}