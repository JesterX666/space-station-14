using Content.Server.Construction.Completions;
using Content.Server.Construction.Conditions;
using Content.Server.Hands.Systems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Destructible;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Kitchen;
using Content.Shared.Kitchen.Components;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.PowerCell;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Kitchen.EntitySystems;
public sealed class DeepfryerSystem : SharedDeepfryerSystem
{
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly PowerReceiverSystem _powerSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly HandsSystem _handsSystem = default!;
    [Dependency] private readonly SharedItemSystem _itemSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly SharedDestructibleSystem _destroySystem = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly ProtoId<TagPrototype> WrenchTag = "Wrench";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DeepFryerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<DeepFryerComponent, EntInsertedIntoContainerMessage>(OnContentInsertedUpdate);
        SubscribeLocalEvent<DeepFryerComponent, EntRemovedFromContainerMessage>(OnContentRemovedUpdate);
        SubscribeLocalEvent<DeepFryerComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<DeepFryerComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<DeepFryerComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<DeepFryerComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<DeepFryerComponent, AnchorStateChangedEvent>(OnAnchorChanged);
    }

    private void OnInteractHand(Entity<DeepFryerComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (!(TryComp<ApcPowerReceiverComponent>(ent, out var apc) && apc.Powered))
        {
            _popupSystem.PopupEntity(Loc.GetString("deepfryer-component-interact-using-no-power"), ent, args.User);
            return;
        }

        _containerSystem.EmptyContainer(ent.Comp.Storage);

        args.Handled = true;
    }

    private void OnInteractUsing(Entity<DeepFryerComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // check if you're trying to put in a wrench (the wrench (un)anchors it to the floor)
        if (_tagSystem.HasTag(args.Used, WrenchTag))
        {
            return;
        }

        if (!(TryComp<ApcPowerReceiverComponent>(ent, out var apc) && apc.Powered))
        {
            _popupSystem.PopupEntity(Loc.GetString("deepfryer-component-interact-using-no-power"), ent, args.User);
            return;
        }

        if (TryComp<ItemComponent>(args.Used, out var item))
        {
            // check if size of an item you're trying to put in is too big
            if (_itemSystem.GetSizePrototype(item.Size) > _itemSystem.GetSizePrototype(ent.Comp.MaxItemSize))
            {
                _popupSystem.PopupEntity(Loc.GetString("deepfryer-component-interact-item-too-big", ("item", args.Used)), ent, args.User);
                return;
            }
        }
        else
        {
            // check if thing you're trying to put in isn't an item
            _popupSystem.PopupEntity(Loc.GetString("deepfryer-component-interact-using-transfer-fail"), ent, args.User);
            return;
        }

        if (ent.Comp.Storage.Count >= ent.Comp.Capacity)
        {
            _popupSystem.PopupEntity(Loc.GetString("deepfryer-component-interact-full"), ent, args.User);
            return;
        }

        args.Handled = true;
        _handsSystem.TryDropIntoContainer(args.User, args.Used, ent.Comp.Storage);
    }

    private void OnInit(Entity<DeepFryerComponent> ent, ref ComponentInit args)
    {
        // this really does have to be in ComponentInit
        ent.Comp.Storage = _containerSystem.EnsureContainer<Container>(ent, ent.Comp.ContainerId);
    }

    private void OnContentInsertedUpdate(EntityUid uid, DeepFryerComponent component, EntInsertedIntoContainerMessage args)
    {
        if (component.Storage != args.Container)
            return;

        if (TryComp<FryableComponent>(args.Entity, out var fryable))
        {
            fryable.CurrentDipFryStart = _timing.CurTime;
        }

        // We only want to play the sound and change the appearance when the first item is inserted.
        if (component.Storage.Count == 1)
        {
            _audioSystem.PlayPvs(component.BasketInsertingSound, uid);
            component.BasketInsertingSoundTimer = _timing.CurTime;
            component.CurrentFryingTimer = _timing.CurTime;
            SetAppearance(uid, DeepFryerVisualState.StartFrying, component);
        }
        else
        {
            AudioParams audioParams = AudioParams.Default.WithVolume(-3f);
            _audioSystem.PlayPvs(component.AddItemWhileFryingSound, uid, audioParams);
        }
    }

    private void OnContentRemovedUpdate(EntityUid uid, DeepFryerComponent component, EntRemovedFromContainerMessage args)
    {
        if (component.Storage != args.Container)
            return;

        if (TryComp<FryableComponent>(args.Entity, out var fryable))
        {
            fryable.CumulativeFryTime += _timing.CurTime - fryable.CurrentDipFryStart;

            if (fryable.CumulativeFryTime > TimeSpan.FromSeconds(fryable.BurnTime))
            {
                // Item is burnt
                ReplaceFryableWith(args.Entity, fryable.BurntResult);
            }
            else if (!string.IsNullOrEmpty(fryable.Result) && fryable.CumulativeFryTime > TimeSpan.FromSeconds(fryable.CookTime))
            {
                // Item is cooked
                ReplaceFryableWith(args.Entity, fryable.Result);
            }
        }

        if (!component.Stopping)
        {
            _audioSystem.Stop(component.CurrentFryingSound);
            _audioSystem.PlayPvs(component.BasketRetractingSound, uid);
            SetAppearance(uid, DeepFryerVisualState.StopFrying, component);
            component.Stopping = true;
        }

        if (component.Storage.Count == 0)
        {
            if (component.CurrentFryingTimer != null)
            {
                TimeSpan delta = _timing.CurTime - component.CurrentFryingTimer.Value;
                if (component.CumulativeFryingTime == TimeSpan.Zero)
                    component.CumulativeFryingTime = delta;
                else
                    component.CumulativeFryingTime += delta;
            }
            component.CurrentFryingTimer = null;
            component.Stopping = false;
        }
    }

    private void ReplaceFryableWith(EntityUid fryedEntity, string result)
    {
        var coords = _transformSystem.GetMapCoordinates(fryedEntity);
        Spawn(result, coords);

        _destroySystem.DestroyEntity(fryedEntity);
    }

    private void OnInsertAttempt(Entity<DeepFryerComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID != ent.Comp.ContainerId)
            return;

        if (TryComp<ItemComponent>(args.EntityUid, out var item))
        {
            if (_itemSystem.GetSizePrototype(item.Size) > _itemSystem.GetSizePrototype(ent.Comp.MaxItemSize))
            {
                args.Cancel();
                return;
            }
        }
        else
        {
            args.Cancel();
            return;
        }

        if (ent.Comp.Storage.Count >= ent.Comp.Capacity)
            args.Cancel();
    }

    private void OnPowerChanged(Entity<DeepFryerComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered)
        {
            SetAppearance(ent, DeepFryerVisualState.Idle, ent.Comp);
            //StopCooking(ent);
        }
    }

    private void OnAnchorChanged(EntityUid uid, DeepFryerComponent component, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
            _containerSystem.EmptyContainer(component.Storage);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DeepFryerComponent>();
        while (query.MoveNext(out var ent, out var component))
        {
            // Update frying sound
            var elapsed = _timing.CurTime - component.BasketInsertingSoundTimer;
            if ((component.BasketInsertingSoundTimer != null) && elapsed > TimeSpan.FromSeconds(1))
            {
                component.BasketInsertingSoundTimer = null;
                AudioParams audioParams = AudioParams.Default.WithVolume(-4f).WithLoop(true);
                var sound = _audioSystem.PlayPvs(component.FryingSound, ent, audioParams);
                if (sound != null)
                    component.CurrentFryingSound = sound.Value.Entity;
            }

            // If we have been frying for a giving amount of time, makes the fryer greasy looking
            if (!component.Greasy && (component.TimeUntilGreasy != null) && (component.CurrentFryingTimer != null) &&
                (component.CumulativeFryingTime + (_timing.CurTime - component.CurrentFryingTimer) >= TimeSpan.FromSeconds(component.TimeUntilGreasy.Value)))
            {
                component.Greasy = true;
                _appearanceSystem.RemoveData(ent, DeepFryerVisuals.Operating);
                _appearanceSystem.SetData(ent, DeepFryerVisuals.Greasy, true);
            }
        }
    }

    private void SetAppearance(EntityUid uid, DeepFryerVisualState state, DeepFryerComponent? component = null, AppearanceComponent? appearanceComponent = null)
    {
        if (!Resolve(uid, ref component, ref appearanceComponent, false))
            return;
        _appearanceSystem.SetData(uid, DeepFryerVisuals.Operating, state, appearanceComponent);
    }
}
