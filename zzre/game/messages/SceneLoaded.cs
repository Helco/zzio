namespace zzre.game.messages;

using System.Collections.Generic;
using zzio;
using zzio.scn;

public readonly record struct SceneLoaded(Scene Scene, Savegame Savegame)
{
    public IEnumerable<IGameStateMod> GetGameState() =>
        Savegame.GetGameStateFor(Scene.dataset.sceneId);
    public IEnumerable<TMod> GetGameState<TMod>() where TMod : IGameStateMod =>
        Savegame.GetGameStateFor<TMod>(Scene.dataset.sceneId);
}
