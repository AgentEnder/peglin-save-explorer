using System.CommandLine;

namespace peglin_save_explorer.Commands
{
    public interface ICommand
    {
        Command CreateCommand();
    }
}