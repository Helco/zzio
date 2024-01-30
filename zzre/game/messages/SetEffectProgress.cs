using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace zzre.game.messages;

public readonly record struct SetEffectProgress(DefaultEcs.Entity parent, float progress, Vector3 dir)
{
    // TODO: Check if necessary
}
