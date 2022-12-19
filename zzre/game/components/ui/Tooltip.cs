using zzio;

namespace zzre.game.components.ui
{
    public record struct TooltipTarget(string Prefix);

    public record struct TooltipText(string Text)
    {
        public static implicit operator TooltipText(string text) => new(text);
    }

    public record struct TooltipUID(UID UID)
    {
        public static implicit operator TooltipUID(UID uid) => new(uid);
    }
}
