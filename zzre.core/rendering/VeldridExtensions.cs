using Veldrid;

namespace zzre.rendering
{
    public static class VeldridExtensions
    {
        public static SamplerDescription AsDescription(this SamplerAddressMode addressMode, SamplerFilter filter) => new SamplerDescription()
        {
            AddressModeU = addressMode,
            AddressModeV = addressMode,
            AddressModeW = addressMode,
            Filter = filter
        };
    }
}
