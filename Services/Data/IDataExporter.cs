namespace RadialLauncher.Services.Data
{
    public interface IDataExporter
    {
        void Export(string path);
        void Import(string path);
    }
}
