using System.Linq;
using System.Numerics;
using DefaultEcs.System;
using Silk.NET.OpenAL;

namespace zzre.game.systems;

public sealed partial class SoundListener : ISystem<float>
{
    private readonly OpenALDevice device = null!;
    private readonly Zanzarah zanzarah;

    public bool IsEnabled { get; set; }

    public SoundListener(ITagContainer diContainer)
    {
        zanzarah = diContainer.GetTag<Zanzarah>();
        if (IsEnabled = diContainer.HasTag<SoundContext>())
            device = diContainer.GetTag<OpenALDevice>();
    }

    public void Dispose()
    {
    }

    public unsafe void Update(float _)
    {
        if (!IsEnabled)
            return;

        Vector3 position = Vector3.Zero, forward = Vector3.UnitZ, up = Vector3.UnitY;
        if (zanzarah.CurrentGame != null)
        {
            var ecsWorld = zanzarah.CurrentGame.GetTag<DefaultEcs.World>();
            var listenerEntity = ecsWorld
                .GetEntities()
                .With<components.SoundListener>()
                .AsEnumerable()
                .FirstOrDefault();
            if (listenerEntity.TryGet<Location>(out var location))
            {
                position = location.LocalPosition;
                forward = location.InnerRight;
                up = location.InnerUp;
            }
        }

        device.AL.SetListenerProperty(ListenerVector3.Position, position * new Vector3(1, 1, -1));
        var orientation = stackalloc Vector3[2];
        orientation[0] = forward * new Vector3(-1, 1, 1);
        orientation[1] = up * new Vector3(-1, 1, 1);
        device.AL.SetListenerProperty(ListenerFloatArray.Orientation, (float*)orientation);
        device.AL.ThrowOnError();
    }
}
