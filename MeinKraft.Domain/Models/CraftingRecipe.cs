using MemoryPack;

/// <summary>
/// Defines a crafting recipe: a set of required input materials
/// and the item produced when those inputs are consumed.
/// </summary>
[MemoryPackable]
public partial class CraftingRecipe
{
    /// <summary>
    /// Input materials required to craft this recipe.
    /// All ingredients must be present in the player's inventory
    /// in the specified amounts.
    /// </summary>
    public Ingredient[]? Ingredients { get; set; }

    /// <summary>The item produced when this recipe is crafted successfully.</summary>
    public Ingredient? Output { get; set; }
}
