using Content.Shared.DeviceLinking;
using Content.Shared.Item;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Kitchen.Components
{
    [RegisterComponent, NetworkedComponent]
    public sealed partial class DeepFryerComponent : Component
    {
        public Container Storage = default!;

        [DataField]
        public string ContainerId = "deepfryer_entity_container";

        [DataField, ViewVariables(VVAccess.ReadWrite)]
        public ProtoId<SinkPortPrototype> OnPort = "On";

        [DataField, ViewVariables(VVAccess.ReadWrite)]
        public ProtoId<ItemSizePrototype> MaxItemSize = "Normal";

        /// <summary>
        /// Number of items that can be fried at once.
        /// </summary>
        [DataField("capacity")]
        public byte Capacity;

        /// <summary>
        /// The state the fryer shows when frying.
        /// </summary>
        [DataField("frying")]
        public string? Frying;

        /// <summary>
        /// The state the fryer shows when idle.
        /// </summary>
        [DataField("idle")]
        public string? Idle;

        /// <summary>
        /// The state the fryer shows when the basket is inserted in the hot oil.
        /// </summary>
        [DataField("basket_inserting")]
        public string? BasketInserting;

        /// <summary>
        /// The state the fryer shows when the basket is lifted from the hot oil.
        /// </summary>
        [DataField("basket_retracting")]
        public string? BasketRetracting;

        /// <summary>
        /// Is the fryer greasy.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public bool Greasy = false;

        [DataField("grease_color")]
        public Color GreaseColor;

        /// <summary>
        /// The amount of time the fryer has been cooking for cumulative purposes.
        /// After a certain threshold, the fryer becomes greasy.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public TimeSpan CumulativeFryingTime = TimeSpan.Zero;

        /// <summary>
        /// The sound played when the fryer basket is inserted.
        /// </summary>
        [DataField("basket_inserting_sound"), ViewVariables]
        public SoundSpecifier? BasketInsertingSound;

        /// <summary>
        /// The sound played when the fryer basket is retracted.
        /// </summary>
        [DataField("basket_retracting_sound"), ViewVariables]
        public SoundSpecifier? BasketRetractingSound;

        /// <summary>
        /// The sound played when the fryer basket is inserted.
        /// </summary>
        [DataField("frying_sound"), ViewVariables]
        public SoundSpecifier? FryingSound;

        /// <summary>
        /// The sound played when another item is inserted while already frying.
        /// </summary>
        [DataField("add_item_sound"), ViewVariables]
        public SoundSpecifier? AddItemWhileFryingSound;

        [DataField("time_until_greasy"), ViewVariables]
        public float? TimeUntilGreasy;

        /// <summary>
        /// Timer to track when to stop the basket inserting sound.
        /// </summary>
        [ViewVariables]
        public TimeSpan? BasketInsertingSoundTimer;

        /// <summary>
        /// Timer to track the current frying time.
        /// </summary>
        [ViewVariables]
        public TimeSpan? CurrentFryingTimer;

        /// <summary>
        /// Current playing frying sound entity.
        /// </summary>
        [ViewVariables]
        public EntityUid? CurrentFryingSound;

        /// <summary>
        /// Are we currently stopping the frying.  (So we don't stack basket retracting sounds)
        /// </summary>
        [ViewVariables]
        public bool Stopping;
    }

    public sealed class BeingFryedEvent : HandledEntityEventArgs
    {
        public EntityUid Fryer;
        public EntityUid? User;

        public BeingFryedEvent(EntityUid fryer, EntityUid? user)
        {
            Fryer = fryer;
            User = user;
        }
    }

    [Serializable, NetSerializable]
    public enum DeepFryerVisuals
    {
        Operating,
        Greasy
    }

    [Serializable, NetSerializable]
    public enum DeepFryerVisualState
    {
        Idle,
        Frying,
        StartFrying,
        StopFrying
    }
}
