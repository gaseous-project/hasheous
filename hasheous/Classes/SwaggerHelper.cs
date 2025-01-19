internal static class SwaggerSchemaHelper
{
    private static readonly Dictionary<string, int> _schemaNameRepetition = new Dictionary<string, int>();

    public static string GetSchemaId(Type type)
    {
        string id;

        // full name for classes starting with "HasheousClient.Models."
        if (type.FullName != null && type.FullName.StartsWith("HasheousClient.Models."))
        {
            id = type.FullName;
        }
        else
        {
            id = type.Name;
        }

        // fix "+" in type names
        id = id.Replace("+", ".");

        return id;
    }
}