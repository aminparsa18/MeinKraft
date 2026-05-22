/// <summary>
/// Serializes and deserializes tab-separated, section-based data files
/// into a model via an <see cref="ITableBinding"/>.
/// </summary>
public class TableSerializer
{
    private const string SectionPrefix = "section=";
    private const string TabSeparator = "\t";

    /// <summary>
    /// Parses <paramref name="data"/> and populates the model
    /// through <paramref name="binding"/>.
    /// </summary>
    /// <exception cref="FormatException">
    /// Thrown when a section header is missing or a row has more
    /// columns than the header.
    /// </exception>
    public static void Deserialize(string data, ITableBinding binding)
    {
        string[] lines = data.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        string[] header = null;
        string section = "";
        int rowIndex = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            if (line == "")
            {
                continue;
            }

            if (line.StartsWith("//", StringComparison.InvariantCultureIgnoreCase))
            {
                continue;
            }

            if (line.StartsWith("#", StringComparison.InvariantCultureIgnoreCase))
            {
                continue;
            }

            if (line.StartsWith(SectionPrefix, StringComparison.InvariantCultureIgnoreCase))
            {
                section = line.Replace(SectionPrefix, "");

                if (i + 1 >= lines.Length)
                {
                    throw new FormatException($"Section '{section}' has no header row.");
                }

                header = lines[++i].Trim().Split(TabSeparator);
                rowIndex = 0;
                continue;
            }

            if (header == null)
            {
                throw new FormatException($"Data row found before any section declaration: '{line}'");
            }

            string[] columns = line.Split(TabSeparator);

            if (columns.Length > header.Length)
            {
                throw new FormatException(
                    $"Row {rowIndex} in section '{section}' has {columns.Length} columns but header has {header.Length}.");
            }

            for (int k = 0; k < columns.Length; k++)
            {
                binding.Set(section, rowIndex, header[k], columns[k]);
            }

            rowIndex++;
        }
    }
}
