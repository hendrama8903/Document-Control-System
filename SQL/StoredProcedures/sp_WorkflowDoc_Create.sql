SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_WorkflowDoc_Create]
	@DOCUMENT_LEVEL 	INT,
	@LOGIN_USER 			VARCHAR(255),
	@REMARK			 			VARCHAR(MAX),
	@MENU_ID		 			VARCHAR(50),
	@DOCUMENT_CREATOR VARCHAR(50),
	@RETURN_MSG 			VARCHAR(MAX) OUTPUT,
	@RETURN_ID	 			VARCHAR(MAX) OUTPUT
AS
BEGIN TRY
	SET NOCOUNT ON;
	SET @RETURN_ID = 0;

	DECLARE @CREATOR_LEVEL INT,
					@CREATOR_POSITION_ID INT,
					@POSITION_NAME VARCHAR(255),
					@DIVISION VARCHAR(50),
					@DEPARTMENT_ID INT,
					@SECTION_ID INT,
					@WORKFLOW_DOC_ID INT;

	-- Get Creator Level
	SELECT TOP 1
		@CREATOR_LEVEL = A.POSITION_LEVEL,
		@CREATOR_POSITION_ID = A.POSITION_ID,
		@POSITION_NAME = A.POSITION_NAME,
		@DIVISION = B.DIVISION,
		@DEPARTMENT_ID = B.DEPARTMENT_ID,
		@SECTION_ID = B.SECTION_ID
	FROM TB_M_POSITION A
	JOIN TB_M_USER_POS B ON B.POSITION_ID = A.POSITION_ID
	WHERE B.USERNAME = @DOCUMENT_CREATOR

	IF @CREATOR_LEVEL IS NULL
	BEGIN
		SET @RETURN_MSG = 'ERROR: User Position for ' + @DOCUMENT_CREATOR + ' not found.'
		RETURN 0;
	END

	-- Get Workflow Doc
	SELECT @WORKFLOW_DOC_ID = WORKFLOW_DOC_ID FROM TB_M_WORKFLOW_DOC_H
	WHERE DOCUMENT_LEVEL = @DOCUMENT_LEVEL AND CREATOR_LEVEL = @CREATOR_LEVEL

	IF @WORKFLOW_DOC_ID IS NULL
	BEGIN
		SET @RETURN_MSG = 'ERROR: Workflow for Document level ' + CAST(@DOCUMENT_LEVEL AS VARCHAR) + ' and creator level ' + @POSITION_NAME + ' not found.'
		RETURN 0;
	END
	-- Insert Approval Header
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

	-- Insert Approval Detail
	IF @@ROWCOUNT > 0
	BEGIN
		DECLARE @APPROVAL_ID INT,
						@DETAIL_CURSOR CURSOR,
						@WORKFLOW_SEQ INT,
						@POSITION_ID INT,
						@LABEL VARCHAR(255),
						@APPROVER VARCHAR(50),
						@STATUS INT,
						@UPDATE_FLAG INT = 0,
						-- Nomor urut SESUNGGUHNYA yang dipakai di TB_R_APPROVAL_D, terpisah
						-- dari @WORKFLOW_SEQ master (TB_M_WORKFLOW_DOC_D) - cuma naik untuk
						-- langkah yang BENAR-BENAR di-insert (lihat blok skip-jika-kosong di
						-- bawah). Supaya WORKFLOW_SEQ hasil insert selalu rapat 1,2,3,... tanpa
						-- lubang, walau ada langkah yang dilewati - kode lain yang berasumsi
						-- nomor urut berurutan (mis. pemetaan kotak tanda tangan approver di
						-- GeneratePengesahanPdf) tetap benar tanpa perlu tahu ada langkah yang
						-- dilewati (request Hendra 2026-08-18, generalisasi skip-logic).
						@ACTUAL_SEQ INT = 0;

		SET @APPROVAL_ID = SCOPE_IDENTITY();

		SET @DETAIL_CURSOR = CURSOR FOR
		SELECT WORKFLOW_SEQ, POSITION_ID, LABEL FROM TB_M_WORKFLOW_DOC_D
		WHERE WORKFLOW_DOC_ID = @WORKFLOW_DOC_ID
		ORDER BY WORKFLOW_SEQ ASC

		OPEN @DETAIL_CURSOR
		FETCH NEXT FROM @DETAIL_CURSOR INTO @WORKFLOW_SEQ, @POSITION_ID, @LABEL

		WHILE @@FETCH_STATUS = 0
		BEGIN

			-- Reset supaya nilai approver iterasi sebelumnya tidak terbawa
			-- (SELECT @var = ... yang tidak menemukan baris TIDAK mengubah variabel)
			SET @APPROVER = NULL;

			-- Get Approver by position

			IF @POSITION_ID = 1 OR @POSITION_ID = 2 -- Staff, Section Head
			BEGIN
				IF @POSITION_ID = @CREATOR_POSITION_ID
				BEGIN
					SET @APPROVER = @DOCUMENT_CREATOR
				END
				ELSE
				BEGIN
					SELECT TOP 1 @APPROVER = USERNAME
					FROM TB_M_USER_POS
					WHERE POSITION_ID = @POSITION_ID
					AND DIVISION = @DIVISION
					AND DEPARTMENT_ID = @DEPARTMENT_ID
					AND SECTION_ID = @SECTION_ID
				END
			END
			ELSE IF @POSITION_ID = 3 -- Department Head
			BEGIN
				IF @POSITION_ID = @CREATOR_POSITION_ID
				BEGIN
					SET @APPROVER = @DOCUMENT_CREATOR
				END
				ELSE
				BEGIN
					SELECT TOP 1 @APPROVER = USERNAME
					FROM TB_M_USER_POS
					WHERE POSITION_ID = @POSITION_ID
					AND DIVISION = @DIVISION
					AND DEPARTMENT_ID = @DEPARTMENT_ID
				END
			END
			ELSE IF @POSITION_ID = 4 OR @POSITION_ID = 5 -- Div Head, EO
			BEGIN
				IF @POSITION_ID = @CREATOR_POSITION_ID
				BEGIN
					SET @APPROVER = @DOCUMENT_CREATOR
				END
				ELSE
				BEGIN
					SELECT TOP 1 @APPROVER = USERNAME
					FROM TB_M_USER_POS
					WHERE POSITION_ID = @POSITION_ID
					AND DIVISION = @DIVISION
				END
			END
			ELSE -- Dir, Presdir
			BEGIN
				SELECT TOP 1 @APPROVER = USERNAME
				FROM TB_M_USER_POS
				WHERE POSITION_ID = @POSITION_ID
			END

			IF @APPROVER IS NULL
			BEGIN
				-- Posisi kosong di division/department/section terkait: lewati
				-- langkah ini, workflow lanjut ke approver berikutnya - berlaku untuk
				-- SEMUA posisi (dulu cuma Section Head/POSITION_ID=2 yang di-skip,
				-- posisi lain malah error kalau usernya tidak ketemu; digeneralisasi
				-- request Hendra 2026-08-18 supaya org yang belum lengkap posisinya
				-- - mis. department tanpa Dept Head - tidak memblokir seluruh
				-- pembuatan dokumen).
				FETCH NEXT FROM @DETAIL_CURSOR INTO @WORKFLOW_SEQ, @POSITION_ID, @LABEL
				CONTINUE
			END

			DECLARE @CHANGED_BY VARCHAR(255), @CHANGED_DT DATETIME = NULL;

			SET @ACTUAL_SEQ = @ACTUAL_SEQ + 1;

			-- Check if Creator = Level 1 then auto approve - pakai @ACTUAL_SEQ (posisi
			-- SESUNGGUHNYA setelah langkah yang di-skip dirapatkan), bukan @WORKFLOW_SEQ
			-- master, supaya tetap benar walau langkah pertama di master (mis. Section
			-- Head) ternyata yang dilewati.
			IF @ACTUAL_SEQ = 1 AND @DOCUMENT_CREATOR = @APPROVER
			BEGIN
				SET @STATUS = 1;
				SET @UPDATE_FLAG = 1;
				SET @CHANGED_BY = @DOCUMENT_CREATOR;
				SET @CHANGED_DT = GETDATE();
			END
			ELSE BEGIN
				SET @STATUS = 0;
				SET @REMARK = NULL;
			END

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
			VALUES (
				@APPROVAL_ID,
				@ACTUAL_SEQ,
				@APPROVER,
				@STATUS,
				@REMARK,
				@LABEL,
				@LOGIN_USER,
				GETDATE(),
				@CHANGED_BY,
				@CHANGED_DT
			)

			FETCH NEXT FROM @DETAIL_CURSOR INTO @WORKFLOW_SEQ, @POSITION_ID, @LABEL
		END

		CLOSE @DETAIL_CURSOR
		DEALLOCATE @DETAIL_CURSOR

		-- Jaring pengaman: kalau SEMUA posisi di chain ternyata kosong (org belum
		-- lengkap sama sekali), jangan buat approval header tanpa satupun approver -
		-- dokumen akan macet permanen di "Waiting Approval" tanpa ada yang bisa
		-- memprosesnya.
		IF @ACTUAL_SEQ = 0
		BEGIN
			SET @RETURN_MSG = 'ERROR: No approver found for any step in this workflow (Document level ' + CAST(@DOCUMENT_LEVEL AS VARCHAR) + ', creator level ' + @POSITION_NAME + ') - all positions in the chain are unfilled.';
			RETURN 0;
		END

		-- Update Approval Header
		IF @UPDATE_FLAG = 1
		BEGIN
			-- Lanjut ke seq pending berikutnya yang benar-benar ada
			-- (bisa melompat kalau ada langkah yang dilewati)
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
		RETURN 1;

	END
	ELSE BEGIN

		SET @RETURN_MSG = 'ERROR: Create Workflow for Document level ' + CAST(@DOCUMENT_LEVEL AS VARCHAR) + ' and creator level ' + @POSITION_NAME
		RETURN 0;

	END

END TRY
BEGIN CATCH
	SET @RETURN_MSG = 'ERROR: ' + ERROR_PROCEDURE() +': '+ ERROR_MESSAGE() + ', at line = ' +  CAST(ERROR_LINE() AS VARCHAR);
	RETURN 0;
END CATCH
GO
