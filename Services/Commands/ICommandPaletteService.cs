namespace RadialLauncher.Services.Commands
{
    public interface ICommandPaletteService
    {
        bool TryHandle(string query, out string message);
    }
}
