using System;
using System.IO;
using System.Text;

namespace InstaWords.App.Services
{
    /// <summary>
    /// «نسخة المتجر»: whether this process runs from the MSIX package (Microsoft Store), detected with
    /// kernel32 GetCurrentPackageFullName. A Store copy is updated by the Store, starts with Windows
    /// through its startup task, and keeps its files where MSIX redirects them (see <see cref="DataDirOnDisk"/>).
    /// </summary>
    public static class AppPackage
    {
        private static readonly Lazy<string> _fullName = new Lazy<string>(() => Query(NativeMethods.GetCurrentPackageFullName));
        private static readonly Lazy<string> _familyName = new Lazy<string>(() => Query(NativeMethods.GetCurrentPackageFamilyName));
        private static readonly Lazy<InstallKind> _kind = new Lazy<InstallKind>(() => StoreLogic.Kind(IsPackaged, UpdateService.IsInstalled));

        public static bool IsPackaged => _fullName.Value != null;

        /// <summary>For example "12345FastKiwi.InstaWords_2.1.0.0_x64__8wekyb3d8bbwe", or null.</summary>
        public static string FullName => _fullName.Value;

        /// <summary>For example "12345FastKiwi.InstaWords_8wekyb3d8bbwe", or null.</summary>
        public static string FamilyName => _familyName.Value;

        public static InstallKind Kind => _kind.Value;

        /// <summary>
        /// The data folder as Explorer sees it. Inside the package the app reads and writes
        /// %APPDATA%\FastKiwi\InstaWords as usual, but MSIX stores new files in the package's LocalCache,
        /// so «فتح مجلد البيانات» must open that folder. Unpackaged: <see cref="AppPaths.DataDir"/>.
        /// </summary>
        public static string DataDirOnDisk
        {
            get
            {
                var dir = AppPaths.DataDir;
                if (!IsPackaged || AppPaths.IsIsolated) return dir;
                try
                {
                    var redirected = StoreLogic.PackagedDataDir(dir,
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        FamilyName);
                    // Files that existed before the package was installed stay in the classic folder.
                    if (redirected != null && Directory.Exists(redirected)) return redirected;
                }
                catch (Exception ex)
                {
                    Log.Warn("Package data folder: " + ex.Message);
                }
                return dir;
            }
        }

        private delegate int NameQuery(ref int length, StringBuilder buffer);

        private static string Query(NameQuery query)
        {
            try
            {
                var length = 0;
                var rc = query(ref length, null);
                if (!StoreLogic.IsPackagedResult(rc, length)) return null;
                var buffer = new StringBuilder(length);
                rc = query(ref length, buffer);
                return rc == StoreLogic.ErrorSuccess && buffer.Length > 0 ? buffer.ToString() : null;
            }
            catch (Exception ex) when (ex is EntryPointNotFoundException || ex is DllNotFoundException)
            {
                return null; // Windows 7: no package identity at all
            }
        }
    }
}
