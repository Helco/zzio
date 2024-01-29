using System;

namespace zzre.game.messages;

public record struct StartDialog(DefaultEcs.Entity NpcEntity, components.DialogCause Cause, int CatchItemId = -1);
