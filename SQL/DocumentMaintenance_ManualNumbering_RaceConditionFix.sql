-- =====================================================================
-- Fix: race condition in manual document numbering (Aug 2026).
-- Background: sp_generate_doc_no's manual-numbering branch checked "is this
-- number already in use" and then reserved/returned it as two separate,
-- unlocked steps. Two callers requesting the same manual number at nearly
-- the same time could both pass the check before either one reserved it,
-- and sp_DocumentMaintenance_Insert would then insert BOTH as live documents
-- with the identical DOCUMENT_CODE - reproduced and confirmed with a
-- deliberately delayed test copy of the procedure (two concurrent sessions
-- both got "ITD/SOP-APP-02/099" back with no error).
--
-- Two independent fixes, applied together (defense in depth):
--
-- 1) sp_generate_doc_no (see SQL/StoredProcedures/sp_generate_doc_no.sql -
--    redeploy that file alongside this script): the manual-numbering branch
--    now takes an exclusive sp_getapplock on the candidate document number
--    BEFORE the "already in use" check, with @LockOwner = 'Transaction' so
--    it stays held through the actual INSERT INTO TB_R_DOCUMENT that happens
--    later back in sp_DocumentMaintenance_Insert's caller (always inside a
--    transaction started by DocumentMaintenanceController). A second
--    concurrent caller for the same number now blocks until the first
--    either commits (and gets a correct "already in use" rejection) or
--    rolls back (and can proceed).
--
-- 2) TB_R_DOCUMENT: filtered unique index on (DOCUMENT_CODE, REVISION) for
--    active rows, as a database-level backstop in case the application-level
--    lock above is ever bypassed by some other code path. NOT a plain unique
--    index on DOCUMENT_CODE alone - revisions legitimately reuse the parent
--    document's DOCUMENT_CODE and coexist with it in TB_R_DOCUMENT (old
--    approved/published revision + new pending revision) until
--    sp_DocumentMaintenance_SupersedeRevision moves the old one to
--    TB_R_DOCUMENT_HISTORY and deletes it - so DOCUMENT_CODE alone is not
--    unique among active rows, but (DOCUMENT_CODE, REVISION) is, since a new
--    document (type 01) is always REVISION=0 and revisions always increment.
--
-- Idempotent. Jalankan di database DMS_NEW.
-- =====================================================================

SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (
	SELECT 1 FROM sys.indexes
	WHERE object_id = OBJECT_ID('dbo.TB_R_DOCUMENT')
	AND name = 'UX_TB_R_DOCUMENT_DocumentCode_Revision_Active'
)
BEGIN
	CREATE UNIQUE INDEX UX_TB_R_DOCUMENT_DocumentCode_Revision_Active
	ON dbo.TB_R_DOCUMENT (DOCUMENT_CODE, REVISION)
	WHERE DELETE_FLAG = 0;
END
GO
