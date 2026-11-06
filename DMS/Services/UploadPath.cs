namespace DMS.Services
{
    /// <summary>
    /// Pengaman path file upload (perbaikan keamanan 2026-11-06).
    ///
    /// Banyak endpoint menerima path file dari browser (query string / form) lalu
    /// langsung membaca atau MENGHAPUS webRootPath + path. Path seperti
    /// "/../appsettings.json" atau "/Upload/../../x" bisa keluar dari folder upload,
    /// sehingga file konfigurasi server bisa diunduh atau file mana pun bisa terhapus.
    ///
    /// Semua file milik user (dokumen, template kategori, dokumen eksternal, foto &
    /// tanda tangan user) disimpan di wwwroot/Upload/ATTACHMENT/ (TB_M_SYSTEM
    /// UPLOAD_FOLDER), jadi path yang sah selalu berada di dalam folder itu.
    /// </summary>
    public static class UploadPath
    {
        /// <summary>
        /// Path lengkap untuk path relatif (mis. "/Upload/ATTACHMENT/DOCUMENT_TRANSACTION/x.xlsx")
        /// kalau path itu benar-benar berada di dalam wwwroot/Upload/ATTACHMENT/; selain itu null.
        /// </summary>
        public static string Resolve(string webRootPath, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(webRootPath) || string.IsNullOrWhiteSpace(relativePath)) return null;

            string root = Path.GetFullPath(Path.Combine(webRootPath, "Upload", "ATTACHMENT"))
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            string relative = relativePath.Trim().Replace('\\', '/').TrimStart('/');
            if (relative.Length == 0 || relative.Contains('\0') || relative.Contains(':')) return null;

            string full;
            try
            {
                full = Path.GetFullPath(Path.Combine(webRootPath, relative));
            }
            catch (Exception)
            {
                return null;
            }

            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full : null;
        }
    }
}
