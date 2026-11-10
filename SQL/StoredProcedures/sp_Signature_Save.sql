SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- Simpan tanda tangan elektronik satu orang untuk satu dokumen/langkah
-- (lihat SQL/ElectronicSignature_Migration.sql). Dipanggil di dalam transaksi
-- Save / Approve / Mengetahui, sebelum aksi itu sendiri dijalankan - kalau aksinya
-- gagal, tanda tangannya ikut dibatalkan. Tanda tangan ulang untuk langkah yang
-- sama (mis. pembuat menyimpan ulang) menggantikan yang lama.
CREATE OR ALTER PROCEDURE [dbo].[sp_Signature_Save]
	@KIND                     VARCHAR(10),
	@DOCUMENT_TRANSACTION_ID  INT,
	@REF_KEY                  VARCHAR(50),
	@SIGNER                   VARCHAR(50),
	@SIGN_METHOD              VARCHAR(10),
	@SIGN_DATA                NVARCHAR(MAX),
	@SIGN_FONT                VARCHAR(50),
	@CLIENT_IP                VARCHAR(64),
	@USER_AGENT               VARCHAR(500),
	@DOCUMENT_FILE_HASH       VARCHAR(64),
	@RETURN_MSG               VARCHAR(MAX) OUTPUT
AS
BEGIN
	SET NOCOUNT ON;
	SET @REF_KEY = ISNULL(@REF_KEY, '');

	-- QR & UPLOAD ditambahkan 2026-11-09 (master setting metode tanda tangan).
	IF @KIND NOT IN ('CREATOR', 'APPROVAL', 'ACK') OR @SIGN_METHOD NOT IN ('DRAW', 'TYPE', 'QR', 'UPLOAD')
		OR @SIGNER IS NULL OR @DOCUMENT_TRANSACTION_ID IS NULL OR LEN(ISNULL(@SIGN_DATA, '')) = 0
	BEGIN
		SET @RETURN_MSG = 'ERROR: Invalid signature data.';
		RETURN 0;
	END

	DECLARE @SIGNER_NAME VARCHAR(255), @SIGNER_POSITION VARCHAR(255);
	SELECT @SIGNER_NAME = FULL_NAME FROM dbo.TB_M_USER WHERE USERNAME = @SIGNER;
	SELECT @SIGNER_POSITION = POSITION_NAME FROM dbo.fn_WorkflowDoc_Creator(@SIGNER);

	DELETE FROM dbo.TB_R_SIGNATURE
	WHERE KIND = @KIND AND DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID AND REF_KEY = @REF_KEY;

	INSERT INTO dbo.TB_R_SIGNATURE
		(KIND, DOCUMENT_TRANSACTION_ID, REF_KEY, SIGNER, SIGNER_NAME, SIGNER_POSITION,
		 SIGN_METHOD, SIGN_DATA, SIGN_FONT, SIGNED_DT, CLIENT_IP, USER_AGENT, DOCUMENT_FILE_HASH)
	VALUES
		(@KIND, @DOCUMENT_TRANSACTION_ID, @REF_KEY, @SIGNER, @SIGNER_NAME, @SIGNER_POSITION,
		 @SIGN_METHOD, @SIGN_DATA, @SIGN_FONT, GETDATE(), @CLIENT_IP, LEFT(@USER_AGENT, 500), @DOCUMENT_FILE_HASH);

	SET @RETURN_MSG = 'Signature saved';
	RETURN 1;
END
GO
