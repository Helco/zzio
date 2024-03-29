﻿using DefaultEcs;

namespace zzre.game.uibuilder;

internal sealed record TooltipTarget : LabelLike<TooltipTarget>
{
    public TooltipTarget(UIBuilder preload, Entity parent) : base(preload, parent)
    {
        font = UIPreloadAsset.Fnt002;
    }

    public static implicit operator Entity(TooltipTarget builder) => builder.Build();

    public Entity Build()
    {
        var prefix = text;
        text = "";
        var entity = BuildBase();
        entity.Set(new components.ui.TooltipTarget(prefix));
        return entity;
    }
}
