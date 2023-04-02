using System;
using zzio;

namespace zzre.game.messages.ui;

public record struct Notification(string[] Texts, CardId? Icon)
{
    public bool SmallFont { get; init; }
    public float Duration { get; init; } = 3f;
    public string? Button { get; init; }
    public Action<bool> ResultAction { get; init; } = _ => { };
}
