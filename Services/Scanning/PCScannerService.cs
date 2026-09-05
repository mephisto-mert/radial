using System.Collections.Generic;
using System.Linq;
using RadialLauncher.Data;

namespace RadialLauncher.Services.Scanning
{
    public class PCScannerService : IPCScannerService
    {
        public List<ScannedApp> ScanAllApps() => RadialLauncher.Services.PCScannerService.ScanAll();
        public ScanSummary SaveScannedApps(IEnumerable<ScannedApp> apps, DatabaseManager db) => 
            RadialLauncher.Services.PCScannerService.ImportToDatabase(db, apps as List<ScannedApp> ?? apps.ToList());
    }
}
