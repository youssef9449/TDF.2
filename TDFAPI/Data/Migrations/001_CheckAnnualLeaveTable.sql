-- Check the structure of the AnnualLeave table
SELECT 
    c.name AS ColumnName,
    t.name AS DataType,
    c.max_length AS MaxLength,
    c.is_nullable AS IsNullable,
    c.is_identity AS IsIdentity,
    OBJECT_NAME(c.default_object_id) AS DefaultConstraintName,
    dc.definition AS DefaultDefinition
FROM 
    sys.columns c
INNER JOIN 
    sys.types t ON c.user_type_id = t.user_type_id
LEFT JOIN 
    sys.default_constraints dc ON c.default_object_id = dc.object_id
WHERE 
    c.object_id = OBJECT_ID('dbo.AnnualLeave')
ORDER BY 
    c.column_id;
GO