using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using zzio;
using zzio.db;
using zzre.rendering;

namespace zzre.game.systems.ui;

public partial class ScrNotification : BaseScreen<components.ui.ScrNotification, messages.ui.Notification>
{
    private const float YOffset = -OverlayHeight / 2;
    private const float OverlayHeight = 100f;
    private const float BorderHeight = 2f;
    private const int RenderOrder = -1000;
    private static readonly components.ui.ElementId ButtonElementId = new(-1000); // not original but avoids id conflicts
    private static readonly FColor BorderColor = new IColor(0xff, 0xea, 0xB7, 0);

    public ScrNotification(ITagContainer diContainer) : base(diContainer, BlockFlags.None)
    {
        World.SetMaxCapacity<components.ui.ScrNotification>(int.MaxValue);
        OnElementDown += HandleElementDown;
    }

    protected override void HandleOpen(in messages.ui.Notification message)
    {
        if (!message.Texts.Any())
            throw new ArgumentException("Notification message does not contain any texts");

        var entity = World.CreateEntity();
        entity.Set<components.ui.ScrNotification>();
        ref var component = ref entity.Get<components.ui.ScrNotification>();
        component.Message = message;
        component.TimeLeft = message.Duration;

        var screenSize = ui.LogicalScreen.Size;
        component.MainOverlay = preload.CreateImage(entity)
            .With(UIPreloader.DefaultOverlayColor)
            .With(components.ui.FullAlignment.Center)
            .With(Rect.FromTopLeftSize(new(0, YOffset), new(screenSize.X, OverlayHeight)))
            .With(components.ui.Fade.SingleIn(1f))
            .WithRenderOrder(RenderOrder + 1)
            .Build();

        component.TopBorder = preload.CreateImage(entity)
            .With(BorderColor)
            .With(components.ui.FullAlignment.BottomCenter)
            .With(Rect.FromTopLeftSize(new(0f, YOffset - OverlayHeight / 2), new(screenSize.X, BorderHeight)))
            .With(components.ui.Fade.StdIn)
            .WithRenderOrder(RenderOrder)
            .Build();

        component.BottomBorder = preload.CreateImage(entity)
            .With(BorderColor)
            .With(components.ui.FullAlignment.TopCenter)
            .With(Rect.FromTopLeftSize(new(0f, YOffset + OverlayHeight / 2), new(screenSize.X, BorderHeight)))
            .With(components.ui.Fade.StdIn)
            .WithRenderOrder(RenderOrder)
            .Build();

        component.TextLabel = preload.CreateLabel(entity)
            .With(components.ui.FullAlignment.TopCenter)
            .With(new Vector2(45, YOffset - 12))
            .With(message.SmallFont ? preload.Fnt000 : preload.Fnt001)
            .WithText(message.Texts.First())
            .WithAnimation(2)
            .WithRenderOrder(RenderOrder)
            .Build();

        if (message.Icon.HasValue)
        {
            var textFont = component.TextLabel.Get<TileSheet>();
            var textWidth = textFont.GetUnformattedWidth(message.Texts.First());
            component.IconImage = preload.CreateImage(entity)
                .With(message.Icon.Value)
                .With(new Vector2(-textWidth / 2, YOffset - 20))
                .WithRenderOrder(RenderOrder)
                .Build();
        }

        if (message.Button != null)
        {
            component.Button = preload.CreateButton(entity)
                .With(ButtonElementId)
                .With(components.ui.FullAlignment.Center)
                .With(new Vector2(0, YOffset))
                .With(new components.ui.ButtonTiles(17, 18))
                .With(preload.Btn000)
                .WithLabel()
                    .With(preload.Fnt000)
                    .WithText(message.Button)
                .WithRenderOrder(RenderOrder)
                .Build();
        }
    }

    private void HandleElementDown(DefaultEcs.Entity buttonEntity, components.ui.ElementId id)
    {
        var screenEntities = Set.GetEntities();
        if (id != ButtonElementId || screenEntities.Length < 1)
            return;
        ref var component = ref screenEntities[0].Get<components.ui.ScrNotification>();
        buttonEntity.Set<components.Disabled>();
        component.TimeLeft = 0f;
        component.WasButtonClicked = true;
    }

    protected override void Update(float timeElapsed, in DefaultEcs.Entity entity, ref components.ui.ScrNotification component)
    {
        if (component.IsFading)
        {
            if (!component.MainOverlay.IsAlive)
            {
                entity.Set<components.Dead>();
                component.Message.ResultAction(component.WasButtonClicked);
            }
            return;
        }

        component.TimeLeft -= timeElapsed;
        if (component.TimeLeft <= 0f)
        {
            var nextIndex = ++component.CurTextIndex;
            if (nextIndex < component.Message.Texts.Length)
            {
                component.TimeLeft = component.Message.Duration;
                component.TextLabel.Set(new components.ui.Label(component.Message.Texts[nextIndex]));
            }
            else
            {
                component.IsFading = true;
                component.TextLabel.Dispose();
                component.IconImage?.Dispose();
                component.Button?.Dispose();
                component.MainOverlay.Set(components.ui.Fade.SingleOut(1f));
                component.TopBorder.Set(components.ui.Fade.StdOut);
                component.BottomBorder.Set(components.ui.Fade.StdOut);
            }
        }
    }
}
