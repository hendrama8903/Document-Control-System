namespace DMS.Services
{
    /// <summary>
    /// Where uploaded files live, and the guard on paths sent by the browser.
    ///
    /// Storage (2026-11-10): user files (documents, external documents, user photos and
    /// signatures, the PDF cache) live under &lt;Root&gt;/Upload/ATTACHMENT/. Root is
    /// appsettings "Storage:RootPath" - a folder outside the application, so publishing a new
    /// version never touches the documents - or wwwroot when it is not set (development). Paths
    /// stored in the database stay "/Upload/ATTACHMENT/...", so moving the storage needs no data
    /// change: Startup serves /Upload from Root, and code builds physical paths as
    /// Root + "/Upload/...".
    ///
    /// Path guard (security fix 2026-11-06): many endpoints take a file path from the browser and
    /// read or delete it. "/../appsettings.json" or "/Upload/../../x" could escape the upload
    /// folder, so <see cref="Resolve"/> only returns paths inside &lt;Root&gt;/Upload/ATTACHMENT/.
    /// </summary>
    public static class UploadPath
    {
        /// <summary>Folder that holds Upload/ATTACHMENT. Set once in Startup.Configure.</summary>
        public static string Root { get; private set; }

        /// <summary>Use the configured storage folder, or wwwroot when none is configured.</summary>
        public static void Configure(string configuredRoot, string webRootPath)
        {
            Root = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredRoot) ? webRootPath : configuredRoot.Trim())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Directory.CreateDirectory(Path.Combine(Root, "Upload", "ATTACHMENT"));
        }

        /// <summary>
        /// Full path of a relative path such as "/Upload/ATTACHMENT/DOCUMENT_TRANSACTION/x.xlsx", when
        /// it really is inside &lt;Root&gt;/Upload/ATTACHMENT/; otherwise null. The first parameter is
        /// kept for the existing callers and only used before Root is configured.
        /// </summary>
        public static string Resolve(string webRootPath, string relativePath)
        {
            string baseFolder = Root ?? webRootPath;
            if (string.IsNullOrWhiteSpace(baseFolder) || string.IsNullOrWhiteSpace(relativePath)) return null;

            string root = Path.GetFullPath(Path.Combine(baseFolder, "Upload", "ATTACHMENT"))
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            string relative = relativePath.Trim().Replace('\\', '/').TrimStart('/');
            if (relative.Length == 0 || relative.Contains('\0') || relative.Contains(':')) return null;

            string full;
            try
            {
                full = Path.GetFullPath(Path.Combine(baseFolder, relative));
            }
            catch (Exception)
            {
                return null;
            }

            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full : null;
        }
    }
}
