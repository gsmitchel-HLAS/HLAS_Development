using HLAS.Infrastructure;
using Microsoft.Win32;
using System.Windows;

namespace HLAS.Desktop
{
    public partial class MainWindow : Window
    {
        private OperationalUserSession? _userSession;
        private OperationalProjectSession? _projectSession;
        public MainWindow()
        {
            InitializeComponent();

            RefreshAuthenticationState();
            RefreshContext();
        }

        private void BootstrapIdentityButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                _userSession =
                    OperationalUserSession.BootstrapInitialIdentity(
                        LoginNameTextBox.Text,
                        SecretPasswordBox.Password);

                SecretPasswordBox.Clear();

                CommandResultText.Text =
                    "Initial HLAS identity created and authenticated.";

                RefreshAuthenticationState();
                RefreshContext();
            }
            catch (Exception ex)
            {
                CommandResultText.Text =
                    ex.Message;
            }
        }

        private void LoginButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                _userSession =
                    OperationalUserSession.Authenticate(
                        LoginNameTextBox.Text,
                        SecretPasswordBox.Password);

                SecretPasswordBox.Clear();

                CommandResultText.Text =
                    "HLAS authentication succeeded.";

                RefreshAuthenticationState();
                RefreshContext();
            }
            catch (Exception ex)
            {
                CommandResultText.Text =
                    ex.Message;
            }
        }
        private void NewProjectButton_Click(
    object sender,
    RoutedEventArgs e)
        {
            try
            {
                if (_userSession is null)
                {
                    throw new InvalidOperationException(
                        "SAFE-STOP: Login is required.");
                }

                OpenFolderDialog dialog = new()
                {
                    Title = "Save New HLAS Project As"
                };

                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                _projectSession =
                    OperationalProjectSession.CreateNew(
                        dialog.FolderName,
                        _userSession.AuthenticatedIdentity);

                CommandResultText.Text =
                    "New HLAS project created.";

                RefreshContext();
            }
            catch (Exception ex)
            {
                CommandResultText.Text =
                    ex.Message;
            }
        }

        private void OpenProjectButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                if (_userSession is null)
                {
                    throw new InvalidOperationException(
                        "SAFE-STOP: Login is required.");
                }

                OpenFolderDialog dialog = new()
                {
                    Title = "Open Existing HLAS Project"
                };

                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                _projectSession =
                    OperationalProjectSession.OpenExisting(
                        dialog.FolderName,
                        _userSession.AuthenticatedIdentity);

                CommandResultText.Text =
                    "HLAS project opened.";

                RefreshContext();
            }
            catch (Exception ex)
            {
                CommandResultText.Text =
                    ex.Message;
            }
        }
        private void RefreshAuthenticationState()
        {
            bool bootstrapRequired =
                LocalIdentityStore.IsInitialBootstrapRequired();

            BootstrapIdentityButton.IsEnabled =
                bootstrapRequired &&
                _userSession is null;

            LoginButton.IsEnabled =
                !bootstrapRequired &&
                _userSession is null;

            NewProjectButton.IsEnabled =
    _userSession is not null;

            OpenProjectButton.IsEnabled =
                _userSession is not null;

            AuthenticationStatusText.Text =
                bootstrapRequired
                    ? "Initial HLAS user must be created."
                    : _userSession is null
                        ? "Login required."
                        : "HLAS user authenticated.";
        }

        
            private void RefreshContext()
        {
            ProjectContextText.Text =
                _projectSession is null
                    ? "No project open"
                    : _projectSession.Context.ProjectId?.ToString()
                        ?? "None";

            UserContextText.Text =
                _userSession is null
                    ? "No authenticated user"
                    : _userSession.AuthenticatedIdentity.UserId.ToString();

            RoleContextText.Text =
                _projectSession?.Context.ProjectRole?.ToString()
                ?? "None";

            SeriesContextText.Text =
                _projectSession?.Context.SeriesId?.ToString()
                ?? "None";
            ProjectStatusFooterText.Text =
        _projectSession is null
            ? "No governed HLAS project is open."
            : "Governed HLAS project is open.";
        }
    }
}