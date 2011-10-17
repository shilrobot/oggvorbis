using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OggVorbis
{
    public static class VorbisUtil
    {
        // Find the index of the highest set bit (plus one)
        // This is equivalent to the minimum number of bits needed to store 'value'
        // ilog(x) where x < 0 = 0
        // ilog(0) = 0
        // ilog(1) = 1
        // ilog(2) = 2
        // ilog(3) = 2
        // ilog(4) = 3
        // ilog(7) = 3
        public static int InverseLog(int value)
        {
            if (value <= 0)
                return 0;
            else
            {
                // start at bit 30 (adjacent to sign bit)
                for (int i = 30; i >= 0; --i)
                    if ((value & (1 << i)) != 0)
                        return (i + 1);

                return 0;
            }
        }
    }
}
