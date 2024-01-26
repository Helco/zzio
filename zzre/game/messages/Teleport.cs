using System;

namespace zzre.game.messages;

public readonly record struct Teleport(int sceneId, int targetEntry);
