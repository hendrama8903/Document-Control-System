SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- Membuat approval chain (TB_R_APPROVAL_H/D) untuk satu dokumen.
--
-- 2026-11-06 (request Hendra, lihat SQL/WorkflowPerCategory_Migration.sql):
--  * @DOCUMENT_ID (opsional, parameter terakhir supaya pemanggil lama tetap
--    jalan): kalau Document Category punya alur khusus, alur itu yang dipakai;
--    kalau tidak, alur default per DOCUMENT_LEVEL seperti sebelumnya.
--  * Approver dicari sesuai TB_M_POSITION.APPROVER_SCOPE, bukan lagi IF per
--    POSITION_ID yang ditulis di sini.
--  * Langkah IS_MANDATORY = 1 yang approvernya tidak ditemukan membatalkan
--    pembuatan approval (sebelumnya semua langkah kosong dilewati diam-diam).
--    Langkah tidak wajib tetap dilewati seperti dulu.
--  * Pencarian langkah & approver ada di dbo.fn_WorkflowDoc_* - dipakai juga
--    sp_WorkflowDoc_Preview, jadi pratinjau = hasil sebenarnya.
CREATE OR ALTER PROCEDURE [dbo].[sp_WorkflowDoc_Create]
	@DOCUMENT_LEVEL 	INT,
	@LOGIN_USER 			VARCHAR(255),
	@REMARK			 			VARCHAR(MAX),
	@MENU_ID		 			VARCHAR(50),
	@DOCUMENT_CREATOR VARCHAR(50),
	@RETURN_MSG 			VARCHAR(MAX) OUTPUT,
	@RETURN_ID	 			VARCHAR(MAX) OUTPUT,
	@DOCUMENT_ID			INT = NULL
AS
BEGIN TRY
	SET NOCOUNT ON;
	SET @RETURN_ID = 0;

	DECLARE @CREATOR_LEVEL INT,
					@POSITION_NAME VARCHAR(255),
					@WORKFLOW_DOC_ID INT,
					@IS_CATEGORY_WORKFLOW BIT;

	SELECT @CREATOR_LEVEL = POSITION_LEVEL, @POSITION_NAME = POSITION_NAME
	FROM dbo.fn_WorkflowDoc_Creator(@DOCUMENT_CREATOR);

	IF @CREATOR_LEVEL IS NULL
	BEGIN
		SET @RETURN_MSG = 'ERROR: User Position for ' + ISNULL(@DOCUMENT_CREATOR, '(empty)') + ' not found.'
		RETURN 0;
	END

	SELECT @WORKFLOW_DOC_ID = WORKFLOW_DOC_ID, @IS_CATEGORY_WORKFLOW = IS_CATEGORY_WORKFLOW
	FROM dbo.fn_WorkflowDoc_FindHeader(@DOCUMENT_LEVEL, @DOCUMENT_ID, @CREATOR_LEVEL);

	IF @WORKFLOW_DOC_ID IS NULL
	BEGIN
		SET @RETURN_MSG = 'ERROR: Workflow for Document level ' + CAST(@DOCUMENT_LEVEL AS VARCHAR) + ' and creator level ' + @POSITION_NAME + ' not found.'
		RETURN 0;
	END

	-- Langkah alur + approver hasil pencarian. ACTUAL_SEQ = nomor urut yang
	-- benar-benar dipakai di TB_R_APPROVAL_D: rapat 1,2,3,... hanya untuk
	-- langkah yang approvernya ada (langkah tidak wajib yang kosong dilewati,
	-- request Hendra 2026-08-18) - kode lain (mis. pemetaan kotak tanda tangan
	-- STEPn di GeneratePengesahanPdf) berasumsi urutan rapat.
	DECLARE @STEPS TABLE (
		WORKFLOW_SEQ INT,
		POSITION_ID INT,
		POSITION_NAME VARCHAR(255),
		LABEL VARCHAR(255),
		IS_MANDATORY BIT,
		APPROVER VARCHAR(50),
		ACTUAL_SEQ INT NULL
	);

	INSERT INTO @STEPS (WORKFLOW_SEQ, POSITION_ID, POSITION_NAME, LABEL, IS_MANDATORY, APPROVER)
	SELECT WORKFLOW_SEQ, POSITION_ID, POSITION_NAME, LABEL, IS_MANDATORY, APPROVER
	FROM dbo.fn_WorkflowDoc_Steps(@WORKFLOW_DOC_ID, @DOCUMENT_CREATOR);

	-- Langkah wajib tanpa approver: batalkan SEBELUM apa pun ditulis
	DECLARE @MISSING VARCHAR(MAX);
	SELECT @MISSING = STUFF((
		SELECT ', ' + ISNULL(POSITION_NAME, 'Position ' + CAST(POSITION_ID AS VARCHAR))
		FROM @STEPS
		WHERE IS_MANDATORY = 1 AND APPROVER IS NULL
		ORDER BY WORKFLOW_SEQ
		FOR XML PATH(''), TYPE).value('.', 'VARCHAR(MAX)'), 1, 2, '');

	IF @MISSING IS NOT NULL
	BEGIN
		SET @RETURN_MSG = 'ERROR: Mandatory approver not found: ' + @MISSING
			+ '. No user holds this position for creator ' + @DOCUMENT_CREATOR
			+ ' - complete the User Position data first.';
		RETURN 0;
	END

	-- Jaring pengaman: kalau SEMUA posisi di chain ternyata kosong (org belum
	-- lengkap sama sekali), jangan buat approval header tanpa satupun approver -
	-- dokumen akan macet permanen di "Waiting Approval".
	IF NOT EXISTS (SELECT 1 FROM @STEPS WHERE APPROVER IS NOT NULL)
	BEGIN
		SET @RETURN_MSG = 'ERROR: No approver found for any step in this workflow (Document level ' + CAST(@DOCUMENT_LEVEL AS VARCHAR) + ', creator level ' + @POSITION_NAME + ') - all positions in the chain are unfilled.';
		RETURN 0;
	END

	;WITH N AS (
		SELECT ACTUAL_SEQ, ROW_NUMBER() OVER (ORDER BY WORKFLOW_SEQ) AS RN
		FROM @STEPS WHERE APPROVER IS NOT NULL
	)
	UPDATE N SET ACTUAL_SEQ = RN;

	-- Insert Approval Header
	DECLARE @APPROVAL_ID INT;

	INSERT INTO [dbo].[TB_R_APPROVAL_H] (
		[APPROVAL_DATE],
		[APPROVAL_STATUS],
		[CURRENT_SEQ],
		[CREATOR],
		[MENU_ID],
		[CREATED_BY],
		[CREATED_DT]
	)
	VALUES (
		GETDATE(),
		0,
		1,
		@LOGIN_USER,
		@MENU_ID,
		@LOGIN_USER,
		GETDATE()
	)

	IF @@ROWCOUNT = 0
	BEGIN
		SET @RETURN_MSG = 'ERROR: Create Workflow for Document level ' + CAST(@DOCUMENT_LEVEL AS VARCHAR) + ' and creator level ' + @POSITION_NAME
		RETURN 0;
	END

	SET @APPROVAL_ID = SCOPE_IDENTITY();

	-- Insert Approval Detail. Langkah pertama yang approvernya = pembuat
	-- langsung dianggap approve (auto-approve pembuat), membawa @REMARK.
	INSERT INTO [dbo].[TB_R_APPROVAL_D] (
		[APPROVAL_ID],
		[WORKFLOW_SEQ],
		[APPROVER],
		[STATUS],
		[REMARK],
		[LABEL],
		[CREATED_BY],
		[CREATED_DT],
		[CHANGED_BY],
		[CHANGED_DT]
	)
	SELECT
		@APPROVAL_ID,
		S.ACTUAL_SEQ,
		S.APPROVER,
		CASE WHEN S.ACTUAL_SEQ = 1 AND S.APPROVER = @DOCUMENT_CREATOR THEN 1 ELSE 0 END,
		CASE WHEN S.ACTUAL_SEQ = 1 AND S.APPROVER = @DOCUMENT_CREATOR THEN @REMARK ELSE NULL END,
		S.LABEL,
		@LOGIN_USER,
		GETDATE(),
		CASE WHEN S.ACTUAL_SEQ = 1 AND S.APPROVER = @DOCUMENT_CREATOR THEN @DOCUMENT_CREATOR ELSE NULL END,
		CASE WHEN S.ACTUAL_SEQ = 1 AND S.APPROVER = @DOCUMENT_CREATOR THEN GETDATE() ELSE NULL END
	FROM @STEPS S
	WHERE S.ACTUAL_SEQ IS NOT NULL
	ORDER BY S.ACTUAL_SEQ;

	-- Update Approval Header: lanjut ke seq pending berikutnya
	IF EXISTS (SELECT 1 FROM @STEPS WHERE ACTUAL_SEQ = 1 AND APPROVER = @DOCUMENT_CREATOR)
	BEGIN
		DECLARE @NEXT_SEQ INT;
		SELECT @NEXT_SEQ = MIN(WORKFLOW_SEQ)
		FROM [dbo].[TB_R_APPROVAL_D]
		WHERE APPROVAL_ID = @APPROVAL_ID AND [STATUS] = 0;

		UPDATE [dbo].[TB_R_APPROVAL_H]
		SET [CURRENT_SEQ] = ISNULL(@NEXT_SEQ, [CURRENT_SEQ] + 1),
				[CHANGED_BY] = @LOGIN_USER,
				[CHANGED_DT] = GETDATE()
		WHERE APPROVAL_ID = @APPROVAL_ID
	END

	SET @RETURN_ID = @APPROVAL_ID;
	SET @RETURN_MSG = 'Successfully Create Workflow for Document level ' + CAST(@DOCUMENT_LEVEL AS VARCHAR) + ' and creator level ' + @POSITION_NAME
		+ CASE WHEN @IS_CATEGORY_WORKFLOW = 1 THEN ' (category workflow)' ELSE '' END;
	RETURN 1;

END TRY
BEGIN CATCH
	SET @RETURN_MSG = 'ERROR: ' + ERROR_PROCEDURE() +': '+ ERROR_MESSAGE() + ', at line = ' +  CAST(ERROR_LINE() AS VARCHAR);
	RETURN 0;
END CATCH
GO
