SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- Antrian cetak untuk PrintTrack (desktop) - baris Copy Request yang sudah
-- Approved dan token cetaknya (PRINT_STATUS) belum dipakai, di-scope ke
-- requester yang login (setiap orang cetak requestnya sendiri di PC
-- masing-masing, request Hendra 2026-08-15). Menggantikan alur lama di mana
-- desktop sendiri yang membuat request dan menyimpan requestId-nya secara
-- lokal (%LOCALAPPDATA%\PrintTrack\requests.json) - sekarang request lahir
-- di web jadi desktop butuh cara menemukan request yang siap dicetak tanpa
-- tahu requestId-nya terlebih dahulu.
CREATE OR ALTER PROCEDURE [dbo].[sp_CopyRequest_PrintQueue]
	@USERNAME VARCHAR(255)
AS
BEGIN
	SET NOCOUNT ON;

	SELECT
		D.REQUEST_DETAIL_ID,
		D.REQUEST_ID,
		H.REQUEST_NO,
		D.LINE_NO,
		D.DOCUMENT_CODE,
		D.DOCUMENT_NAME,
		D.REVISION_NO,
		D.COPY_TYPE,
		COPYSYS.SYSTEM_VALUE AS COPY_TYPE_DISPLAY,
		D.COPY_QTY,
		H.APPROVED_BY,
		H.APPROVED_DT
	FROM [dbo].[TB_R_COPY_REQUEST_D] D
	INNER JOIN [dbo].[TB_R_COPY_REQUEST_H] H ON H.REQUEST_ID = D.REQUEST_ID
	LEFT JOIN [dbo].[TB_M_SYSTEM] COPYSYS ON COPYSYS.SYSTEM_TYPE = 'DOC_SUBMISSION_COPY_TYPE' AND COPYSYS.SYSTEM_CODE = D.COPY_TYPE
	WHERE H.STATUS = '2'
		AND D.PRINT_STATUS = '0'
		AND H.REQUESTED_BY = @USERNAME
		AND D.DELETE_FLAG = 0 AND H.DELETE_FLAG = 0
	ORDER BY H.APPROVED_DT DESC, D.LINE_NO ASC;
END
GO
