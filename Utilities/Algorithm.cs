using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Force.Crc32;

namespace Ruminoid.Common.Utilities
{
    public static class Algorithm
    {
        public static uint CalcFileCrc32(string file) => Crc32Algorithm.Compute(File.ReadAllBytes(file));
    }
}
