SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- Grid Document Category: semua kategori, termasuk yang nonaktif. Request Hendra
-- 2026-11-07. Terpisah dari sp_DocumentMaster_Search, yang dipakai banyak layar dan
-- harus tetap hanya mengembalikan kategori aktif.
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
		D.CHANGED_BY
	FROM dbo.TB_M_DOCUMENT D
	LEFT JOIN dbo.TB_M_SYSTEM S ON S.SYSTEM_TYPE = 'LEVEL_CODE' AND S.SYSTEM_CODE = D.LEVEL
	ORDER BY ISNULL(D.DELETE_FLAG, 0), D.DOCUMENT_CODE;
END
GO
