-- =====================================================================
-- Fitur baru: "Document Receipt Report" (Tanda Terima Dokumen) - list +
-- cetak PDF untuk dokumen distribusi P4D yang sudah dikonfirmasi diterima
-- oleh department tujuan (lewat tombol Acknowledge di UserDashboard,
-- yang insert baris ke TB_R_PUBLISH_HISTORY).
--
-- Formatnya mengikuti form QMS Hino "TANDA TERIMA DOKUMEN"
-- (QCD/FR-QMS-00/006): No, No. Dokumen, Nama Dokumen, No. Revisi,
-- Diterima Oleh (Tanggal/Div.Dept.OF/Nama/Paraf), Penarikan Dokumen
-- Lama (Ya/Tidak/Jumlah), Keterangan, Paraf QMS.
--
-- sp_DocumentReceiptReport_Search: 1 baris = 1 record TB_R_PUBLISH_HISTORY
-- (1 dokumen x 1 department yang sudah acknowledge), di-join ke data
-- dokumen (nama, revisi) dan nama lengkap yang meng-acknowledge.
--
-- Menu baru M00008-03 di bawah "Reports" (M00008), tidak ada Function
-- permission terpisah (mengikuti pola Document Report/Document
-- Masterlist - view-only, cukup akses menu), digrant ke DMS-ADMIN.
--
-- Idempotent - aman dijalankan ulang.
-- Jalankan di database DMS_NEW
-- =====================================================================

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- 2026-11-19: rewritten as a parameterised query (the old version pasted @DOCUMENT_CODE
-- into dynamic SQL = SQL injection) and returns the receiving division, used by the
-- form header of the Excel / PDF print (same style as the Master List).
CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentReceiptReport_Search]
	@DOCUMENT_CODE 	VARCHAR(255),
	@DEPARTMENT_ID 	INT,
	@PageNumber 	INT,
	@PageSize 		INT
AS
BEGIN
	SET NOCOUNT ON;

	-- '*' stays the wildcard of the search box
	DECLARE @CODE_PATTERN VARCHAR(255) = REPLACE(@DOCUMENT_CODE, '*', '%');

	WITH data AS
	(
		SELECT
			ROW_NUMBER() OVER (ORDER BY PH.CREATED_DT DESC, PH.PUBLISH_HISTORY_ID DESC) AS RowNumber,
			PH.PUBLISH_HISTORY_ID,
			PH.DOCUMENT_CTRL_ID,
			C.DOCUMENT_CODE,
			C.DOCUMENT_NAME,
			ISNULL(C.REVISION, 0) REVISION,
			PH.DEPARTMENT_ID,
			ISNULL(PH.DEPARTMENT_CODE, D.DEPARTMENT_CODE) AS DEPARTMENT_CODE,
			ISNULL(PH.DEPARTMENT_NAME, D.DEPARTMENT_NAME) AS DEPARTMENT_NAME,
			D.DIVISION AS DIVISION,
			DV.DIVISION_NAME AS DIVISION_NAME,
			PH.CREATED_DT RECEIVED_DATE,
			PH.CREATED_BY RECEIVED_BY,
			ISNULL(U.FULL_NAME, PH.CREATED_BY) RECEIVED_BY_NAME
		FROM [dbo].[TB_R_PUBLISH_HISTORY] PH
		JOIN [dbo].[TB_R_CTRL_DOCUMENT] C ON C.DOCUMENT_CTRL_ID = PH.DOCUMENT_CTRL_ID
		JOIN [dbo].[TB_M_DEPARTMENT] D ON D.DEPARTMENT_ID = PH.DEPARTMENT_ID
		LEFT JOIN [dbo].[TB_M_DIVISION] DV ON DV.DIVISION_CODE = D.DIVISION
		LEFT JOIN [dbo].[TB_M_USER] U ON U.USERNAME = PH.CREATED_BY
		WHERE (@CODE_PATTERN IS NULL OR C.DOCUMENT_CODE LIKE @CODE_PATTERN)
		  AND (@DEPARTMENT_ID IS NULL OR PH.DEPARTMENT_ID = @DEPARTMENT_ID)
	)
	SELECT * FROM data
	WHERE (@PageSize IS NULL OR @PageNumber IS NULL)
	   OR (RowNumber > @PageSize * (@PageNumber - 1) AND RowNumber <= @PageSize * @PageNumber)
	ORDER BY RowNumber;
END
GO
