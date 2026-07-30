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

CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentReceiptReport_Search]
	@DOCUMENT_CODE 	VARCHAR(255),
	@DEPARTMENT_ID 	INT,
	@PageNumber 		INT,
	@PageSize 			INT
AS
BEGIN

	DECLARE @QUERY VARCHAR(MAX)

	SET @QUERY = 'WITH data AS
								(
									SELECT
										ROW_NUMBER() OVER (ORDER BY PH.CREATED_DT DESC) as RowNumber,
										PH.PUBLISH_HISTORY_ID,
										PH.DOCUMENT_CTRL_ID,
										C.DOCUMENT_CODE,
										C.DOCUMENT_NAME,
										ISNULL(C.REVISION, 0) REVISION,
										PH.DEPARTMENT_ID,
										D.DEPARTMENT_CODE,
										D.DEPARTMENT_NAME,
										PH.CREATED_DT RECEIVED_DATE,
										PH.CREATED_BY RECEIVED_BY,
										ISNULL(U.FULL_NAME, PH.CREATED_BY) RECEIVED_BY_NAME
									FROM [dbo].[TB_R_PUBLISH_HISTORY] PH
									JOIN [dbo].[TB_R_CTRL_DOCUMENT] C ON C.DOCUMENT_CTRL_ID = PH.DOCUMENT_CTRL_ID
									JOIN [dbo].[TB_M_DEPARTMENT] D ON D.DEPARTMENT_ID = PH.DEPARTMENT_ID
									LEFT JOIN [dbo].[TB_M_USER] U ON U.USERNAME = PH.CREATED_BY
									WHERE 1 = 1 '

									IF @DOCUMENT_CODE IS NOT NULL
									BEGIN
										SET @QUERY += ' AND C.DOCUMENT_CODE LIKE ''' + REPLACE(@DOCUMENT_CODE , '*', '%') + ''' '
									END

									IF @DEPARTMENT_ID IS NOT NULL
									BEGIN
										SET @QUERY += ' AND PH.DEPARTMENT_ID = ''' + REPLACE(@DEPARTMENT_ID , '*', '') + ''' '
									END

	SET @QUERY += '
								)
								SELECT * FROM data
								WHERE 1 = 1 '

	IF (@PageSize IS NOT NULL AND @PageNumber IS NOT NULL)
	BEGIN
		SET @QUERY += ' AND RowNumber > '+ CAST((@PageSize * (@PageNumber - 1)) AS VARCHAR) +' AND RowNumber <= ' +CAST(@PageSize + (@PageSize * (@PageNumber - 1)) AS VARCHAR);
	END

	EXEC(@QUERY)

END
GO
