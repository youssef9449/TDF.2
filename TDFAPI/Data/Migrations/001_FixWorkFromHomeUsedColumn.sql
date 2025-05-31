-- Fix the WorkFromHomeUsed column name in AnnualLeave table
IF EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[AnnualLeave]') 
    AND name = '
WorkFromHomeUsed'
)
BEGIN
    EXEC sp_rename '[dbo].[AnnualLeave].[
WorkFromHomeUsed]', 'WorkFromHomeUsed', 'COLUMN';
    
    PRINT 'Renamed column from [
WorkFromHomeUsed] to [WorkFromHomeUsed] in AnnualLeave table';
END
ELSE IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[AnnualLeave]') 
    AND name = 'WorkFromHomeUsed'
)
BEGIN
    ALTER TABLE [dbo].[AnnualLeave]
    ADD [WorkFromHomeUsed] INT NOT NULL DEFAULT 0;
    
    PRINT 'Added WorkFromHomeUsed column to AnnualLeave table';
END
ELSE
BEGIN
    PRINT 'WorkFromHomeUsed column already exists with correct name in AnnualLeave table';
END
GO