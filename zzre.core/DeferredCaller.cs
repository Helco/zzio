using System;
using System.Collections.Generic;

namespace zzre.core
{
    public class DeferredCaller
    {
        public event Action Next = () => { };

        public void Call()
        {
            Next();
            Next = () => { };
        }
    }
}
