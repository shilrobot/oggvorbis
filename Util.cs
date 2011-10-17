using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OggVorbis
{
    class Util
    {
        static public void DumpBytes(byte[] bytes, int offset, int count)
        {
            StringBuilder sb = new StringBuilder();

            int j = 0;
            while (j < count)
            {
                sb.Clear();
                int jOld = j;

                for (int i = 0; i < 16; ++i)
                {
                    if (i == 4 || i == 12)
                        sb.Append(" ");
                    if (i == 8)
                        sb.Append("   ");
                    if (j < count)
                        sb.AppendFormat("{0:X2}", bytes[offset + j]);
                    else
                        sb.Append("  ");
                    ++j;
                    sb.Append(" ");
                }

                sb.Append("  ");
                j = jOld;

                for (int i = 0; i < 16; ++i)
                {
                    if (j < count)
                    {
                        if (i == 8)
                            sb.Append(" ");
                        char c = (char)bytes[offset + j];
                        if (c < ' ' || c > '~')
                            c = '.';
                        sb.AppendFormat("{0}", c);
                    }
                    else
                        sb.Append(" ");
                    ++j;
                }


                Console.WriteLine(sb.ToString());
            }
            Console.WriteLine();
        }
    }
}
