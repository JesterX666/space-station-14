using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Kitchen.Components;


/// <summary>
/// Used to mark entity that can be fried.
/// </summary>
[RegisterComponent]
public sealed partial class FryableComponent : Component
{
    /// <summary>
    /// The result entity of frying the item.
    /// </summary>
    [DataField("result", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string Result { get; private set; } = string.Empty;

    /// <summary>
    /// Cook time in seconds.
    /// </summary>
    [DataField("cookTime")]
    public uint CookTime { get; private set; } = 10;

    /// <summary>
    /// What the item turns into if it gets burnt.
    /// </summary>
    [DataField("burntResult", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string BurntResult { get; private set; } = "FoodBadRecipe";

    /// <summary>
    /// The time in seconds it takes to burn the item after being cooked.
    /// </summary>
    [DataField("burnTime")]
    public int BurnTime { get; private set; } = 25;

    /// <summary>
    /// The cumulative time the item has been fried for.
    /// </summary>
    [ViewVariables]
    public TimeSpan CumulativeFryTime = TimeSpan.Zero;

    /// <summary>
    /// The starting time the item has been fried in the current dipping.
    /// </summary>
    [ViewVariables]
    public TimeSpan CurrentDipFryStart;
}
