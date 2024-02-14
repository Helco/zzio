using System;
using System.Linq;
using System.Numerics;
using DefaultEcs.System;
using Veldrid;
using zzio.scn;
using zzre.materials;
using zzre.rendering;

namespace zzre.game.systems;

public partial class FogModifier : ISystem<float>, ISystem<CommandList>
{
    private readonly Camera camera;
    private readonly UniformBuffer<FogParams> fogParams;
    private readonly IDisposable sceneLoadedSubscription;

    private readonly struct Modifier
    {
        public readonly Vector3 Position;
        public readonly float
            MaxDistance,
            FogDensity,
            FogDistance,
            FarPlane;

        public Modifier(Trigger t)
        {
            Position = t.pos;
            FogDensity = 0.01f * t.ii1;
            FogDistance = 0.01f * t.ii2;
            FarPlane = 0.01f * t.ii3;
            MaxDistance = t.ii4;
        }
    }
    private Modifier[] modifiers = [];
    private Misc misc = new();

    public bool IsEnabled { get; set; } = true;

    public FogModifier(ITagContainer diContainer)
    {
        camera = diContainer.GetTag<Camera>();
        fogParams = new UniformBuffer<FogParams>(diContainer.GetTag<ResourceFactory>(), dynamic: true);
        fogParams.Ref = FogParams.None;
        diContainer.AddTag(fogParams);
        var ecsWorld = diContainer.GetTag<DefaultEcs.World>();
        sceneLoadedSubscription = ecsWorld.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
    }

    public void Dispose()
    {
        fogParams.Dispose();
        sceneLoadedSubscription.Dispose();
    }

    private void HandleSceneLoaded(in messages.SceneLoaded msg)
    {
        misc = msg.Scene.misc;
        modifiers = msg.Scene.triggers
            .Where(t => t.type == TriggerType.FogModifier)
            .Select(t => new Modifier(t))
            .ToArray();

        UpdateParams(misc.fogDistance, misc.fogDensity, misc.farClip);
    }

    public void Update(float state)
    {
        if (!IsEnabled || !modifiers.Any())
            return;
        float fogDensityF = 1f, fogDistanceF = 1f, farPlaneF = 1f;
        foreach (var mod in modifiers)
        {
            var distance = camera.Location.Distance(mod.Position);
            if (distance >= mod.MaxDistance)
                continue;
            var strength = distance / mod.MaxDistance;
            fogDensityF *= strength + (1f - strength) * mod.FogDensity;
            fogDistanceF *= strength + (1f - strength) * mod.FogDistance;
            farPlaneF *= strength + (1f - strength) * mod.FarPlane;
        }

        var newFarPlane = farPlaneF * misc.farClip;
        UpdateParams(
            misc.fogDistance + (1f - fogDistanceF) * (newFarPlane - misc.fogDistance),
            misc.fogDensity * fogDensityF,
            newFarPlane);
    }

    private void UpdateParams(float fogDistance, float fogDensity, float farPlane)
    {
        camera.FarPlane = farPlane;
        fogParams.Ref = misc.fogType switch
        {
            FogType.None => FogParams.None,
            FogType.Linear => FogParams.Linear(misc.fogColor, fogDistance, farPlane),
            FogType.Exponential => FogParams.Exponential(misc.fogColor, fogDensity),
            FogType.Exponential2 => FogParams.Exponential2(misc.fogColor, fogDensity),
            _ => throw new NotSupportedException($"Unsupported fog type: {misc.fogType}")
        };
    }

    public void Update(CommandList cl) => fogParams.Update(cl);
}
