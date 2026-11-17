-- =====================================================================
-- Organisation master 2026 (request Hendra 2026-11-17).
--
-- Source: "UPDATE DATA QMS COMMITTE  KODE UNTUK PENOMORAN DOKUMEN.xlsx", sheet
-- "KODE SECTION (2026)" - 19 divisions, 52 departments, their sections.
--
-- Run AFTER SQL/Maintenance_ResetTransactionData_20261117.sql (transactions empty),
-- so no document, distribution or request points at a code removed here.
--
--   - Division / Department / Section in the list: inserted, or updated in place
--     (same ID, name and division from the list, active again).
--   - Codes not in the list are DELETED from the table (Hendra: "bersihkan juga di
--     database"), except a row still used by a user position (TB_M_USER_POS) or a
--     Document Control binder (TB_M_DOCUMENT_FOLDER) - those are kept as they are
--     and listed at the end. Today that is QPD and its section 00 (4 users and the
--     Document Control access); move those users to QPA first, then run again.
--   - Duplicate codes (e.g. VMD, PCE) keep one row (active first, lowest ID).
--   - Section names written "…" in the list are kept as "…" (Hendra), blank names too.
--   - DEPT_SUCCESSOR: UDT#1 / UDT#2 pointed at UD1 / UD2, which are gone - now UDP.
--   - TB_M_SEQUENCE is emptied (all counters are 0 after the reset; rows are
--     recreated by sp_generate_doc_no / sp_GetNextSeqNo when needed).
--
-- Backups: *_BACKUP_20261117 (TB_M_SEQUENCE by the reset script). One transaction.
-- Run on database DMS_NEW with: sqlcmd -f 65001 (file is UTF-8).
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID('dbo.TB_M_DEPARTMENT_BACKUP_20261117', 'U') IS NOT NULL
BEGIN
    RAISERROR('Already run (TB_M_DEPARTMENT_BACKUP_20261117 exists).', 16, 1);
    SET NOEXEC ON;
END
GO

IF EXISTS (SELECT 1 FROM dbo.TB_R_DOCUMENT) OR EXISTS (SELECT 1 FROM dbo.TB_R_CTRL_DOCUMENT)
BEGIN
    RAISERROR('Transaction data is not empty - run Maintenance_ResetTransactionData_20261117.sql first.', 16, 1);
    SET NOEXEC ON;
END
GO

BEGIN TRANSACTION;

SELECT * INTO dbo.TB_M_DIVISION_BACKUP_20261117   FROM dbo.TB_M_DIVISION;
SELECT * INTO dbo.TB_M_DEPARTMENT_BACKUP_20261117 FROM dbo.TB_M_DEPARTMENT;
SELECT * INTO dbo.TB_M_SECTION_BACKUP_20261117    FROM dbo.TB_M_SECTION;
SELECT * INTO dbo.TB_M_SYSTEM_BACKUP_20261117     FROM dbo.TB_M_SYSTEM WHERE SYSTEM_TYPE = 'DEPT_SUCCESSOR';

DECLARE @BY VARCHAR(20) = 'dms.admin', @NOW DATETIME = GETDATE();

DECLARE @X TABLE (DIV VARCHAR(5), DIVNAME VARCHAR(255), DEPT VARCHAR(5), DEPTNAME VARCHAR(255), SEC VARCHAR(5), SECNAME VARCHAR(255));
INSERT @X (DIV, DIVNAME, DEPT, DEPTNAME, SEC, SECNAME) VALUES
    (N'IAD', N'Internal Audit Div.', N'IAD', N'Internal Audit Dept.', N'00', N'General'),
    (N'IAD', N'Internal Audit Div.', N'IAD', N'Internal Audit Dept.', N'01', N'Financial Audit'),
    (N'IAD', N'Internal Audit Div.', N'IAD', N'Internal Audit Dept.', N'02', N'Non-Financial Audit'),
    (N'HRD', N'Human Resources Development Div.', N'RMN', N'Remuneration Dept.', N'00', N'General'),
    (N'HRD', N'Human Resources Development Div.', N'RMN', N'Remuneration Dept.', N'01', N'Payroll'),
    (N'HRD', N'Human Resources Development Div.', N'RMN', N'Remuneration Dept.', N'02', N'Personnel Administration'),
    (N'HRD', N'Human Resources Development Div.', N'OPD', N'Organization & People Development Dept.', N'00', N'General'),
    (N'HRD', N'Human Resources Development Div.', N'OPD', N'Organization & People Development Dept.', N'01', N'Organization Development'),
    (N'HRD', N'Human Resources Development Div.', N'OPD', N'Organization & People Development Dept.', N'02', N'People Development'),
    (N'HRD', N'Human Resources Development Div.', N'IRL', N'Industrial Relation & Recruitment Dept.', N'00', N'General'),
    (N'HRD', N'Human Resources Development Div.', N'IRL', N'Industrial Relation & Recruitment Dept.', N'01', N'Recruitment'),
    (N'HRD', N'Human Resources Development Div.', N'IRL', N'Industrial Relation & Recruitment Dept.', N'02', N'Employee Service'),
    (N'HRD', N'Human Resources Development Div.', N'IRL', N'Industrial Relation & Recruitment Dept.', N'03', N'Employee Relation'),
    (N'GAD', N'General Affair Div.', N'GPC', N'General Procurement Dept.', N'00', N'General'),
    (N'GAD', N'General Affair Div.', N'GPC', N'General Procurement Dept.', N'01', N'Expatriate Matters & Administration'),
    (N'GAD', N'General Affair Div.', N'GPC', N'General Procurement Dept.', N'02', N'Secretary BOD'),
    (N'GAD', N'General Affair Div.', N'GPC', N'General Procurement Dept.', N'03', N'General Procurement & Project'),
    (N'GAD', N'General Affair Div.', N'OPR', N'GA Operation Dept.', N'00', N'General'),
    (N'GAD', N'General Affair Div.', N'OPR', N'GA Operation Dept.', N'01', N'Office & Public Facilities'),
    (N'GAD', N'General Affair Div.', N'OPR', N'GA Operation Dept.', N'02', N'Meal & Drink Service'),
    (N'GAD', N'General Affair Div.', N'OPR', N'GA Operation Dept.', N'03', N'Scrap Management'),
    (N'GAD', N'General Affair Div.', N'OPR', N'GA Operation Dept.', N'04', N'Service Management (Cleaning Company)'),
    (N'GAD', N'General Affair Div.', N'OPR', N'GA Operation Dept.', N'05', N'Outsourcing Management (Security & Driver Company)'),
    (N'GAD', N'General Affair Div.', N'OPR', N'GA Operation Dept.', N'06', N'Warehouse'),
    (N'ITD', N'Information & Technology Div.', N'APP', N'IT Application & Project Dept.', N'00', N'General'),
    (N'ITD', N'Information & Technology Div.', N'APP', N'IT Application & Project Dept.', N'01', N'IT Project & System Adm.'),
    (N'ITD', N'Information & Technology Div.', N'APP', N'IT Application & Project Dept.', N'02', N'Prod. System'),
    (N'ITD', N'Information & Technology Div.', N'INF', N'IT Infrastructure Dept.', N'00', N'General'),
    (N'ITD', N'Information & Technology Div.', N'INF', N'IT Infrastructure Dept.', N'01', N'Server'),
    (N'ITD', N'Information & Technology Div.', N'INF', N'IT Infrastructure Dept.', N'02', N'Helpdesk'),
    (N'ITD', N'Information & Technology Div.', N'INF', N'IT Infrastructure Dept.', N'03', N'Network'),
    (N'ITD', N'Information & Technology Div.', N'INF', N'IT Infrastructure Dept.', N'04', N'Security'),
    (N'ITD', N'Information & Technology Div.', N'INF', N'IT Infrastructure Dept.', N'05', N'Telephony'),
    (N'CED', N'Corporate & External Affairs Div.', N'CTE', N'Certification, Technical Regulation & External Affairs', N'00', N'General'),
    (N'CED', N'Corporate & External Affairs Div.', N'CTE', N'Certification, Technical Regulation & External Affairs', N'01', N'Certification, Technical Regulation'),
    (N'CED', N'Corporate & External Affairs Div.', N'CTE', N'Certification, Technical Regulation & External Affairs', N'02', N'External Affairs'),
    (N'CED', N'Corporate & External Affairs Div.', N'CLD', N'Corporate & Legal Dept.', N'00', N'General'),
    (N'CED', N'Corporate & External Affairs Div.', N'CLD', N'Corporate & Legal Dept.', N'01', N'Legal & Agreement'),
    (N'CED', N'Corporate & External Affairs Div.', N'CLD', N'Corporate & Legal Dept.', N'02', N'Corporate & CSR'),
    (N'FAD', N'Finance & Accounting Div.', N'FNC', N'Finance Dept.', N'00', N'General'),
    (N'FAD', N'Finance & Accounting Div.', N'FNC', N'Finance Dept.', N'01', N'Finance'),
    (N'FAD', N'Finance & Accounting Div.', N'FNC', N'Finance Dept.', N'02', N'Account Receivable'),
    (N'FAD', N'Finance & Accounting Div.', N'GAC', N'General Accounting & Account Payable Dept.', N'00', N'General'),
    (N'FAD', N'Finance & Accounting Div.', N'GAC', N'General Accounting & Account Payable Dept.', N'01', N'Account Payable'),
    (N'FAD', N'Finance & Accounting Div.', N'GAC', N'General Accounting & Account Payable Dept.', N'02', N'General Accounting & Financial Report'),
    (N'FAD', N'Finance & Accounting Div.', N'TAX', N'Tax & Asset Control Dept.', N'00', N'General'),
    (N'FAD', N'Finance & Accounting Div.', N'TAX', N'Tax & Asset Control Dept.', N'01', N'Tax'),
    (N'FAD', N'Finance & Accounting Div.', N'TAX', N'Tax & Asset Control Dept.', N'02', N'Asset Control'),
    (N'FAD', N'Finance & Accounting Div.', N'CST', N'Costing Dept.', N'00', N'General'),
    (N'FAD', N'Finance & Accounting Div.', N'CST', N'Costing Dept.', N'01', N'Costing Assy'),
    (N'FAD', N'Finance & Accounting Div.', N'CST', N'Costing Dept.', N'02', N'Costing Machining'),
    (N'FAD', N'Finance & Accounting Div.', N'EID', N'Export - Import Dept.', N'00', N'General'),
    (N'FAD', N'Finance & Accounting Div.', N'EID', N'Export - Import Dept.', N'01', N'Export Operation'),
    (N'FAD', N'Finance & Accounting Div.', N'EID', N'Export - Import Dept.', N'02', N'Import Operation'),
    (N'FAD', N'Finance & Accounting Div.', N'RNF', N'Regulation & Facility Dept.', N'00', N'General'),
    (N'BPD', N'Business Planning Div.', N'PBD', N'Part Business Dept.', N'00', N'General'),
    (N'BPD', N'Business Planning Div.', N'PBD', N'Part Business Dept.', N'01', N'MSP Export Business'),
    (N'BPD', N'Business Planning Div.', N'PBD', N'Part Business Dept.', N'02', N'Service Part Business'),
    (N'BPD', N'Business Planning Div.', N'VBP', N'Vehicle Business & Profit Planning Dept.', N'00', N'General'),
    (N'BPD', N'Business Planning Div.', N'VBP', N'Vehicle Business & Profit Planning Dept.', N'01', N'Vehicle Cost Planning'),
    (N'BPD', N'Business Planning Div.', N'VBP', N'Vehicle Business & Profit Planning Dept.', N'02', N'Buss.Plan & Investment'),
    (N'BPD', N'Business Planning Div.', N'VBP', N'Vehicle Business & Profit Planning Dept.', N'03', N'Cost Management System'),
    (N'BPD', N'Business Planning Div.', N'VBP', N'Vehicle Business & Profit Planning Dept.', N'04', N'PII/BSR Secretary'),
    (N'PUD', N'Purchasing Div.', N'POM', N'Planning & Operation Management Dept.', N'00', N'General'),
    (N'PUD', N'Purchasing Div.', N'POM', N'Planning & Operation Management Dept.', N'01', N'…'),
    (N'PUD', N'Purchasing Div.', N'POM', N'Planning & Operation Management Dept.', N'02', N'…'),
    (N'PUD', N'Purchasing Div.', N'POM', N'Planning & Operation Management Dept.', N'03', N'…'),
    (N'PUD', N'Purchasing Div.', N'PRC', N'Procurement Dept.', N'00', N'General'),
    (N'PUD', N'Purchasing Div.', N'PRC', N'Procurement Dept.', N'01', N'…'),
    (N'PUD', N'Purchasing Div.', N'PRC', N'Procurement Dept.', N'02', N'…'),
    (N'PUD', N'Purchasing Div.', N'PRC', N'Procurement Dept.', N'03', N'…'),
    (N'PUD', N'Purchasing Div.', N'CGD', N'Component Group Dept.', N'00', N'General'),
    (N'PUD', N'Purchasing Div.', N'CGD', N'Component Group Dept.', N'01', N'…'),
    (N'PUD', N'Purchasing Div.', N'CGD', N'Component Group Dept.', N'02', N'…'),
    (N'PUD', N'Purchasing Div.', N'CGD', N'Component Group Dept.', N'03', N'…'),
    (N'PUD', N'Purchasing Div.', N'NCG', N'Non Component Group Dept.', N'00', N'General'),
    (N'PUD', N'Purchasing Div.', N'NCG', N'Non Component Group Dept.', N'01', N'…'),
    (N'PUD', N'Purchasing Div.', N'NCG', N'Non Component Group Dept.', N'02', N'…'),
    (N'PUD', N'Purchasing Div.', N'NCG', N'Non Component Group Dept.', N'03', N'…'),
    (N'PPP', N'Production Plan & Project Div.', N'PPD', N'Production Planning Dept.', N'00', N'General'),
    (N'PPP', N'Production Plan & Project Div.', N'PPD', N'Production Planning Dept.', N'01', N'Production Planning'),
    (N'PPP', N'Production Plan & Project Div.', N'PPD', N'Production Planning Dept.', N'02', N'Import Part Order Planning'),
    (N'PPP', N'Production Plan & Project Div.', N'PPD', N'Production Planning Dept.', N'03', N'Supply Demand & Project'),
    (N'PPP', N'Production Plan & Project Div.', N'PJT', N'Project Planning & Management Dept.', N'00', N'General'),
    (N'PPP', N'Production Plan & Project Div.', N'PJT', N'Project Planning & Management Dept.', N'01', N'Project Administration'),
    (N'PPP', N'Production Plan & Project Div.', N'PJT', N'Project Planning & Management Dept.', N'02', N'Project Planning'),
    (N'PPP', N'Production Plan & Project Div.', N'PJT', N'Project Planning & Management Dept.', N'03', N'Project Management'),
    (N'SHE', N'Safety, Health and Environment Div.', N'SFH', N'Safety & Health Dept.', N'00', N'General'),
    (N'SHE', N'Safety, Health and Environment Div.', N'SFH', N'Safety & Health Dept.', N'01', N'Document & Regulation'),
    (N'SHE', N'Safety, Health and Environment Div.', N'SFH', N'Safety & Health Dept.', N'02', N'Safety Operational'),
    (N'SHE', N'Safety, Health and Environment Div.', N'SFH', N'Safety & Health Dept.', N'03', N'Health Operational'),
    (N'SHE', N'Safety, Health and Environment Div.', N'ENV', N'Environment Dept.', N'00', N'General'),
    (N'SHE', N'Safety, Health and Environment Div.', N'ENV', N'Environment Dept.', N'01', N'Environment Management System'),
    (N'SHE', N'Safety, Health and Environment Div.', N'ENV', N'Environment Dept.', N'02', N'Environment Operational'),
    (N'LCD', N'Logistic Control Div.', N'SPD', N'Service Part & Packing Export Dept.', N'00', N'General'),
    (N'LCD', N'Logistic Control Div.', N'SPD', N'Service Part & Packing Export Dept.', N'01', N'Service Part'),
    (N'LCD', N'Logistic Control Div.', N'SPD', N'Service Part & Packing Export Dept.', N'02', N'Packing Export'),
    (N'LCD', N'Logistic Control Div.', N'LPA', N'Logistic Planning & Assembly Dept.', N'00', N'General'),
    (N'LCD', N'Logistic Control Div.', N'LPA', N'Logistic Planning & Assembly Dept.', N'01', N'Logistic Planning'),
    (N'LCD', N'Logistic Control Div.', N'LPA', N'Logistic Planning & Assembly Dept.', N'02', N'Logistic Assembly'),
    (N'LCD', N'Logistic Control Div.', N'LMC', N'Logistic Machining Dept.', N'00', N'General'),
    (N'BDY', N'Body Production Div.', N'PAT', N'Painting Dept.', N'00', N'General'),
    (N'BDY', N'Body Production Div.', N'PAT', N'Painting Dept.', N'01', N'Small'),
    (N'BDY', N'Body Production Div.', N'PAT', N'Painting Dept.', N'02', N'Medium'),
    (N'BDY', N'Body Production Div.', N'PAT', N'Painting Dept.', N'03', N'Plastic'),
    (N'BDY', N'Body Production Div.', N'PAT', N'Painting Dept.', N'04', N'Anti Rust Line'),
    (N'BDY', N'Body Production Div.', N'PAT', N'Painting Dept.', N'05', N'Pre Treatment ED'),
    (N'BDY', N'Body Production Div.', N'PAT', N'Painting Dept.', N'06', N'Sanding Sealer'),
    (N'BDY', N'Body Production Div.', N'PAT', N'Painting Dept.', N'07', N'Top Coat'),
    (N'BDY', N'Body Production Div.', N'PAT', N'Painting Dept.', N'08', N'Inspection Repair'),
    (N'BDY', N'Body Production Div.', N'WEL', N'Welding Dept.', N'00', N'General'),
    (N'BDY', N'Body Production Div.', N'WEL', N'Welding Dept.', N'01', N'Welding MDT'),
    (N'BDY', N'Body Production Div.', N'WEL', N'Welding Dept.', N'02', N'Welding LDT'),
    (N'BDY', N'Body Production Div.', N'WEL', N'Welding Dept.', N'03', N'Press Bus'),
    (N'BDY', N'Body Production Div.', N'WEL', N'Welding Dept.', N'04', N'Welding Technical'),
    (N'ASY', N'Assy Production Div.', N'VMD', N'Vehicle Medium Dept.', N'00', N'General'),
    (N'ASY', N'Assy Production Div.', N'VMD', N'Vehicle Medium Dept.', N'01', N'Frame Line - Truck'),
    (N'ASY', N'Assy Production Div.', N'VMD', N'Vehicle Medium Dept.', N'02', N'Final Line - Truck'),
    (N'ASY', N'Assy Production Div.', N'VMD', N'Vehicle Medium Dept.', N'03', N'Cabin Trimming Line - Truck'),
    (N'ASY', N'Assy Production Div.', N'VMD', N'Vehicle Medium Dept.', N'04', N'Sub Assy Line - Truck'),
    (N'ASY', N'Assy Production Div.', N'VMD', N'Vehicle Medium Dept.', N'05', N'Tire Line'),
    (N'ASY', N'Assy Production Div.', N'VMD', N'Vehicle Medium Dept.', N'06', N'Unpacking Supply Line - Truck'),
    (N'ASY', N'Assy Production Div.', N'VMD', N'Vehicle Medium Dept.', N'07', N'Nylon Tube Line - Truck'),
    (N'ASY', N'Assy Production Div.', N'VMD', N'Vehicle Medium Dept.', N'08', N'Inspection Line - Truck'),
    (N'ASY', N'Assy Production Div.', N'VMD', N'Vehicle Medium Dept.', N'09', N'Frame Line - Bus (move to VSD)'),
    (N'ASY', N'Assy Production Div.', N'VMD', N'Vehicle Medium Dept.', N'10', N'Final Line - Bus (move to VSD)'),
    (N'ASY', N'Assy Production Div.', N'VMD', N'Vehicle Medium Dept.', N'11', N'Sub Assy Line - Bus (move to VSD)'),
    (N'ASY', N'Assy Production Div.', N'VMD', N'Vehicle Medium Dept.', N'12', N'Toeboard Line - Bus (move to VSD)'),
    (N'ASY', N'Assy Production Div.', N'VMD', N'Vehicle Medium Dept.', N'13', N'Inspection Line - Bus (move to VSD)'),
    (N'ASY', N'Assy Production Div.', N'VMD', N'Vehicle Medium Dept.', N'14', N'Side Guard Line'),
    (N'ASY', N'Assy Production Div.', N'VMD', N'Vehicle Medium Dept.', N'21', N'Frame Line - Truck (Y415)'),
    (N'ASY', N'Assy Production Div.', N'VMD', N'Vehicle Medium Dept.', N'22', N'Final Line - Truck (Y415)'),
    (N'ASY', N'Assy Production Div.', N'VMD', N'Vehicle Medium Dept.', N'23', N'Cabin Trimming Line - Truck (Y415)'),
    (N'ASY', N'Assy Production Div.', N'VMD', N'Vehicle Medium Dept.', N'24', N'Sub Assy Line - Truck (Y415)'),
    (N'ASY', N'Assy Production Div.', N'VMD', N'Vehicle Medium Dept.', N'25', N'Tire Line (Y415)'),
    (N'ASY', N'Assy Production Div.', N'VMD', N'Vehicle Medium Dept.', N'26', N'Unpacking Supply Line - Truck (Y415)'),
    (N'ASY', N'Assy Production Div.', N'VMD', N'Vehicle Medium Dept.', N'27', N'Nylon Tube Line - Truck (Y415)'),
    (N'ASY', N'Assy Production Div.', N'VMD', N'Vehicle Medium Dept.', N'28', N'Inspection Line - Truck (Y415)'),
    (N'ASY', N'Assy Production Div.', N'VSD', N'Vehicle Small Dept.', N'00', N'General'),
    (N'ASY', N'Assy Production Div.', N'VSD', N'Vehicle Small Dept.', N'01', N'Frame Small'),
    (N'ASY', N'Assy Production Div.', N'VSD', N'Vehicle Small Dept.', N'02', N'Final Small'),
    (N'ASY', N'Assy Production Div.', N'VSD', N'Vehicle Small Dept.', N'03', N'Trimming Small'),
    (N'ASY', N'Assy Production Div.', N'VSD', N'Vehicle Small Dept.', N'04', N'Sub Assy Small'),
    (N'ASY', N'Assy Production Div.', N'VSD', N'Vehicle Small Dept.', N'05', N'Sub Assy Tire Small'),
    (N'ASY', N'Assy Production Div.', N'VSD', N'Vehicle Small Dept.', N'06', N'Unpacking Supply Small'),
    (N'ASY', N'Assy Production Div.', N'VSD', N'Vehicle Small Dept.', N'07', N'Inspection'),
    (N'ASY', N'Assy Production Div.', N'VSD', N'Vehicle Small Dept.', N'08', N'-'),
    (N'ASY', N'Assy Production Div.', N'VSD', N'Vehicle Small Dept.', N'09', N'Frame Line - Bus'),
    (N'ASY', N'Assy Production Div.', N'VSD', N'Vehicle Small Dept.', N'10', N'Final Line - Bus'),
    (N'ASY', N'Assy Production Div.', N'VSD', N'Vehicle Small Dept.', N'11', N'Sub Assy Line - Bus'),
    (N'ASY', N'Assy Production Div.', N'VSD', N'Vehicle Small Dept.', N'12', N'Toeboard Line - Bus'),
    (N'ASY', N'Assy Production Div.', N'VSD', N'Vehicle Small Dept.', N'13', N'Inspection Line - Bus'),
    (N'MUD', N'Machining & Unit Production Div.', N'AXP', N'Machining Axle & PS Dept.', N'00', N'General'),
    (N'MUD', N'Machining & Unit Production Div.', N'AXP', N'Machining Axle & PS Dept.', N'01', N'Axle 13 Inch'),
    (N'MUD', N'Machining & Unit Production Div.', N'AXP', N'Machining Axle & PS Dept.', N'02', N'Axle 17 Inch'),
    (N'MUD', N'Machining & Unit Production Div.', N'AXP', N'Machining Axle & PS Dept.', N'11', N'Sliding'),
    (N'MUD', N'Machining & Unit Production Div.', N'AXP', N'Machining Axle & PS Dept.', N'12', N'Flange'),
    (N'MUD', N'Machining & Unit Production Div.', N'AXP', N'Machining Axle & PS Dept.', N'13', N'Coupling'),
    (N'MUD', N'Machining & Unit Production Div.', N'AXP', N'Machining Axle & PS Dept.', N'14', N'Inner Shaft'),
    (N'MUD', N'Machining & Unit Production Div.', N'AXP', N'Machining Axle & PS Dept.', N'15', N'Spline Shaft/Center Bearing'),
    (N'MUD', N'Machining & Unit Production Div.', N'AXP', N'Machining Axle & PS Dept.', N'16', N'End Yoke'),
    (N'MUD', N'Machining & Unit Production Div.', N'AXP', N'Machining Axle & PS Dept.', N'17', N'Nylon Coating'),
    (N'MUD', N'Machining & Unit Production Div.', N'AXP', N'Machining Axle & PS Dept.', N'18', N'Propeller Shaft Sub Assy'),
    (N'MUD', N'Machining & Unit Production Div.', N'AXP', N'Machining Axle & PS Dept.', N'19', N'Unpacking'),
    (N'MUD', N'Machining & Unit Production Div.', N'AXP', N'Machining Axle & PS Dept.', N'20', N'Consumable'),
    (N'MUD', N'Machining & Unit Production Div.', N'AXP', N'Machining Axle & PS Dept.', N'21', N'Propeller Shaft Main Assy'),
    (N'MUD', N'Machining & Unit Production Div.', N'EMC', N'Engine Machining 5C Dept.', N'00', N'General'),
    (N'MUD', N'Machining & Unit Production Div.', N'EMC', N'Engine Machining 5C Dept.', N'01', N'Connecting Rod'),
    (N'MUD', N'Machining & Unit Production Div.', N'EMC', N'Engine Machining 5C Dept.', N'02', N'Camshaft'),
    (N'MUD', N'Machining & Unit Production Div.', N'EMC', N'Engine Machining 5C Dept.', N'03', N'Crankshaft'),
    (N'MUD', N'Machining & Unit Production Div.', N'EMC', N'Engine Machining 5C Dept.', N'04', N'Cylinder Head'),
    (N'MUD', N'Machining & Unit Production Div.', N'EMC', N'Engine Machining 5C Dept.', N'05', N'Cylinder Block'),
    (N'MUD', N'Machining & Unit Production Div.', N'EMC', N'Engine Machining 5C Dept.', N'06', N'Preset Tools'),
    (N'MUD', N'Machining & Unit Production Div.', N'EMC', N'Engine Machining 5C Dept.', N'07', N'Production Logistic Support'),
    (N'MUD', N'Machining & Unit Production Div.', N'EMC', N'Engine Machining 5C Dept.', N'08', N'Packing Export'),
    (N'MUD', N'Machining & Unit Production Div.', N'EMC', N'Engine Machining 5C Dept.', N'09', N'Internal Regrinding'),
    (N'MUD', N'Machining & Unit Production Div.', N'UDP', N'Unit Drivetrain Production Dept.', N'00', N'General'),
    (N'MUD', N'Machining & Unit Production Div.', N'UDP', N'Unit Drivetrain Production Dept.', N'01', N'CKD Axle - Truck'),
    (N'MUD', N'Machining & Unit Production Div.', N'UDP', N'Unit Drivetrain Production Dept.', N'02', N'Axle Line - Truck'),
    (N'MUD', N'Machining & Unit Production Div.', N'UDP', N'Unit Drivetrain Production Dept.', N'03', N'CKD Axle - Bus'),
    (N'MUD', N'Machining & Unit Production Div.', N'UDP', N'Unit Drivetrain Production Dept.', N'04', N'DC Line - Bus'),
    (N'MUD', N'Machining & Unit Production Div.', N'UDP', N'Unit Drivetrain Production Dept.', N'05', N'DC Line - Truck'),
    (N'MUD', N'Machining & Unit Production Div.', N'UDP', N'Unit Drivetrain Production Dept.', N'06', N'S/A DC (Pinion and Ring Gear) - Truck'),
    (N'MUD', N'Machining & Unit Production Div.', N'UDP', N'Unit Drivetrain Production Dept.', N'07', N'S/A DC (Pinion and Ring Gear) - Bus'),
    (N'MUD', N'Machining & Unit Production Div.', N'UDP', N'Unit Drivetrain Production Dept.', N'08', N'Axle Line - Bus'),
    (N'MUD', N'Machining & Unit Production Div.', N'UDP', N'Unit Drivetrain Production Dept.', N'09', N'S/A Axle (Hub Drum) - Bus'),
    (N'MUD', N'Machining & Unit Production Div.', N'UDP', N'Unit Drivetrain Production Dept.', N'10', N'Axle Line - Truck'),
    (N'MUD', N'Machining & Unit Production Div.', N'UDP', N'Unit Drivetrain Production Dept.', N'11', N'S/A Axle (Hub Drum) - Truck'),
    (N'MUD', N'Machining & Unit Production Div.', N'UDP', N'Unit Drivetrain Production Dept.', N'12', N'Painting & Inspection'),
    (N'MUD', N'Machining & Unit Production Div.', N'UDP', N'Unit Drivetrain Production Dept.', N'15', N'Unpacking Line ZA'),
    (N'MUD', N'Machining & Unit Production Div.', N'UDP', N'Unit Drivetrain Production Dept.', N'20', N'Main Line ZA'),
    (N'MUD', N'Machining & Unit Production Div.', N'UDP', N'Unit Drivetrain Production Dept.', N'25', N'Unpacking Line M009'),
    (N'MUD', N'Machining & Unit Production Div.', N'UDP', N'Unit Drivetrain Production Dept.', N'30', N'Main Line M009'),
    (N'MUD', N'Machining & Unit Production Div.', N'UDP', N'Unit Drivetrain Production Dept.', N'35', N'Trunnion Sub-Assy'),
    (N'MUD', N'Machining & Unit Production Div.', N'UDP', N'Unit Drivetrain Production Dept.', N'40', N'Cutting Tube Propeller'),
    (N'MUD', N'Machining & Unit Production Div.', N'UDP', N'Unit Drivetrain Production Dept.', N'45', N'Propeller Shaft Main Assy'),
    (N'MUD', N'Machining & Unit Production Div.', N'UED', N'Unit Engine Dept.', N'00', N'General'),
    (N'MUD', N'Machining & Unit Production Div.', N'UED', N'Unit Engine Dept.', N'01', N'CKD Engine'),
    (N'MUD', N'Machining & Unit Production Div.', N'UED', N'Unit Engine Dept.', N'02', N'Sub Assy'),
    (N'MUD', N'Machining & Unit Production Div.', N'UED', N'Unit Engine Dept.', N'03', N'Engine Line'),
    (N'MUD', N'Machining & Unit Production Div.', N'UED', N'Unit Engine Dept.', N'04', N'Engine Test'),
    (N'MUD', N'Machining & Unit Production Div.', N'UED', N'Unit Engine Dept.', N'05', N'Inspection'),
    (N'MUD', N'Machining & Unit Production Div.', N'UED', N'Unit Engine Dept.', N'06', N'Process Engineering'),
    (N'PED', N'Production Engineering Div.', N'MTA', N'Maintenance Assembly Dept.', N'00', N'General'),
    (N'PED', N'Production Engineering Div.', N'MTA', N'Maintenance Assembly Dept.', N'01', N'M/E - Assy'),
    (N'PED', N'Production Engineering Div.', N'MTA', N'Maintenance Assembly Dept.', N'02', N'M/E - Painting'),
    (N'PED', N'Production Engineering Div.', N'MTA', N'Maintenance Assembly Dept.', N'03', N'M/E - Repair Tools'),
    (N'PED', N'Production Engineering Div.', N'MTU', N'Maintenance Utility Dept.', N'00', N'General'),
    (N'PED', N'Production Engineering Div.', N'MTU', N'Maintenance Utility Dept.', N'01', N'Mechanical & Electrical Operation'),
    (N'PED', N'Production Engineering Div.', N'MTU', N'Maintenance Utility Dept.', N'02', N'Infrastructure Factory'),
    (N'PED', N'Production Engineering Div.', N'MTU', N'Maintenance Utility Dept.', N'03', N'Technical Support'),
    (N'PED', N'Production Engineering Div.', N'MTM', N'Maintenance Machining Dept.', N'00', N'General'),
    (N'PED', N'Production Engineering Div.', N'MTM', N'Maintenance Machining Dept.', N'01', N'Conrod'),
    (N'PED', N'Production Engineering Div.', N'MTM', N'Maintenance Machining Dept.', N'02', N'Camshaft'),
    (N'PED', N'Production Engineering Div.', N'MTM', N'Maintenance Machining Dept.', N'03', N'Crankshaft'),
    (N'PED', N'Production Engineering Div.', N'MTM', N'Maintenance Machining Dept.', N'04', N'Cylinder Head'),
    (N'PED', N'Production Engineering Div.', N'MTM', N'Maintenance Machining Dept.', N'05', N'Cylinder Block'),
    (N'PED', N'Production Engineering Div.', N'MTM', N'Maintenance Machining Dept.', N'06', N'Axle Housing 13"'),
    (N'PED', N'Production Engineering Div.', N'MTM', N'Maintenance Machining Dept.', N'08', N'Flange'),
    (N'PED', N'Production Engineering Div.', N'MTM', N'Maintenance Machining Dept.', N'09', N'Coupling'),
    (N'PED', N'Production Engineering Div.', N'MTM', N'Maintenance Machining Dept.', N'10', N'Sliding'),
    (N'PED', N'Production Engineering Div.', N'MTM', N'Maintenance Machining Dept.', N'11', N'End Yoke'),
    (N'PED', N'Production Engineering Div.', N'MTM', N'Maintenance Machining Dept.', N'12', N'Center Bearing'),
    (N'PED', N'Production Engineering Div.', N'MTM', N'Maintenance Machining Dept.', N'13', N'Inner Shaft'),
    (N'PED', N'Production Engineering Div.', N'MTM', N'Maintenance Machining Dept.', N'14', N'Nylon Coat'),
    (N'PED', N'Production Engineering Div.', N'MTM', N'Maintenance Machining Dept.', N'15', N'Sub Assy'),
    (N'PED', N'Production Engineering Div.', N'MTM', N'Maintenance Machining Dept.', N'16', N'Main Assy'),
    (N'PED', N'Production Engineering Div.', N'MTM', N'Maintenance Machining Dept.', N'17', N'Cutting Tube'),
    (N'PED', N'Production Engineering Div.', N'MTM', N'Maintenance Machining Dept.', N'18', N'Axle Assy'),
    (N'PED', N'Production Engineering Div.', N'MTM', N'Maintenance Machining Dept.', N'19', N'Transmission Old'),
    (N'PED', N'Production Engineering Div.', N'MTM', N'Maintenance Machining Dept.', N'20', N'Transmission M009'),
    (N'PED', N'Production Engineering Div.', N'MTM', N'Maintenance Machining Dept.', N'27', N'Axle Housing 17"'),
    (N'PED', N'Production Engineering Div.', N'MTM', N'Maintenance Machining Dept.', N'32', N'QC Machining'),
    (N'PED', N'Production Engineering Div.', N'MTM', N'Maintenance Machining Dept.', N'65', N'ME Repair'),
    (N'PED', N'Production Engineering Div.', N'MTM', N'Maintenance Machining Dept.', N'70', N'Re-Grinding & Preset Tool'),
    (N'PED', N'Production Engineering Div.', N'MTM', N'Maintenance Machining Dept.', N'78', N'Unit Engine MDT (MIX)'),
    (N'PED', N'Production Engineering Div.', N'MTM', N'Maintenance Machining Dept.', N'79', N'Unit Engine LDT (N)'),
    (N'PED', N'Production Engineering Div.', N'MTM', N'Maintenance Machining Dept.', N'80', N'Engine Bench Test'),
    (N'PED', N'Production Engineering Div.', N'ESD', N'Engineering Service Assembly & Machining Dept.', N'00', N'General'),
    (N'PED', N'Production Engineering Div.', N'ESD', N'Engineering Service Assembly & Machining Dept.', N'01', N'Engineering Sevice Assembly'),
    (N'PED', N'Production Engineering Div.', N'ESD', N'Engineering Service Assembly & Machining Dept.', N'02', N'Engineering Service Body'),
    (N'PED', N'Production Engineering Div.', N'ESD', N'Engineering Service Assembly & Machining Dept.', N'03', N'Engineering Service Machining'),
    (N'PED', N'Production Engineering Div.', N'ESD', N'Engineering Service Assembly & Machining Dept.', N'04', N'Engineering Service Unit Production'),
    (N'QCD', N'Quality Control Div.', N'QCP', N'Quality Control Part Dept.', N'00', N'General'),
    (N'QCD', N'Quality Control Div.', N'QCP', N'Quality Control Part Dept.', N'01', N'…'),
    (N'QCD', N'Quality Control Div.', N'QCP', N'Quality Control Part Dept.', N'02', N'…'),
    (N'QCD', N'Quality Control Div.', N'QCP', N'Quality Control Part Dept.', N'03', N'…'),
    (N'QCD', N'Quality Control Div.', N'QCV', N'Quality Control Vehicle Dept.', N'00', N'General'),
    (N'QCD', N'Quality Control Div.', N'QCV', N'Quality Control Vehicle Dept.', N'01', N'Vehicle Inspection Medium Truck'),
    (N'QCD', N'Quality Control Div.', N'QCV', N'Quality Control Vehicle Dept.', N'02', N'Vehicle Inspection Small Truck'),
    (N'QCD', N'Quality Control Div.', N'QCV', N'Quality Control Vehicle Dept.', N'03', N'QCV Engineering & Administration'),
    (N'QCD', N'Quality Control Div.', N'QCV', N'Quality Control Vehicle Dept.', N'04', N'Vehicle Inspection Bus'),
    (N'QCD', N'Quality Control Div.', N'QCV', N'Quality Control Vehicle Dept.', N'05', N'Calibration'),
    (N'QCD', N'Quality Control Div.', N'QCM', N'Quality Control Machining Dept.', N'00', N'General'),
    (N'QCD', N'Quality Control Div.', N'QCM', N'Quality Control Machining Dept.', N'01', N'Receiving Inspection'),
    (N'QCD', N'Quality Control Div.', N'QCM', N'Quality Control Machining Dept.', N'02', N'Inprocess Inspection'),
    (N'QCD', N'Quality Control Div.', N'QCM', N'Quality Control Machining Dept.', N'03', N'Technical'),
    (N'QCD', N'Quality Control Div.', N'QCM', N'Quality Control Machining Dept.', N'04', N'Calibration'),
    (N'QCD', N'Quality Control Div.', N'QCM', N'Quality Control Machining Dept.', N'05', N'Outgoing Inspection'),
    (N'QCD', N'Quality Control Div.', N'QCM', N'Quality Control Machining Dept.', N'06', N'Export Part'),
    (N'PAD', N'Plant Administration Div.', N'PID', N'Process Investigation Dept.', N'00', N'General'),
    (N'PAD', N'Plant Administration Div.', N'PID', N'Process Investigation Dept.', N'01', N'Internal Development'),
    (N'PAD', N'Plant Administration Div.', N'PID', N'Process Investigation Dept.', N'02', N'External Development'),
    (N'PAD', N'Plant Administration Div.', N'PID', N'Process Investigation Dept.', N'03', N'Training & Document Control'),
    (N'PAD', N'Plant Administration Div.', N'PCD', N'Production Control Dept.', N'00', N'General'),
    (N'PAD', N'Plant Administration Div.', N'PCD', N'Production Control Dept.', N'01', N'Vehicle Line Control'),
    (N'PAD', N'Plant Administration Div.', N'PCD', N'Production Control Dept.', N'02', N'Machining Unit Line Control'),
    (N'PAD', N'Plant Administration Div.', N'PAC', N'Plant Cost & Administration Dept.', N'00', N'General'),
    (N'EGD', N'Engineering Div.', N'PPL', N'Product Planning Dept.', N'00', N'General'),
    (N'EGD', N'Engineering Div.', N'PPL', N'Product Planning Dept.', N'01', N'…'),
    (N'EGD', N'Engineering Div.', N'PPL', N'Product Planning Dept.', N'02', N'…'),
    (N'EGD', N'Engineering Div.', N'PPL', N'Product Planning Dept.', N'03', N'…'),
    (N'EGD', N'Engineering Div.', N'VDV', N'Vehicle Development & Cost Reduction Dept.', N'00', N'General'),
    (N'EGD', N'Engineering Div.', N'VDV', N'Vehicle Development & Cost Reduction Dept.', N'01', N'…'),
    (N'EGD', N'Engineering Div.', N'VDV', N'Vehicle Development & Cost Reduction Dept.', N'02', N'…'),
    (N'EGD', N'Engineering Div.', N'VDV', N'Vehicle Development & Cost Reduction Dept.', N'03', N'…'),
    (N'EGD', N'Engineering Div.', N'LSD', N'Localization Support Dept.', N'00', N'General'),
    (N'EGD', N'Engineering Div.', N'LSD', N'Localization Support Dept.', N'01', N'…'),
    (N'EGD', N'Engineering Div.', N'LSD', N'Localization Support Dept.', N'02', N'…'),
    (N'EGD', N'Engineering Div.', N'LSD', N'Localization Support Dept.', N'03', N'…'),
    (N'QAD', N'Quality Assurance Div.', N'QPA', N'Quality Planning & Audit System Dept.', N'00', N'General'),
    (N'QAD', N'Quality Assurance Div.', N'QPA', N'Quality Planning & Audit System Dept.', N'01', N'Quality Planning'),
    (N'QAD', N'Quality Assurance Div.', N'QPA', N'Quality Planning & Audit System Dept.', N'02', N'Shipping Quality Audit'),
    (N'QAD', N'Quality Assurance Div.', N'QPA', N'Quality Planning & Audit System Dept.', N'03', N'Quality Management System'),
    (N'QAD', N'Quality Assurance Div.', N'CQE', N'Customer Quality Engineering Dept.', N'00', N'General'),
    (N'QAD', N'Quality Assurance Div.', N'CQE', N'Customer Quality Engineering Dept.', N'01', N'Early Detection and Early Resolution'),
    (N'QAD', N'Quality Assurance Div.', N'CQE', N'Customer Quality Engineering Dept.', N'02', N'Material Assurance & Warranty');

-- ---------- Division ----------
;WITH k AS (SELECT DIVISION_ID, ROW_NUMBER() OVER (PARTITION BY DIVISION_CODE ORDER BY CASE WHEN ISNULL(DELETE_FLAG, 0) = 0 THEN 0 ELSE 1 END, DIVISION_ID) rn FROM dbo.TB_M_DIVISION)
DELETE v FROM dbo.TB_M_DIVISION v JOIN k ON k.DIVISION_ID = v.DIVISION_ID
WHERE (k.rn > 1 OR v.DIVISION_CODE NOT IN (SELECT DIV FROM @X))
  AND v.DIVISION_CODE NOT IN (SELECT DIVISION FROM dbo.TB_M_USER_POS WHERE DIVISION IS NOT NULL);

UPDATE v SET DIVISION_NAME = x.DIVNAME, DELETE_FLAG = 0, CHANGED_BY = @BY, CHANGED_DT = @NOW
FROM dbo.TB_M_DIVISION v JOIN (SELECT DISTINCT DIV, DIVNAME FROM @X) x ON x.DIV = v.DIVISION_CODE;

INSERT dbo.TB_M_DIVISION (DIVISION_CODE, DIVISION_NAME, DELETE_FLAG, CREATED_BY, CREATED_DT, CHANGED_BY, CHANGED_DT, VALID_FROM, VALID_TO)
SELECT DISTINCT x.DIV, x.DIVNAME, 0, @BY, @NOW, @BY, @NOW, '2000-01-01', '9999-12-31'
FROM @X x WHERE NOT EXISTS (SELECT 1 FROM dbo.TB_M_DIVISION v WHERE v.DIVISION_CODE = x.DIV);

-- ---------- Department ----------
;WITH k AS (SELECT DEPARTMENT_ID, ROW_NUMBER() OVER (PARTITION BY DEPARTMENT_CODE ORDER BY CASE WHEN ISNULL(DELETE_FLAG, 0) = 0 THEN 0 ELSE 1 END, DEPARTMENT_ID) rn FROM dbo.TB_M_DEPARTMENT)
DELETE d FROM dbo.TB_M_DEPARTMENT d JOIN k ON k.DEPARTMENT_ID = d.DEPARTMENT_ID
WHERE (k.rn > 1 OR d.DEPARTMENT_CODE NOT IN (SELECT DEPT FROM @X))
  AND d.DEPARTMENT_ID NOT IN (SELECT DEPARTMENT_ID FROM dbo.TB_M_USER_POS WHERE DEPARTMENT_ID IS NOT NULL)
  AND d.DEPARTMENT_ID NOT IN (SELECT DEPARTMENT_ID FROM dbo.TB_M_DOCUMENT_FOLDER WHERE DEPARTMENT_ID IS NOT NULL);

UPDATE d SET DEPARTMENT_NAME = x.DEPTNAME, DIVISION = x.DIV, DELETE_FLAG = 0, CHANGED_BY = @BY, CHANGED_DT = @NOW
FROM dbo.TB_M_DEPARTMENT d JOIN (SELECT DISTINCT DIV, DEPT, DEPTNAME FROM @X) x ON x.DEPT = d.DEPARTMENT_CODE;

INSERT dbo.TB_M_DEPARTMENT (DEPARTMENT_CODE, DEPARTMENT_NAME, DIVISION, DELETE_FLAG, CREATED_BY, CREATED_DT, CHANGED_BY, CHANGED_DT, DOCUMENT_CONTROL_ACCESS, VALID_FROM, VALID_TO)
SELECT DISTINCT x.DEPT, x.DEPTNAME, x.DIV, 0, @BY, @NOW, @BY, @NOW, 0, '2000-01-01', '9999-12-31'
FROM @X x WHERE NOT EXISTS (SELECT 1 FROM dbo.TB_M_DEPARTMENT d WHERE d.DEPARTMENT_CODE = x.DEPT);

-- ---------- Section ----------
;WITH k AS (SELECT SECTION_ID, ROW_NUMBER() OVER (PARTITION BY DEPARTMENT_CODE, SECTION_CODE ORDER BY CASE WHEN ISNULL(DELETE_FLAG, 0) = 0 THEN 0 ELSE 1 END, SECTION_ID) rn FROM dbo.TB_M_SECTION)
DELETE s FROM dbo.TB_M_SECTION s JOIN k ON k.SECTION_ID = s.SECTION_ID
WHERE (k.rn > 1 OR NOT EXISTS (SELECT 1 FROM @X x WHERE x.DEPT = s.DEPARTMENT_CODE AND x.SEC = s.SECTION_CODE))
  AND s.SECTION_ID NOT IN (SELECT SECTION_ID FROM dbo.TB_M_USER_POS WHERE SECTION_ID IS NOT NULL);

UPDATE s SET SECTION_NAME = x.SECNAME, DELETE_FLAG = 0, CHANGED_BY = @BY, CHANGED_DT = @NOW
FROM dbo.TB_M_SECTION s JOIN @X x ON x.DEPT = s.DEPARTMENT_CODE AND x.SEC = s.SECTION_CODE;

INSERT dbo.TB_M_SECTION (SECTION_CODE, SECTION_NAME, DEPARTMENT_CODE, DELETE_FLAG, CREATED_BY, CREATED_DT, CHANGED_BY, CHANGED_DT, VALID_FROM, VALID_TO)
SELECT x.SEC, x.SECNAME, x.DEPT, 0, @BY, @NOW, @BY, @NOW, '2000-01-01', '9999-12-31'
FROM @X x WHERE NOT EXISTS (SELECT 1 FROM dbo.TB_M_SECTION s WHERE s.DEPARTMENT_CODE = x.DEPT AND s.SECTION_CODE = x.SEC);

-- ---------- Successor map, sequence ----------
UPDATE dbo.TB_M_SYSTEM SET SYSTEM_VALUE = 'UDP', CHANGED_BY = @BY, CHANGED_DT = @NOW
WHERE SYSTEM_TYPE = 'DEPT_SUCCESSOR' AND SYSTEM_VALUE IN ('UD1', 'UD2');

DELETE FROM dbo.TB_M_SEQUENCE;

COMMIT TRANSACTION;

-- ---------- Report ----------
SELECT 'DIVISION' T, COUNT(*) N FROM dbo.TB_M_DIVISION
UNION ALL SELECT 'DEPARTMENT', COUNT(*) FROM dbo.TB_M_DEPARTMENT
UNION ALL SELECT 'SECTION', COUNT(*) FROM dbo.TB_M_SECTION;

SELECT 'Kept, not in list (still used)' NOTE, 'DEPARTMENT' T, DEPARTMENT_CODE CODE, DEPARTMENT_NAME NAME FROM dbo.TB_M_DEPARTMENT WHERE DEPARTMENT_CODE NOT IN (SELECT DEPT FROM @X)
UNION ALL SELECT 'Kept, not in list (still used)', 'SECTION', DEPARTMENT_CODE + '-' + SECTION_CODE, SECTION_NAME FROM dbo.TB_M_SECTION s
WHERE NOT EXISTS (SELECT 1 FROM @X x WHERE x.DEPT = s.DEPARTMENT_CODE AND x.SEC = s.SECTION_CODE);
GO
SET NOEXEC OFF;
GO
