using System;
using zzre.game.components;
using zzre.game.components.ui;
using zzre.game.resources;
using zzre.materials;
using zzre.rendering;
using zzio;
using zzio.db;
using zzio.scn;
using DefaultEcs.Resource;

using EffectCombinerResource = DefaultEcs.Resource.ManagedResource<string, zzio.effect.EffectCombiner>;
using zzre.game.components.effect;

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

        AddEntityNamerByComponent<LensFlare>(Highest, e => $"LensFlare {e}");
        AddEntityNamerByComponent<EffectCombinerResource>(Highest, (e, c) => $"EffectCombiner {c.Info} {e}");
        AddEntityNamerByComponent<zzio.effect.parts.BeamStar>(Highest, (e, c) => $"BeamStar {c.Name} {e}");
        AddEntityNamerByComponent<zzio.effect.parts.ElectricBolt>(Highest, (e, c) => $"ElectricBolt {c.Name} {e}");
        AddEntityNamerByComponent<zzio.effect.parts.Models>(Highest, (e, c) => $"Models {c.Name} {e}");
        AddEntityNamerByComponent<zzio.effect.parts.MovingPlanes>(Highest, (e, c) => $"MovingPlanes {c.Name} {e}");
        AddEntityNamerByComponent<zzio.effect.parts.ParticleBeam>(Highest, (e, c) => $"ParticleBeam {c.Name} {e}");
        AddEntityNamerByComponent<zzio.effect.parts.ParticleCollector>(Highest, (e, c) => $"ParticleCollector {c.Name} {e}");
        AddEntityNamerByComponent<zzio.effect.parts.ParticleEmitter>(Highest, (e, c) => $"ParticleEmitter {c.Type} {c.Name} {e}");
        AddEntityNamerByComponent<zzio.effect.parts.PlaneBeam>(Highest, (e, c) => $"PlaneBeam {c.Name} {e}");
        AddEntityNamerByComponent<zzio.effect.parts.RandomPlanes>(Highest, (e, c) => $"RandomPlanes {c.Name} {e}");
        AddEntityNamerByComponent<zzio.effect.parts.Sound>(Highest, (e, c) => $"Sound {c.Name} {e}");
        AddEntityNamerByComponent<zzio.effect.parts.Sparks>(Highest, (e, c) => $"Sparks {c.Name} {e}");

        AddEntityNamerByComponent<InventoryFairy>(High + 1, e => $"OwnedFairy \"{e.Get<InventoryFairy>().name}\" {e}");
        AddEntityNamerByComponent<FairyRow>(High, e => $"Fairy \"{e.Get<FairyRow>().Name}\" {e}");
        AddEntityNamerByComponent<ActorPart>(High, e => $"ActorPart {e}");

        AddEntityNamerByComponent<ManagedResource<string, ActorExDescription>>(Def + 1,
            e => $"Actor {e.Get<ManagedResource<string, ActorExDescription>>().Info} {e}");
        AddEntityNamerByComponent<ClumpMesh>(Def, e => $"Model {e.Get<ClumpMesh>().Name} {e}");
        AddEntityNamerByComponent<SoundEmitter>(Def, e => $"{(e.Has<Location>() ? "3D" : "2D")} Sample {e}");

        AddEntityNamerByComponent(Low, (in Trigger t) => $"Trigger {t.type} {t.idx}");

        AddEntityNamer(High, e => e.Has<Rect>() || !e.TryGet<ManagedResource<UITileSheetInfo, TileSheet>>(out var res) ? null
            : $"Preload {(res.Info.IsFont ? "font" : "tilesheet")} {res.Info.Name}");
        AddEntityNamer(Def, e => e.Has<Rect>() || !e.TryGet<ManagedResource<string, UIMaterial>>(out var res) ? null : $"Preload bitmap {res.Info}");
        AddEntityNamerByComponent<ButtonTiles>(High, e => $"Button #{e.TryGet<ElementId>().GetValueOrDefault(default).Value} {e}");
        AddEntityNamerByComponent<TooltipTarget>(High, e => $"Tooltip Target {e}");
        AddEntityNamerByComponent<Fade>(High, e => $"Fade {e}");
        AddEntityNamerByComponent<Slider>(High, e => $"Slider #{e.TryGet<ElementId>().GetValueOrDefault(default).Value} {e}");
        AddEntityNamerByComponent(High, (in AnimatedLabel label) => $"Anim. Label \"{Sanitize(label.FullText)}\"");
        AddEntityNamerByComponent(Def, (in Label label) => $"Label \"{Sanitize(label.Text)}\"");
        AddEntityNamerByComponent<Tile[]>(Low, e => $"Visuals {e}");

        AddEntityNamerByComponent<ScrDeck>(High, nameof(ScrDeck));
        AddEntityNamerByComponent<ScrGotCard>(High, nameof(ScrGotCard));
        AddEntityNamerByComponent<ScrRuneMenu>(High, nameof(ScrRuneMenu));
        AddEntityNamerByComponent<ScrBookMenu>(High, nameof(ScrBookMenu));
        AddEntityNamerByComponent<ScrMapMenu>(High, nameof(ScrMapMenu));
        AddEntityNamerByComponent<ScrNotification>(High, nameof(ScrNotification));

        static string Sanitize(string t) => t[0..Math.Min(16, t.Length)].Replace("\n", "\\n").Replace("\t", "\\t");
    }

    private static void AddStandardEntityGrouping()
    {
        const string Models = "Models";
        const string Triggers = "Triggers";
        const string NPCs = "NPCs";
        const string Animals = "Animals";
        const string Preload = "Preload";
        const string Effects = "Effects";
        const string Samples = "Samples";

        AddEntityGrouperByComponent<NpcRow>(1000, NPCs);
        AddEntityGrouperByComponent<Butterfly>(1000, Animals);
        AddEntityGrouperByComponent<CirclingBird>(1000, Animals);
        AddEntityGrouperByComponent<AnimalWaypointAI>(1000, Animals);
        AddEntityGrouperByComponent<CollectionFairy>(1000, Animals);
        AddEntityGrouperByComponent<EffectCombinerResource>(1000, Effects);
        AddEntityGrouperByComponent<LensFlare>(1000, Effects);
        AddEntityGrouperByComponent<ClumpMesh>(0, Models);
        AddEntityGrouperByComponent<Trigger>(-1, Triggers);
        AddEntityGrouperByComponent<SoundEmitter>(0, Samples);

        AddEntityGrouper(1000, e => e.Has<UIMaterial>() && !e.Has<Rect>() ? Preload : null);
    }
}
