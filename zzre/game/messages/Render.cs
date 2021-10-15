using Veldrid;

namespace zzre.game.messages
{
    public readonly struct Render
    {
        public readonly CommandList CommandList;

        public Render(CommandList cl) => CommandList = cl;
    }
}
