using System.Collections.Generic;
using RadialLauncher.Data;

namespace RadialLauncher.Services.Scanning
{
    public interface IPCScannerService
    {
        List<ScannedApp> ScanAllApps();
        ScanSummary SaveScannedApps(IEnumerable<ScannedApp> apps, DatabaseManager db);
    }
}
