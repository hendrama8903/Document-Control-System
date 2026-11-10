SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- Grid Document Category: semua kategori, termasuk yang nonaktif. Request Hendra
-- 2026-11-07. Terpisah dari sp_DocumentMaster_Search, yang dipakai banyak layar dan
-- harus tetap hanya mengembalikan kategori aktif.
-- 2026-11-09: kategori yang di-Delete (DELETE_FLAG = 2) tidak ditampilkan; IS_USED
-- menandai kategori yang sudah dipakai, sehingga hanya bisa di-Deactivate (lihat
-- sp_DocumentMaster_Remove untuk daftar pemakaian yang sama).
CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentMaster_SearchWithStatus]
AS
BEGIN
	SET NOCOUNT ON;

	SELECT
		D.DOCUMENT_ID,
		D.DOCUMENT_CODE,
		D.DOCUMENT_NAME,
		D.LEVEL,
		S.SYSTEM_VALUE AS LEVEL_VAL,
		D.FILE_PATH,
		D.REVIEW_CYCLE_MONTHS,
		ISNULL(D.DELETE_FLAG, 0) AS DELETE_FLAG,
		D.CREATED_DT,
		D.CREATED_BY,
		D.CHANGED_DT,
		D.CHANGED_BY,
		CASE WHEN EXISTS (SELECT 1 FROM dbo.TB_R_DOCUMENT X WHERE X.DOCUMENT_ID = D.DOCUMENT_ID)
			   OR EXISTS (SELECT 1 FROM dbo.TB_R_DOCUMENT_HISTORY X WHERE X.DOCUMENT_ID = D.DOCUMENT_ID)
			   OR EXISTS (SELECT 1 FROM dbo.TB_M_WORKFLOW_DOC_H X WHERE X.DOCUMENT_ID = D.DOCUMENT_ID)
			   OR EXISTS (SELECT 1 FROM dbo.TB_M_EXCEL_TEMPLATE X WHERE X.DOCUMENT_ID = D.DOCUMENT_ID)
			 THEN 1 ELSE 0 END AS IS_USED
	FROM dbo.TB_M_DOCUMENT D
	LEFT JOIN dbo.TB_M_SYSTEM S ON S.SYSTEM_TYPE = 'LEVEL_CODE' AND S.SYSTEM_CODE = D.LEVEL
	WHERE ISNULL(D.DELETE_FLAG, 0) <> 2
	ORDER BY ISNULL(D.DELETE_FLAG, 0), D.DOCUMENT_CODE;
END
GO
