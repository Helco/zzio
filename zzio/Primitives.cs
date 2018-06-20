using System;
using System.IO;
using Newtonsoft.Json;
using zzio.primitives;

namespace zzio {
    public partial class Utils {
        public static bool isUID (string str)
        {
            if (str.Length > 8)
                return false;
            for (int i=0; i<str.Length; i++)
            {
                if (!(str[i] >= '0' && str[i] <= '9') &&
                    !(str[i] >= 'A' && str[i] <= 'F') &&
                    !(str[i] >= 'a' && str[i] <= 'f'))
                    return false;
            }
            return true;
        }
    }
}