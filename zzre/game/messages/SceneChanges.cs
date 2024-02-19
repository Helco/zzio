namespace zzre.game.messages;

using System.Collections.Generic;
using zzio;
using zzio.scn;

// The messages are published in order of declaration:

/// <summary>
/// Triggered at the start of a fade-out-fade-in process for changing scenes or teleporting to a different sectioln
/// </summary>
/// <param name="NextScene">The name of the next scene (could be the current one)</param>
public record struct PlayerLeaving(string NextScene);

/// <summary>
/// Triggered to clear all scene-specific entities to prepare for loading
/// </summary>
/// <remarks>Use this to clear scene-specific resources and entities</remarks>
/// <param name="NextScene">The name of the next scene</param>
public record struct SceneChanging(string NextScene);

/// <summary>
/// Triggered after loading next scene
/// </summary>
/// <param name="Scene">The loaded scene data</param>
/// <param name="Savegame">The current savegame</param>
public readonly record struct SceneLoaded(Scene Scene, Savegame Savegame)
{
    public IEnumerable<IGameStateMod> GetGameState() =>
        Savegame.GetGameStateFor(Scene.dataset.sceneId);
    public IEnumerable<TMod> GetGameState<TMod>() where TMod : IGameStateMod =>
        Savegame.GetGameStateFor<TMod>(Scene.dataset.sceneId);
}

/// <summary>
/// Triggered after everything is loaded when the player enters a scene or teleports to a different section
/// </summary>
/// <param name="EntryTrigger">The trigger where the player appears</param>
public record struct PlayerEntered(Trigger EntryTrigger);
