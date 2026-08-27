SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- Daftar status "Mengetahui" Related Division per dokumen (SPR/SIPOCOR Level
-- 2) - dipakai panel "Related Division Acknowledgment" di modal Approval List
-- (request Hendra 2026-08-20). Terpisah dari sp_Approval_GetDetail karena
-- acknowledgment ini sudah tidak lagi bagian dari TB_R_APPROVAL_D.
CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentMaintenance_GetRelatedDivisionAckStatus]
	@DOCUMENT_TRANSACTION_ID INT
AS
BEGIN
	SET NOCOUNT ON;

	-- CREATED_BY/CREATED_DT wajib ikut di-SELECT meski tidak dipakai UI -
	-- FromSqlRaw<DocumentRelatedDivision> mewajibkan SEMUA properti model ada
	-- di hasil query (pola bug yang sama sudah terjadi 2x di sesi ini, lihat
	-- juga model DocumentRelatedDivision.cs).
	SELECT
		RD.RELATED_DIVISION_ID,
		RD.DOCUMENT_TRANSACTION_ID,
		RD.DIVISION_CODE,
		RD.DIVISION_ROLE,
		RD.CREATED_BY,
		RD.CREATED_DT,
		ISNULL(RD.DIVISION_NAME, DIV.DIVISION_NAME) AS DIVISION_NAME,
		HEAD.USERNAME AS HEAD_USERNAME,
		HEADU.FULL_NAME AS HEAD_FULL_NAME,
		RD.ACKNOWLEDGED_FLAG,
		RD.ACKNOWLEDGED_BY,
		RD.ACKNOWLEDGED_DT
	FROM [dbo].[TB_R_DOCUMENT_RELATED_DIVISION] RD
	LEFT JOIN [dbo].[TB_M_DIVISION] DIV ON DIV.DIVISION_CODE = RD.DIVISION_CODE
	OUTER APPLY (
		SELECT TOP 1 USERNAME
		FROM [dbo].[TB_M_USER_POS]
		WHERE POSITION_ID = 4 AND DIVISION = RD.DIVISION_CODE
	) HEAD
	LEFT JOIN [dbo].[TB_M_USER] HEADU ON HEADU.USERNAME = HEAD.USERNAME
	WHERE RD.DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID
		-- Cuma peran RELATED yang wajib "Mengetahui" (request Hendra 2026-08-20) -
		-- MAIN_PIC/NOTE_RELATED sengaja tidak dimasukkan panel ini, karena tidak
		-- ada aksi Acknowledge buat mereka (lihat DocumentMaintenance_
		-- RelatedDivision_Role_Migration.sql).
		AND RD.DIVISION_ROLE = 'RELATED'
	ORDER BY RD.DIVISION_CODE
END
GO
