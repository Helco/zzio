using System;
using System.Collections.Generic;
using System.Linq;

namespace zzre.tools
{
    public partial class SceneEditor
    {
        public interface ISelectable
        {
            string Title { get; }
            Bounds Bounds { get; } // In object space
            Location Location { get; }
        }

        private List<IEnumerable<ISelectable>> selectableContainers = new List<IEnumerable<ISelectable>>();
        private IEnumerable<ISelectable> Selectables => selectableContainers.SelectMany(c => c);

        private ISelectable? _selected;
        private ISelectable? Selected
        {
            get => _selected;
            set
            {
                _selected = value;
                OnNewSelection.Invoke(value);
            }
        }

        private event Action<ISelectable?> OnNewSelection = _ => { };
    }
}
