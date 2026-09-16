using HLAS.Application;
using HLAS.Domain;
using System.Windows;

namespace HLAS.Desktop
{
    public partial class MainWindow : Window
    {
        private readonly ShellContext _context;
        private readonly ShellCommandRouter _router = new();

        public MainWindow()
        {
            InitializeComponent();

            _context = new ShellContext(
                ProjectId.CreateNew(),
                UserId.CreateNew(),
                ProjectRole.Admin,
                SeriesId.V);

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
            CommandResultText.Text =
                "Developmental Production Source selection is not yet connected.";
        }
    }
}