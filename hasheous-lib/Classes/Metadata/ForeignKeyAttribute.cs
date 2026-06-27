namespace hasheous_lib.Classes.Metadata;

/// <summary>
/// Marks a property as a foreign key reference to another table.
/// When applied to a model property, the importer will:
/// 1. Extract and deduplicate the property values into a referenced table
/// 2. Replace the property value with the auto-generated ID from the referenced table during insert
///
/// If no ReferencedModelType is provided, a simple table with Id (auto-increment) and Name fields is auto-created.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ForeignKeyAttribute : Attribute
{
    /// <summary>Name of the referenced table that will be created or updated.</summary>
    public string ReferencedTableName { get; set; }

    /// <summary>
    /// Optional model type for the referenced table schema.
    /// If null, a simple table with Id (BIGINT auto-increment PK) and Name (VARCHAR) columns is created.
    /// If provided, the type's scalar properties define the table schema (similar to normal model import).
    /// </summary>
    public Type? ReferencedModelType { get; set; }

    /// <summary>
    /// Column name used to find the referenced row in typed references.
    /// Must be explicitly provided when ReferencedModelType is set.
    /// </summary>
    public string? ReferencedLookupColumn { get; set; }

    /// <summary>
    /// Column name read back as the resolved foreign key value in typed references.
    /// Must be explicitly provided when ReferencedModelType is set.
    /// </summary>
    public string? ReferencedIdColumn { get; set; }

    /// <summary>
    /// Creates a foreign key attribute that references a simple Id+Name deduplication table.
    /// </summary>
    public ForeignKeyAttribute(string referencedTableName)
    {
        ReferencedTableName = referencedTableName;
        ReferencedModelType = null;
        ReferencedLookupColumn = null;
        ReferencedIdColumn = null;
    }

    /// <summary>
    /// Creates a foreign key attribute that references a table derived from a model type,
    /// explicitly providing lookup and ID column names.
    /// </summary>
    public ForeignKeyAttribute(string referencedTableName, Type referencedModelType, string referencedLookupColumn, string referencedIdColumn)
    {
        ReferencedTableName = referencedTableName;
        ReferencedModelType = referencedModelType;
        ReferencedLookupColumn = referencedLookupColumn;
        ReferencedIdColumn = referencedIdColumn;
    }
}
