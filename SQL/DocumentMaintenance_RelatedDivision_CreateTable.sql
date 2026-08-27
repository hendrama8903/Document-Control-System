-- =====================================================================
-- Fitur "Divisi Terkait" untuk dokumen SPR (SIPOCOR) Level 2 - creator
-- memilih divisi lain yang perlu "Mengetahui" (acknowledge) dokumen ini,
-- selain divisi pembuat sendiri (Main PIC, diturunkan dari
-- TB_R_DOCUMENT.DIVISION, tidak perlu disimpan di sini).
--
-- Approval untuk tiap divisi terkait di-APPEND ke ekor approval chain
-- normal (TB_R_APPROVAL_D) - lihat sp_WorkflowDoc_Create dan
-- DocumentMaintenanceController - bukan mekanisme approval terpisah.
-- Tabel ini murni catatan "divisi apa saja yang dipilih", bukan status
-- approval-nya sendiri (status Mengetahui dibaca dari TB_R_APPROVAL_D).
--
-- Idempotent - aman dijalankan ulang.
-- Jalankan di database DMS_NEW
-- =====================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TB_R_DOCUMENT_RELATED_DIVISION')
BEGIN
	CREATE TABLE [dbo].[TB_R_DOCUMENT_RELATED_DIVISION] (
		[RELATED_DIVISION_ID]     INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
		[DOCUMENT_TRANSACTION_ID] INT          NOT NULL,
		[DIVISION_CODE]           VARCHAR(10)  NOT NULL,
		[CREATED_BY]              VARCHAR(50)  NULL,
		[CREATED_DT]              DATETIME     NULL,
		CONSTRAINT [FK_DOC_RELATED_DIVISION_DOCUMENT] FOREIGN KEY ([DOCUMENT_TRANSACTION_ID])
			REFERENCES [dbo].[TB_R_DOCUMENT] ([DOCUMENT_TRANSACTION_ID])
			ON DELETE CASCADE
	);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DOC_RELATED_DIVISION_TRANSACTION_ID')
BEGIN
	CREATE INDEX [IX_DOC_RELATED_DIVISION_TRANSACTION_ID] ON [dbo].[TB_R_DOCUMENT_RELATED_DIVISION] ([DOCUMENT_TRANSACTION_ID]);
END
GO
