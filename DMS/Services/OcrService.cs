using Tesseract;

namespace DMS.Services
{
    /// <summary>
    /// OCR untuk dokumen hasil scan (tahap 1 - pencarian, request Hendra 2026-11-06).
    /// Memakai Tesseract (NuGet "Tesseract", Apache 2.0) + data bahasa Indonesia &amp; Inggris
    /// di folder tessdata aplikasi (ikut ter-copy saat build/publish - tidak perlu instalasi
    /// di server selain Visual C++ Redistributable x64 yang biasanya sudah ada).
    ///
    /// Satu instance = satu mesin OCR (mahal dibuat, ~0,5 detik) - dipakai ulang untuk
    /// semua halaman dalam satu putaran indexer, lalu di-Dispose.
    /// </summary>
    public sealed class OcrService : IDisposable
    {
        public const string Languages = "ind+eng";

        private readonly TesseractEngine engine;

        /// <summary>null kalau komponen OCR tidak bisa dimuat (alasan di <paramref name="error"/>).</summary>
        public static OcrService TryCreate(out string error)
        {
            error = null;
            try
            {
                string tessdata = Path.Combine(AppContext.BaseDirectory, "tessdata");
                if (!File.Exists(Path.Combine(tessdata, "ind.traineddata")) || !File.Exists(Path.Combine(tessdata, "eng.traineddata")))
                {
                    error = "OCR language data not found in " + tessdata;
                    return null;
                }
                return new OcrService(new TesseractEngine(tessdata, Languages, EngineMode.Default));
            }
            catch (Exception ex)
            {
                // Mis. Visual C++ Redistributable belum terpasang di server (DllNotFoundException)
                error = "OCR unavailable: " + (ex.InnerException ?? ex).GetType().Name + " - " + (ex.InnerException ?? ex).Message;
                return null;
            }
        }

        private OcrService(TesseractEngine engine)
        {
            this.engine = engine;
        }

        /// <summary>Teks dari gambar (PNG/JPG/TIFF/BMP dalam bentuk byte).</summary>
        public string Read(byte[] image)
        {
            using Pix pix = Pix.LoadFromMemory(image);
            using Page page = engine.Process(pix, PageSegMode.Auto);
            return page.GetText();
        }

        public void Dispose() => engine.Dispose();
    }
}
