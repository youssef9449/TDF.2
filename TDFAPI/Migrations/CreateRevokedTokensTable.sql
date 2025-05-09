-- Script to create RevokedTokens table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RevokedTokens]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[RevokedTokens](
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [Jti] [nvarchar](100) NOT NULL,
        [UserId] [int] NOT NULL,
        [ExpiryDate] [datetime2](7) NOT NULL,
        [RevokedDate] [datetime2](7) NOT NULL,
        [RevokedByIp] [nvarchar](50) NULL,
        PRIMARY KEY CLUSTERED 
        (
            [Id] ASC
        )
    )

    CREATE INDEX [IX_RevokedTokens_Jti] ON [dbo].[RevokedTokens] ([Jti])
    CREATE INDEX [IX_RevokedTokens_UserId] ON [dbo].[RevokedTokens] ([UserId])
    CREATE INDEX [IX_RevokedTokens_ExpiryDate] ON [dbo].[RevokedTokens] ([ExpiryDate])
    
    PRINT 'RevokedTokens table created successfully.'
END
ELSE
BEGIN
    PRINT 'RevokedTokens table already exists.'
END 