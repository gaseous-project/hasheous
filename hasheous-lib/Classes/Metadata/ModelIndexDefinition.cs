namespace hasheous_lib.Classes.Metadata;

/// <summary>
/// Declarative SQL index metadata used by model importers.
/// Supports both single-column and composite indexes.
/// </summary>
public sealed class ModelIndexDefinition
{
    public required string Name { get; init; }
    public required string[] Columns { get; init; }
    public bool Unique { get; init; }

    public static ModelIndexDefinition Single(string name, string column, bool unique = false)
    {
        return new ModelIndexDefinition
        {
            Name = name,
            Columns = new[] { column },
            Unique = unique
        };
    }

    public static ModelIndexDefinition Composite(string name, bool unique = false, params string[] columns)
    {
        return new ModelIndexDefinition
        {
            Name = name,
            Columns = columns,
            Unique = unique
        };
    }
}
