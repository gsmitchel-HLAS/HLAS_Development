namespace HLAS.Application
{
    public enum ShellCommand
    {
        ShowContext
    }

    public sealed record ShellCommandResult(
        ShellCommand Command,
        ShellContext Context,
        string Message);

    public sealed class ShellCommandRouter
    {
        public ShellCommandResult Route(
            ShellCommand command,
            ShellContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return command switch
            {
                ShellCommand.ShowContext => new ShellCommandResult(
                    command,
                    context,
                    "Shell context route reached HLAS.Application."),

                _ => throw new ArgumentOutOfRangeException(
                    nameof(command),
                    command,
                    "Unsupported shell command.")
            };
        }
    }
}