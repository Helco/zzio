using System;
using System.Collections.Generic;

namespace zzre.imgui
{
    public class DeferredCallerTag
    {
        public event Action Next = () => { };

        public DeferredCallerTag(Window window)
        {
            window.AddTag(this);
            window.OnBeforeContent += () =>
            {
                Next();
                Next = () => { };
            };
        }
    }
}
