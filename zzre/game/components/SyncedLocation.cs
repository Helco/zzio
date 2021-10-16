using System;
using Veldrid;

namespace zzre.game.components
{
    public readonly struct SyncedLocation
    {
        public readonly DeviceBufferRange BufferRange;

        public SyncedLocation(DeviceBufferRange range) => this.BufferRange = range;
    }
}
