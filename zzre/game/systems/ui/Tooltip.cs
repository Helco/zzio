using System;
using DefaultEcs.System;
using zzio.db;

namespace zzre.game.systems.ui;

[With(
    typeof(components.ui.TooltipTarget),
    typeof(components.ui.Label))]
public partial class Tooltip : AEntitySetSystem<float>
{
    private readonly IDisposable removedSubscription;
    private readonly IDisposable addedSubscription;
    private readonly MappedDB mappedDB;

    public Tooltip(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        mappedDB = diContainer.GetTag<MappedDB>();
        addedSubscription = World.SubscribeEntityComponentAdded<components.ui.Hovered>(HandleAddedComponent);
        removedSubscription = World.SubscribeEntityComponentRemoved<components.ui.Hovered>(HandleRemovedComponent);
    }

    public override void Dispose()
    {
        base.Dispose();
        removedSubscription.Dispose();
        addedSubscription.Dispose();
    }

    private void HandleAddedComponent(in DefaultEcs.Entity hovered, in components.ui.Hovered _)
    {
        string text = hovered switch
        {
            _ when hovered.TryGet<components.ui.TooltipText>(out var tooltip) => tooltip.Text,
            _ when hovered.TryGet<components.ui.TooltipUID>(out var tooltip) => mappedDB.GetText(tooltip.UID).Text,
            _ => ""
        };

        if (string.IsNullOrWhiteSpace(text))
        {
            // disable all targets
            HandleRemovedComponent(hovered, _);
            return;
        }

        foreach (var entity in Set.GetEntities())
        {
            var tooltip = entity.Get<components.ui.TooltipTarget>();
            entity.Set(new components.ui.Label(tooltip.Prefix + text));
        }
    }

    private void HandleRemovedComponent(in DefaultEcs.Entity __, in components.ui.Hovered _)
    {
        foreach (var entity in Set.GetEntities())
            // setting visibility does not work due to sublabels not recognizing that change
            entity.Set(new components.ui.Label(""));
    }

    [Update]
    private static void Update() { }
}
