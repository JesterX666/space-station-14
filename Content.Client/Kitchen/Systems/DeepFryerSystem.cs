using Content.Client.Kitchen.Visualizers;
using Content.Shared.Kitchen;
using Content.Shared.Kitchen.Components;
using Robust.Client.Animations;
using Robust.Client.GameObjects;

namespace Content.Client.Kitchen.Systems;
public sealed class DeepFryerSystem : SharedDeepfryerSystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly AnimationPlayerSystem _animationPlayer = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DeepFryerComponent, AppearanceChangeEvent>(OnAppearanceChange);
        SubscribeLocalEvent<DeepFryerComponent, AnimationCompletedEvent>(OnAnimationCompleted);
    }

    private void OnAnimationCompleted(EntityUid uid, DeepFryerComponent component, AnimationCompletedEvent args)
    {
        if (!args.Finished)
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        // Apply the next state based on which animation just finished
        if (args.Key == component.BasketInserting)
        {
            UpdateAppearance((uid, sprite), DeepFryerVisualState.Frying, component);
        }
        else if (args.Key == component.BasketRetracting)
        {
            UpdateAppearance((uid, sprite), DeepFryerVisualState.Idle, component);
        }
        else if (args.Key == component.Frying)
        {
            // After frying animation, keep playing frying animation (until told otherwise)
            PlayAnimation((uid, sprite), component.Frying, DeepFryerVisualizerLayers.Frying, 0.6f);
        }
    }

    private void OnAppearanceChange(EntityUid uid, DeepFryerComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (args.AppearanceData.ContainsKey(DeepFryerVisuals.Operating))
        {
            if (!args.AppearanceData.TryGetValue(DeepFryerVisuals.Operating, out var visualStateValue) ||
                visualStateValue is not DeepFryerVisualState visualState)
                visualStateValue = DeepFryerVisualState.Idle;

            UpdateAppearance((uid, args.Sprite), (DeepFryerVisualState)visualStateValue, component);
        }

        if (args.AppearanceData.TryGetValue(DeepFryerVisuals.Greasy, out var greasyObj) &&
            greasyObj is bool isGreasy)
        {
            _sprite.LayerSetVisible(uid, DeepFryerVisualizerLayers.Greasy, isGreasy);
            _sprite.LayerSetColor(uid, DeepFryerVisualizerLayers.Greasy, component.GreaseColor);
        }
    }

    private void UpdateAppearance(Entity<SpriteComponent> entity, DeepFryerVisualState visualState, DeepFryerComponent component)
    {
        string? state = null;
        DeepFryerVisualizerLayers? layer = null;
        float animationTime = 1.0f;
        switch (visualState)
        {
            case DeepFryerVisualState.Idle:
                state = component.Idle;
                layer = DeepFryerVisualizerLayers.Idle;
                animationTime = 0.0f;
                break;
            case DeepFryerVisualState.Frying:
                state = component.Frying;
                layer = DeepFryerVisualizerLayers.Frying;
                animationTime = 0.6f;
                break;
            case DeepFryerVisualState.StartFrying:
                state = component.BasketInserting;
                layer = DeepFryerVisualizerLayers.Inserting;
                animationTime = 0.5f;
                break;
            case DeepFryerVisualState.StopFrying:
                state = component.BasketRetracting;
                layer = DeepFryerVisualizerLayers.Retracting;
                animationTime = 0.4f;
                break;
        }

        if (string.IsNullOrEmpty(state) || layer == null)
            return;

        foreach (DeepFryerVisualizerLayers layerName in Enum.GetValues(typeof(DeepFryerVisualizerLayers)))
        {
            if (layerName != DeepFryerVisualizerLayers.Greasy)
                _sprite.LayerSetVisible(entity.AsNullable(), layerName, layerName == layer);
        }

        PlayAnimation(entity, state, layer, animationTime);
    }

    private void PlayAnimation(Entity<SpriteComponent> entity, string state, DeepFryerVisualizerLayers? layer, float animationTime)
    {
        if (string.IsNullOrEmpty(state) || layer == null)
            return;

        if (!_animationPlayer.HasRunningAnimation(entity, state))
        {
            var animation = GetAnimation(layer.Value, state, animationTime);
            _animationPlayer.Play(entity, animation, state);
        }
    }

    private static Animation GetAnimation(DeepFryerVisualizerLayers layer, string state, float animationTime)
    {
        return new Animation
        {
            Length = TimeSpan.FromSeconds(animationTime),
            AnimationTracks =
                {
                    new AnimationTrackSpriteFlick
                    {
                        LayerKey = layer,
                        KeyFrames =
                        {
                            new AnimationTrackSpriteFlick.KeyFrame(state, 0f)
                        }
                    }
                }
        };
    }

    private void SetLayerState(DeepFryerVisualizerLayers layer, string? state, bool isGreasy, Entity<SpriteComponent> sprite)
    {
        if (string.IsNullOrEmpty(state))
            return;

        _sprite.LayerSetVisible(sprite.AsNullable(), layer, true);
        _sprite.LayerSetAutoAnimated(sprite.AsNullable(), layer, true);
        _sprite.LayerSetRsiState(sprite.AsNullable(), layer, state);
    }
}
