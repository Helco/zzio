using System;

namespace zzre.game.components.ui
{
    public record struct Label(string Text, bool DoFormat = true);

    public record struct LabelNeedsTiling;
    public record struct SubLabel;
}
