
using MemoryPack;

/// <summary>
/// A single material requirement or output in a <see cref="CraftingRecipe"/>.
/// </summary>
[MemoryPackable]
public partial class Ingredient
{
    /// <summary>Block type ID of this ingredient or output material.</summary>
    public int Type { get; set; }

    /// <summary>Number of items required (for inputs) or produced (for output).</summary>
    public int Amount { get; set; }
}
