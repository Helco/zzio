namespace zzre.game.messages;
using zzio.scn;

public readonly struct SceneLoaded
{
    public readonly Scene Scene;

    public SceneLoaded(Scene scene) => this.Scene = scene;
}
