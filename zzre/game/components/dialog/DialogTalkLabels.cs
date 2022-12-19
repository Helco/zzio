﻿using System;

namespace zzre.game.components
{
    public record struct DialogTalkLabels(bool IsLast, int LabelYes, int LabelNo)
    {
        public static readonly DialogTalkLabels Exit = new(IsLast: true, -1, -1);
        public static readonly DialogTalkLabels Continue = new(IsLast: false, -1, -1);
        public static DialogTalkLabels YesNo(int labelYes, int labelNo) =>
            new(IsLast: false, labelYes, labelNo);
    }
}
