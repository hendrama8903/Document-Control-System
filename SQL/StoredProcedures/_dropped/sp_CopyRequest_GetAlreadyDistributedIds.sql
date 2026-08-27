SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- Daftar DOCUMENT_TRANSACTION_ID yang SUDAH terdistribusi resmi (P4D Send
-- Distribution, STATUS=1) ke department/divisi milik @USERNAME - dipakai
-- CopyRequestController.SearchApprovedDocuments untuk menyembunyikan
-- dokumen itu dari document picker "Add" Copy Request, supaya user tidak
-- minta salinan untuk dokumen yang departemennya sudah terima lewat jalur
-- distribusi normal. Pola pengecekan sama persis dengan
-- sp_UserDashboard_Request (request Hendra 2026-08-14).
CREATE OR ALTER PROCEDURE [dbo].[sp_CopyRequest_GetAlreadyDistributedIds]
	@USERNAME VARCHAR(255)
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @DepartmentCount INT;
	SELECT @DepartmentCount = COUNT(DEPARTMENT_ID) FROM TB_M_USER_POS WHERE USERNAME = @USERNAME;

	IF @DepartmentCount > 0
	BEGIN
		SELECT DISTINCT DD.DOCUMENT_TRANSACTION_ID
		FROM TB_R_DOCUMENT_DISTRIBUTION DD
		WHERE DD.STATUS = 1
		AND DD.DEPARTMENT_ID IN (SELECT DEPARTMENT_ID FROM TB_M_USER_POS WHERE USERNAME = @USERNAME);
	END
	ELSE
	BEGIN
		SELECT DISTINCT DD.DOCUMENT_TRANSACTION_ID
		FROM TB_R_DOCUMENT_DISTRIBUTION DD
		WHERE DD.STATUS = 1
		AND DD.DEPARTMENT_ID IN (
			SELECT DEPARTMENT_ID FROM TB_M_DEPARTMENT
			WHERE DIVISION IN (SELECT DIVISION FROM TB_M_USER_POS WHERE USERNAME = @USERNAME)
		);
	END
END
GO
