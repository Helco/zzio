using System;
using zzre.game.components;
using zzre.rendering;
using zzio;
using zzio.db;
using zzio.scn;
using DefaultEcs.Resource;

namespace zzre.tools;

partial class ECSExplorer
{
    private static void AddStandardEntityNaming()
    {
        const int Highest = 100000;
        const int High = 1000;
        const int Def = 0;
        const int Low = -1000;

        AddEntityNamerByComponent<PlayerControls>(Highest, "Player");
        AddEntityNamerByComponent(Highest, (in NpcRow npc) => $"NPC \"{npc.Name}\" {npc.InternalName}");
        AddEntityNamerByComponent<Butterfly>(Highest, entity => $"Butterfly {entity.Get<Trigger>().idx}");
        AddEntityNamerByComponent<CirclingBird>(Highest, entity => $"CirclingBird {entity.Get<Trigger>().idx}");
        AddEntityNamerByComponent<AnimalWaypointAI>(Highest, entity => entity.Get<AnimalWaypointAI>().Config switch
        {
            var c when c == AnimalWaypointAI.Configuration.BlackPixie => "BlackPixie ",
            var c when c == AnimalWaypointAI.Configuration.Bug => "Bug ",
            var c when c == AnimalWaypointAI.Configuration.Chicken => "Chicken ",
            var c when c == AnimalWaypointAI.Configuration.Dragonfly => "Dragonfly ",
            var c when c == AnimalWaypointAI.Configuration.Firefly => "Firefly ",
            var c when c == AnimalWaypointAI.Configuration.Frog => "Frog ",
            var c when c == AnimalWaypointAI.Configuration.Rabbit => "Rabbit ",
            _ => "unknown "
        } + entity.Get<Trigger>().idx);
        AddEntityNamerByComponent<CollectionFairy>(Highest, e => $"CollectionFairy \"{e.Get<InventoryFairy>().name}\" {e}");

        AddEntityNamerByComponent<InventoryFairy>(High + 1, e => $"OwnedFairy \"{e.Get<InventoryFairy>().name}\" {e}");
        AddEntityNamerByComponent<FairyRow>(High, e => $"Fairy \"{e.Get<FairyRow>().Name}\" {e}");
        AddEntityNamerByComponent<ActorPart>(High, e => $"ActorPart {e}");

        AddEntityNamerByComponent<ManagedResource<string, ActorExDescription>>(Def + 1,
            e => $"Actor {e.Get<ManagedResource<string, ActorExDescription>>().Info} {e}");
        AddEntityNamerByComponent<ClumpMesh>(Def, e => $"Model {e.Get<ClumpMesh>().Name} {e}");

        AddEntityNamerByComponent(Low, (in Trigger t) => $"Trigger {t.type} {t.idx}");
    }

    private static void AddStandardEntityGrouping()
    {
        const string Models = "Models";
        const string Triggers = "Triggers";
        const string NPCs = "NPCs";
        const string Animals = "Animals";

        AddEntityGrouperByComponent<NpcRow>(1000, NPCs);
        AddEntityGrouperByComponent<Butterfly>(1000, Animals);
        AddEntityGrouperByComponent<CirclingBird>(1000, Animals);
        AddEntityGrouperByComponent<AnimalWaypointAI>(1000, Animals);
        AddEntityGrouperByComponent<CollectionFairy>(1000, Animals);
        AddEntityGrouperByComponent<ClumpMesh>(0, Models);
        AddEntityGrouperByComponent<Trigger>(-1, Triggers);
    }
}
