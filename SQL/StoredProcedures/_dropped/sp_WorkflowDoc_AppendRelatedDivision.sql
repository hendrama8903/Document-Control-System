SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- Menambahkan satu langkah "Mengetahui" di EKOR approval chain yang sudah
-- dibuat sp_WorkflowDoc_Create - dipakai fitur "Divisi Terkait" dokumen SPR
-- (SIPOCOR) Level 2 (request Hendra 2026-08-20). Dipanggil sekali per divisi
-- terkait yang dipilih creator, dari controller, dalam transaksi yang sama
-- dengan pembuatan dokumen.
--
-- Reuse TOTAL mekanisme approval yang sudah ada (TB_R_APPROVAL_D/H) - tidak
-- ada tabel/status approval terpisah. sp_Approval_Approve menandai seluruh
-- APPROVAL_H selesai (APPROVAL_STATUS=1) begitu WORKFLOW_SEQ yang di-approve
-- = MAX(WORKFLOW_SEQ) utk APPROVAL_ID itu - karena langkah "Mengetahui" ini
-- SELALU di-insert dengan WORKFLOW_SEQ lebih besar dari seluruh langkah
-- normal, dokumen otomatis tidak bisa Approved sebelum SEMUA divisi terkait
-- selesai Mengetahui, tanpa perlu logika blocking tambahan di manapun.
CREATE OR ALTER PROCEDURE [dbo].[sp_WorkflowDoc_AppendRelatedDivision]
	@APPROVAL_ID		INT,
	@DIVISION_CODE	VARCHAR(10),
	@LOGIN_USER 		VARCHAR(255),
	@RETURN_MSG 		VARCHAR(MAX) OUTPUT
AS
BEGIN TRY
	SET NOCOUNT ON;

	DECLARE @APPROVER VARCHAR(50), @NEXT_SEQ INT;

	-- Kepala Divisi = POSITION_ID 4 ("Div. Head", posisi generik yang sama
	-- dipakai lintas divisi - pola sama seperti TARGET_POSITION_ID Div Head
	-- di GeneratePengesahanPdf) di divisi terkait yang dipilih.
	SELECT TOP 1 @APPROVER = USERNAME
	FROM [dbo].[TB_M_USER_POS]
	WHERE POSITION_ID = 4
	AND DIVISION = @DIVISION_CODE

	IF @APPROVER IS NULL
	BEGIN
		SET @RETURN_MSG = 'ERROR: Div. Head belum terdaftar untuk divisi ' + @DIVISION_CODE + ' - tidak bisa menambahkan langkah Mengetahui.';
		RETURN 0;
	END

	SELECT @NEXT_SEQ = ISNULL(MAX(WORKFLOW_SEQ), 0) + 1
	FROM [dbo].[TB_R_APPROVAL_D]
	WHERE APPROVAL_ID = @APPROVAL_ID;

	INSERT INTO [dbo].[TB_R_APPROVAL_D] (
		[APPROVAL_ID],
		[WORKFLOW_SEQ],
		[APPROVER],
		[STATUS],
		[LABEL],
		[CREATED_BY],
		[CREATED_DT]
	)
	VALUES (
		@APPROVAL_ID,
		@NEXT_SEQ,
		@APPROVER,
		0,
		'Mengetahui',
		@LOGIN_USER,
		GETDATE()
	)

	-- Header mungkin sudah sempat dianggap selesai kalau seluruh langkah
	-- normal ternyata auto-approved (mis. creator = approver terakhir di
	-- chain) - buka lagi ke langkah "Mengetahui" yang baru ditambahkan.
	UPDATE [dbo].[TB_R_APPROVAL_H]
	SET [CURRENT_SEQ] = (SELECT MIN(WORKFLOW_SEQ) FROM [dbo].[TB_R_APPROVAL_D] WHERE APPROVAL_ID = @APPROVAL_ID AND [STATUS] = 0),
			[APPROVAL_STATUS] = 0,
			[CHANGED_BY] = @LOGIN_USER,
			[CHANGED_DT] = GETDATE()
	WHERE APPROVAL_ID = @APPROVAL_ID

	SET @RETURN_MSG = 'Successfully appended related-division step for ' + @DIVISION_CODE;
	RETURN 1;

END TRY
BEGIN CATCH
	SET @RETURN_MSG = 'ERROR: ' + ERROR_PROCEDURE() +': '+ ERROR_MESSAGE() + ', at line = ' +  CAST(ERROR_LINE() AS VARCHAR);
	RETURN 0;
END CATCH
GO
