using zzio;

namespace zzre.game.messages
{
    public record struct DialogTalk(DefaultEcs.Entity DialogEntity, UID DialogUID);
}
