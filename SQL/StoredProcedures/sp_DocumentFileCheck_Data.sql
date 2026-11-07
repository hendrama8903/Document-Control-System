SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- Data pembanding untuk cek file upload di Document Preparation (OCR tahap 2,
-- request Hendra 2026-11-06).
-- Result set 1: file revisi terakhir dari @REVISED_DOCUMENT_CODE (untuk deteksi file
--               yang identik dengan revisi sebelumnya). Kosong untuk dokumen baru.
-- Result set 2: semua nomor dokumen yang terdaftar (untuk deteksi file yang memuat
--               nomor dokumen lain - kemungkinan salah memilih file).
CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentFileCheck_Data]
	@REVISED_DOCUMENT_CODE VARCHAR(255) = NULL
AS
BEGIN
	SET NOCOUNT ON;

	SELECT TOP 1 D.DOCUMENT_TRANSACTION_ID, D.REVISION, D.FILE_PATH
	FROM dbo.TB_R_DOCUMENT D
	WHERE D.DOCUMENT_CODE = NULLIF(LTRIM(RTRIM(@REVISED_DOCUMENT_CODE)), '')
	AND ISNULL(D.DELETE_FLAG, 0) = 0 AND NULLIF(LTRIM(RTRIM(D.FILE_PATH)), '') IS NOT NULL
	ORDER BY D.REVISION DESC, D.DOCUMENT_TRANSACTION_ID DESC;

	SELECT DISTINCT LTRIM(RTRIM(D.DOCUMENT_CODE)) AS DOCUMENT_CODE
	FROM dbo.TB_R_DOCUMENT D
	WHERE ISNULL(D.DELETE_FLAG, 0) = 0 AND LEN(LTRIM(RTRIM(ISNULL(D.DOCUMENT_CODE, '')))) >= 6;
END
GO
