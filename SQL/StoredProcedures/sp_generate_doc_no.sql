CREATE OR ALTER PROCEDURE [dbo].[sp_generate_doc_no]
  @pDocLevel AS varchar(10),
	@pDivCode AS varchar(10),
	@pDeptCode AS varchar(10),
	@pSectionCode AS varchar(10),
	@pDocCode AS varchar(10),
  @pProcessCode AS varchar(10),
  @pCompanyCode AS varchar(10),
	@pDate AS DATE,
	@pDocNo AS VARCHAR (25) OUTPUT
	
AS
BEGIN
	-- routine body goes here, e.g.
	-- SELECT 'Navicat for SQL Server'
		DECLARE @vDocNo VARCHAR(50);
		DECLARE @vYear NUMERIC;
		DECLARE @vSeq1 int ;
		DECLARE @vSeqType VARCHAR(50);
		
		Set @vSeqType = 'DOC_NO';
		set @vSeq1 = 0;
		
		
		--SELECT YEAR('2017/08/25') AS Year;
		
		-- Check Documentcode 
		IF @pDocLevel = 1 
		   BEGIN
		   SET @vYear = YEAR(@pDate);
			 
			 SET @vDocNo = @pCompanyCode + '/' + @pDocCode + '/' + CAST (@vYear as VARCHAR) ;
			-- + YEAR(@pDate);
			
			
			
	     EXECUTE [dbo].[sp_GetNextSeqNo]
			 			 @SEQ_TYPE  = 'DOC_NO',
						 @SEQ_CODE  = @vDocNo,
	           @LOGIN_USER = 'System',
						 @p_seq_no = @vseq1 OUTPUT 
	           /*  @vSeqType,
							 @vDocNo,
							 'system',
							 @vSeq1*/
			 
			 -- print @vDocNo;
			-- print @vseq1;
			-- set @vSeq1 = @vSeq1 + 1;
			 SET @vDocNo = @vDocNo + '/' + RIGHT('000' + CAST (@vSeq1 as VARCHAR(3)),3) ;
			
			 -- print 
			 END;
		ELSE
		IF @pDocLevel = 2
		   BEGIN
				 SET @vDocNo = @pCompanyCode + '/' + @pDocCode + '/' + @pProcessCode
					
						 EXECUTE [dbo].[sp_GetNextSeqNo]
			 			 @SEQ_TYPE  = 'DOC_NO',
						 @SEQ_CODE  = @vDocNo,
	           @LOGIN_USER = 'System',
						 @p_seq_no = @vseq1 OUTPUT
					
					
				SET @vDocNo = @vDocNo + '/' + RIGHT('000' + CAST (@vSeq1 as VARCHAR(3)),3) ;
			 
			 END;
			 
			 ELSE
		IF @pDocLevel = 4
		   BEGIN
			    SET @vDocNo = @pDivCode + '/'+  @pDocCode + '-' + @pDeptCode + '-' + @pSectionCode
				  
					EXECUTE [dbo].[sp_GetNextSeqNo]
			 			 @SEQ_TYPE  = 'DOC_NO',
						 @SEQ_CODE  = @vDocNo,
	           @LOGIN_USER = 'System',
						 @p_seq_no = @vseq1 OUTPUT
			    
					
					
					SET @vDocNo = @vDocNo + '/' + RIGHT('000' + CAST (@vSeq1 as VARCHAR(3)),3) ;
			 
			 END;
			 ELSE
			 IF @pDocLevel = 3
		   BEGIN
			    SET @vDocNo = @pDivCode + '/'+  @pDocCode + '-' + @pDeptCode + '-' + @pSectionCode 				  
			    EXECUTE [dbo].[sp_GetNextSeqNo]
			 			 @SEQ_TYPE  = 'DOC_NO',
						 @SEQ_CODE  = @vDocNo,
	           @LOGIN_USER = 'System',
						 @p_seq_no = @vseq1 OUTPUT
			    
					
					SET @vDocNo = @vDocNo + '/' +  RIGHT('000' + CAST (@vSeq1 as VARCHAR(3)),3) ;
			 
			 END;
			 
			 
			 set @pDocNo = @vDocNo;
END
GO
