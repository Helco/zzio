using System;

namespace zzre.game.components
{
    public struct PlayerControls
    {
        public bool GoesForward;
        public bool GoesBackward;
        public bool GoesLeft;
        public bool GoesRight;
        public bool Jumps;
        public bool WhirlJumps; // not actually set by input...

        public bool GoesAnywhere => GoesForward || GoesBackward || GoesRight || GoesLeft;
    }
}
