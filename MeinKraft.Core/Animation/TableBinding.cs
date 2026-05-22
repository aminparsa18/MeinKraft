/// <summary>
/// Defines the contract for reading and writing a structured table-based data model.
/// Implemented by model-specific bindings such as <see cref="AnimatedModelBinding"/>.
/// </summary>
public interface ITableBinding
{
    /// <summary>
    /// Sets a single field on the record at <paramref name="index"/>
    /// in the given <paramref name="table"/>.
    /// </summary>
    void Set(string table, int index, string column, string value);

    /// <summary>
    /// Populates <paramref name="items"/> with all fields of the record
    /// at <paramref name="index"/> in the given <paramref name="table"/>.
    /// </summary>
    void Get(string table, int index, Dictionary<string, string> items);

    /// <summary>
    /// Populates <paramref name="names"/> and <paramref name="counts"/> with
    /// the name and record count of each table in the model.
    /// </summary>
    void GetTables(string[] names, int[] counts);
}