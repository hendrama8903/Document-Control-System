-- =====================================================================
-- Full-text search isi dokumen (request Hendra 2026-11-06, terinspirasi Folderit)
--
-- SQL Server Express di sini tidak punya fitur Full-Text Search, jadi teks isi
-- dokumen diekstrak oleh aplikasi (Services/DocumentContentIndexer: Excel lewat
-- DevExpress Spreadsheet, PDF lewat PdfDocumentProcessor, Word lewat RichEdit)
-- ke TB_R_DOCUMENT_CONTENT, lalu dicari dengan sp_DocumentSearch.
--
-- Pengindeksan berjalan sebagai job Hangfire berkala "document-content-index"
-- (tiap 5 menit): mengambil dokumen yang belum terindeks atau file-nya berganti.
-- Dokumen lama otomatis ikut terindeks pada putaran pertama.
--
-- Pencarian: kotak "Search documents" di top bar -> /Search?q=...
--
-- Idempotent. Jalankan di DMS_NEW, lalu sp_DocumentContent_Pending.sql,
-- sp_DocumentContent_Save.sql, sp_DocumentSearch.sql.
-- =====================================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID('dbo.TB_R_DOCUMENT_CONTENT', 'U') IS NULL
BEGIN
	CREATE TABLE dbo.TB_R_DOCUMENT_CONTENT (
		SOURCE_TYPE   VARCHAR(10)    NOT NULL,   -- INTERNAL (TB_R_DOCUMENT) / EXTERNAL (TB_M_EXTERNAL_DOCUMENT)
		SOURCE_ID     INT            NOT NULL,   -- DOCUMENT_TRANSACTION_ID / EXTERNAL_DOCUMENT_ID
		FILE_PATH     VARCHAR(500)   NOT NULL,   -- file yang diindeks; beda dengan file dokumen = perlu indeks ulang
		CONTENT       NVARCHAR(MAX)  NULL,
		CHAR_COUNT    INT            NULL,
		INDEX_STATUS  VARCHAR(10)    NOT NULL,   -- OK / EMPTY / ERROR / MISSING
		ERROR_MSG     VARCHAR(500)   NULL,
		INDEXED_DT    DATETIME       NOT NULL CONSTRAINT DF_TB_R_DOCUMENT_CONTENT_DT DEFAULT (GETDATE()),
		CONSTRAINT PK_TB_R_DOCUMENT_CONTENT PRIMARY KEY (SOURCE_TYPE, SOURCE_ID)
	);
END
GO

-- OCR (tahap 1, 2026-11-06): metode ekstraksi teks - TEXT (teks digital), OCR (semua
-- halaman hasil scan dibaca OCR), TEXT+OCR (campuran), TEXT/NO-OCR (ada halaman scan
-- tetapi komponen OCR tidak tersedia di server; dicoba lagi tiap hari).
IF COL_LENGTH('dbo.TB_R_DOCUMENT_CONTENT', 'EXTRACT_METHOD') IS NULL
	ALTER TABLE dbo.TB_R_DOCUMENT_CONTENT ADD EXTRACT_METHOD VARCHAR(20) NULL;
IF COL_LENGTH('dbo.TB_R_DOCUMENT_CONTENT', 'OCR_PAGES') IS NULL
	ALTER TABLE dbo.TB_R_DOCUMENT_CONTENT ADD OCR_PAGES INT NULL;
GO
