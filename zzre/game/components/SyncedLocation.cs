using System;
using Veldrid;

namespace zzre.game.components
{
    public readonly struct SyncedLocation
    {
        public readonly DeviceBufferRange range;

        public SyncedLocation(DeviceBufferRange range) => this.range = range;
    }
}
