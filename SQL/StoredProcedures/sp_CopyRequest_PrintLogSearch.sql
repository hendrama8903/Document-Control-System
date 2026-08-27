SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- Rincian eksekusi cetak per baris dokumen, dipakai panel monitoring
-- "Print" di web CopyRequest/Index (request Hendra 2026-08-15). Satu baris
-- dokumen bisa punya banyak percobaan cetak (termasuk yang gagal - sengaja
-- ikut ditampilkan untuk audit, bukan cuma yang sukses) - makanya LEFT JOIN
-- ke TB_R_COPY_REQUEST_PRINT_LOG, dan dokumen yang belum pernah dicetak
-- sama sekali tetap muncul (kolom log-nya NULL).
--
-- Ada 2 kolom "PRINT_STATUS" dengan domain berbeda: token sekali-pakai di
-- _D ('0'/'1') vs hasil eksekusi di _PRINT_LOG ('Success'/'Failed'/
-- 'Cancelled') - dialiaskan DETAIL_PRINT_STATUS / LOG_PRINT_STATUS supaya
-- tidak ketuker di sisi C#.
CREATE OR ALTER PROCEDURE [dbo].[sp_CopyRequest_PrintLogSearch]
	@REQUEST_ID INT
AS
BEGIN
	SET NOCOUNT ON;

	SELECT
		D.REQUEST_DETAIL_ID,
		D.LINE_NO,
		D.DOCUMENT_CODE,
		D.DOCUMENT_NAME,
		D.COPY_QTY,
		D.PRINT_STATUS AS DETAIL_PRINT_STATUS,
		L.PRINT_LOG_ID,
		L.COMPUTER_NAME,
		L.PRINTER_NAME,
		L.PAGE_COUNT,
		L.COPY_COUNT,
		L.TOTAL_SHEETS,
		L.PRINT_STATUS AS LOG_PRINT_STATUS,
		L.ERROR_DETAIL,
		L.PRINTED_BY,
		L.PRINTED_DT
	FROM [dbo].[TB_R_COPY_REQUEST_D] D
	LEFT JOIN [dbo].[TB_R_COPY_REQUEST_PRINT_LOG] L ON L.REQUEST_DETAIL_ID = D.REQUEST_DETAIL_ID
	WHERE D.REQUEST_ID = @REQUEST_ID AND D.DELETE_FLAG = 0
	ORDER BY D.LINE_NO ASC, L.PRINT_LOG_ID ASC;
END
GO
