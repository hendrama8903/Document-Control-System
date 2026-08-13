-- =====================================================================
-- Copy Request: tambah pencatatan eksekusi cetak fisik dari PrintTrack
-- (aplikasi desktop terpisah, lihat project PrintTrack) - request/approval
-- di TB_R_COPY_REQUEST_H/D sudah ada, tapi belum ada bagian eksekusi cetak
-- & audit jumlah kertas terpakai. Keputusan Hendra 2026-08-12: PrintTrack
-- menumpang alur Copy Request yang sudah ada, bukan bikin request/approval
-- terpisah.
--
-- Alur token: begitu REQUEST_H.STATUS='2' (Approved), tiap baris
-- TB_R_COPY_REQUEST_D dengan PRINT_STATUS='0' boleh dieksekusi cetak oleh
-- PrintTrack. Setelah sukses, PRINT_STATUS dikunci ke '1' (sekali pakai) -
-- cetak ulang wajib request baru, bukan pakai baris yang sama.
--
-- Idempotent - aman dijalankan ulang.
-- Jalankan di database DMS_NEW
-- =====================================================================

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- ---------------------------------------------------------------------
-- Bagian 1: token cetak sekali-pakai per baris dokumen di Copy Request.
-- ---------------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TB_R_COPY_REQUEST_D') AND name = 'PRINT_STATUS')
BEGIN
	ALTER TABLE [dbo].[TB_R_COPY_REQUEST_D] ADD [PRINT_STATUS] CHAR(1) NOT NULL DEFAULT ('0');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TB_R_COPY_REQUEST_D') AND name = 'PRINTED_BY')
BEGIN
	ALTER TABLE [dbo].[TB_R_COPY_REQUEST_D] ADD [PRINTED_BY] VARCHAR(50) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TB_R_COPY_REQUEST_D') AND name = 'PRINTED_DT')
BEGIN
	ALTER TABLE [dbo].[TB_R_COPY_REQUEST_D] ADD [PRINTED_DT] DATETIME NULL;
END
GO

-- ---------------------------------------------------------------------
-- Bagian 2: log audit tiap eksekusi cetak dari PrintTrack (termasuk yang
-- gagal - PRINT_STATUS di sini beda konteks dari PRINT_STATUS di header
-- _D di atas, ini status hasil eksekusi per percobaan cetak).
-- TOTAL_SHEETS computed supaya tidak bisa nyimpang dari PAGE_COUNT x
-- COPY_COUNT.
-- ---------------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TB_R_COPY_REQUEST_PRINT_LOG')
BEGIN
	CREATE TABLE [dbo].[TB_R_COPY_REQUEST_PRINT_LOG] (
		[PRINT_LOG_ID]       INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
		[REQUEST_DETAIL_ID]  INT          NOT NULL,
		[COMPUTER_NAME]      VARCHAR(100) NULL,
		[PRINTER_NAME]       VARCHAR(200) NULL,
		[PAGE_COUNT]         INT          NULL,
		[COPY_COUNT]         INT          NULL,
		[TOTAL_SHEETS]       AS ([PAGE_COUNT] * [COPY_COUNT]) PERSISTED,
		[PRINT_JOB_ID]       INT          NULL,
		[PRINT_STATUS]       VARCHAR(20)  NOT NULL DEFAULT ('Success'),
		[ERROR_DETAIL]       VARCHAR(MAX) NULL,
		[PRINTED_BY]         VARCHAR(50)  NULL,
		[PRINTED_DT]         DATETIME     NULL,
		CONSTRAINT [FK_CPR_PRINTLOG_REQUEST_DETAIL] FOREIGN KEY ([REQUEST_DETAIL_ID])
			REFERENCES [dbo].[TB_R_COPY_REQUEST_D] ([REQUEST_DETAIL_ID])
			ON DELETE CASCADE
	);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CPR_PRINTLOG_REQUEST_DETAIL_ID')
BEGIN
	CREATE INDEX [IX_CPR_PRINTLOG_REQUEST_DETAIL_ID] ON [dbo].[TB_R_COPY_REQUEST_PRINT_LOG] ([REQUEST_DETAIL_ID]);
END
GO
