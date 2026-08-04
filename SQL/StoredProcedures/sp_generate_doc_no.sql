SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_generate_doc_no]
  @pDocLevel AS varchar(10),
	@pDivCode AS varchar(10),
	@pDeptCode AS varchar(10),
	@pSectionCode AS varchar(10),
	@pDocCode AS varchar(10),
  @pProcessCode AS varchar(10),
  @pCompanyCode AS varchar(10),
	@pDate AS DATE,
	@pManualSeq AS INT = NULL,
	@pDocNo AS VARCHAR (25) OUTPUT,
	@pReturnMsg AS VARCHAR(500) OUTPUT

AS
BEGIN
	DECLARE @vDocNo VARCHAR(50);
	DECLARE @vYear NUMERIC;
	DECLARE @vSeq1 int ;
	DECLARE @vSeqType VARCHAR(50);

	Set @vSeqType = 'DOC_NO';
	set @vSeq1 = 0;
	SET @pReturnMsg = NULL;

	-- Bangun prefix (belum termasuk 3 digit nomor urut) - persis pola lama, per level
	IF @pDocLevel = 1
	   BEGIN
	   SET @vYear = YEAR(@pDate);
		 SET @vDocNo = @pCompanyCode + '/' + @pDocCode + '/' + CAST (@vYear as VARCHAR) ;
		 END;
		ELSE
		IF @pDocLevel = 2
		   BEGIN
				 SET @vDocNo = @pCompanyCode + '/' + @pDocCode + '/' + @pProcessCode
			 END;
			 ELSE
		IF @pDocLevel = 4
		   BEGIN
			    SET @vDocNo = @pDivCode + '/'+  @pDocCode + '-' + @pDeptCode + '-' + @pSectionCode
			 END;
			 ELSE
			 IF @pDocLevel = 3
		   BEGIN
			    SET @vDocNo = @pDivCode + '/'+  @pDocCode + '-' + @pDeptCode + '-' + @pSectionCode
			 END;

	IF @pManualSeq IS NOT NULL
	BEGIN
		-- Manual (Jul 2026): user pilih sendiri nomor urutnya, mis. untuk
		-- mengisi nomor yang bolong karena draft lama dihapus. Wajib dicek
		-- belum dipakai dokumen aktif lain dengan prefix yang sama, lalu
		-- counter TB_M_SEQUENCE disamakan NAIK kalau nomor manual ini
		-- melebihi counter saat ini - supaya nomor "Generated" berikutnya
		-- untuk prefix yang sama tidak akan pernah tabrakan dengan nomor
		-- manual ini. Kalau nomor manual ini mengisi nomor bolong yang lebih
		-- kecil dari counter, counter TIDAK disentuh.
		IF @pManualSeq < 1 OR @pManualSeq > 999
		BEGIN
			SET @pReturnMsg = 'ERROR: Manual document number must be between 1 and 999.';
			RETURN;
		END

		SET @vDocNo = @vDocNo + '/' + RIGHT('000' + CAST (@pManualSeq as VARCHAR(3)),3);

		-- Concurrency guard (Aug 2026): without this, two callers requesting the same
		-- manual number at nearly the same time can both pass the "already in use"
		-- check below before either one has reserved it, and both go on to insert a
		-- live document with the identical DOCUMENT_CODE - reproduced and confirmed
		-- with a deliberately delayed test copy of this procedure (two concurrent
		-- sessions both got "ITD/SOP-APP-02/099" back with no error). sp_getapplock
		-- with @LockOwner='Transaction' ties the lock to the CALLER's transaction -
		-- sp_DocumentMaintenance_Insert always runs inside a transaction started by
		-- DocumentMaintenanceController, so the lock stays held through the actual
		-- INSERT INTO TB_R_DOCUMENT that happens later, back in the caller, not just
		-- for the duration of this procedure.
		IF @@TRANCOUNT = 0
		BEGIN
			SET @pReturnMsg = 'ERROR: Manual document numbering must run inside a transaction.';
			RETURN;
		END

		DECLARE @lockResult INT;
		EXEC @lockResult = sp_getapplock
			@Resource = @vDocNo,
			@LockMode = 'Exclusive',
			@LockOwner = 'Transaction',
			@LockTimeout = 10000;

		IF @lockResult < 0
		BEGIN
			SET @pReturnMsg = 'ERROR: Document number ' + @vDocNo + ' is currently being reserved by another request - please try again.';
			RETURN;
		END

		IF EXISTS (
			SELECT 1 FROM TB_R_DOCUMENT
			WHERE DOCUMENT_CODE = @vDocNo AND ISNULL(DELETE_FLAG, 0) = 0
		)
		BEGIN
			SET @pReturnMsg = 'ERROR: Document number ' + @vDocNo + ' is already in use.';
			RETURN;
		END

		DECLARE @seqPrefix VARCHAR(50) = LEFT(@vDocNo, LEN(@vDocNo) - 4);
		DECLARE @curSeq INT;

		SELECT @curSeq = SEQ_NO FROM TB_M_SEQUENCE WHERE SEQ_TYPE = @vSeqType AND SEQ_CODE = @seqPrefix;

		IF @curSeq IS NULL
		BEGIN
			INSERT INTO TB_M_SEQUENCE (SEQ_TYPE, SEQ_CODE, SEQ_NO, CREATED_BY, CREATED_DT, CHANGED_BY, CHANGED_DT)
			VALUES (@vSeqType, @seqPrefix, @pManualSeq, 'System', GETDATE(), 'System', GETDATE());
		END
		ELSE IF @pManualSeq > @curSeq
		BEGIN
			UPDATE TB_M_SEQUENCE
			SET SEQ_NO = @pManualSeq, CHANGED_BY = 'System', CHANGED_DT = GETDATE()
			WHERE SEQ_TYPE = @vSeqType AND SEQ_CODE = @seqPrefix;
		END

		SET @pDocNo = @vDocNo;
		RETURN;
	END

	-- Generated (Auto) - behavior lama, tidak berubah
	EXECUTE [dbo].[sp_GetNextSeqNo]
				 			 @SEQ_TYPE  = 'DOC_NO',
						 @SEQ_CODE  = @vDocNo,
	           @LOGIN_USER = 'System',
						 @p_seq_no = @vseq1 OUTPUT

	SET @vDocNo = @vDocNo + '/' +  RIGHT('000' + CAST (@vSeq1 as VARCHAR(3)),3) ;

	set @pDocNo = @vDocNo;
END
GO
