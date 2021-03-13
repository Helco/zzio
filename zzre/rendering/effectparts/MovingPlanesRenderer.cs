using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Veldrid;
using zzio.effect.parts;

namespace zzre.rendering.effectparts
{
    public class MovingPlanesRenderer : ListDisposable, IEffectCombinerPartRenderer
    {
        private readonly ITagContainer diContainer;
        private readonly MovingPlanes data;

        public Location Location { get; } = new Location();

        public MovingPlanesRenderer(ITagContainer diContainer, MovingPlanes data)
        {
            this.diContainer = diContainer;
            this.data = data;
        }

        public void Render(CommandList cl)
        {
        }
    }
}
