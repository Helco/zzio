﻿using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace zzre
{
    public readonly struct Raycast
    {
        public readonly float Distance;
        public readonly Vector3 Point;
        public readonly Vector3 Normal;

        public Raycast(float d, Vector3 p, Vector3 n) => (Distance, Point, Normal) = (d, p, n);
    }
}
