using System;
using System.Collections.Generic;
using System.Linq;

namespace zzre.game.messages;

public record struct CreatureSetVisibility(DefaultEcs.Entity Entity, bool IsVisible);
