using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OggVorbis
{
    abstract class Floor
    {
        public int Type;

        public abstract bool Decode(BitReader r, int samples, float[] data);
    }

    class Floor0 : Floor
    {
        public int Order;
        public int Rate;
        public int BarkMapSize;
        public int AmplitudeBits;
        public int AmplitudeOffset;
        public Codebook[] Codebooks;

        public override bool Decode(BitReader r, int samples, float[] data)
        {
            throw new NotImplementedException();
        }
    }

    class Floor1 : Floor
    {
        public int Multiplier;
        public int[] XList;
        public int[] LowNeighborLookup;
        public int[] HighNeighborLookup;
        public int[] SortTable;
        public Floor1Class[] ClassesByPartition;

        // Runtime storage
        private int[] ylistStorage;
        private bool[] step2FlagStorage;

        public void CacheLookups()
        {
            LowNeighborLookup = new int[XList.Length];
            HighNeighborLookup = new int[XList.Length];

            for (int i = 2; i < XList.Length; ++i)
            {
                LowNeighborLookup[i] = lowNeighbor(XList, i);
                HighNeighborLookup[i] = highNeighbor(XList, i);
            }

            SortTable = new int[XList.Length];
            for (int i = 0; i < SortTable.Length; ++i)
                SortTable[i] = i;
            Array.Sort(SortTable, (x, y) => (XList[x] - XList[y]));

            ylistStorage = new int[XList.Length];
            step2FlagStorage = new bool[XList.Length];
        }

        public override bool Decode(BitReader r, int samples, float[] data)
        {
            if (!r.ReadBit())
            {
                //Console.WriteLine("    (zero)");
                //data = null;
                return false;
            }

            int[] ylist = ylistStorage;

            int range;

            if (Multiplier == 1)
                range = 256;
            else if (Multiplier == 2)
                range = 128;
            else if (Multiplier == 3)
                range = 84;
            else if (Multiplier == 4)
                range = 64;
            else
                // this is really a consistency check
                throw new VorbisReadException("Bad multiplier");

            ylist[0] = (int)r.ReadUnsigned(VorbisUtil.InverseLog(range - 1));
            ylist[1] = (int)r.ReadUnsigned(VorbisUtil.InverseLog(range - 1));
            int offset = 2;

            foreach (Floor1Class cls in ClassesByPartition)
            {
                int cdim = cls.Dimensions;
                int cbits = cls.SubclassBits;
                int csub = (1 << cbits) - 1;
                int cval = 0;

                if (cbits > 0)
                {
                    cval = cls.MasterCodebook.ScalarLookup(r);
                }

                for (int j = 0; j < cdim; ++j)
                {
                    Codebook book = cls.SubclassBooks[cval & csub];
                    cval = cval >> cbits;
                    if (book != null)
                    {
                        ylist[offset + j] = book.ScalarLookup(r);
                    }
                    else
                    {
                        ylist[offset + j] = 0;
                    }
                }

                offset += cdim;
            }

            /*Console.Write("    X vals: ");
            for (int i = 0; i < XList.Length; ++i)
                Console.Write("{0},", XList[i]);
            Console.WriteLine();
            Console.Write("    Y vals: ");
            for (int i = 0; i < XList.Length; ++i)
                Console.Write("{0},", ylist[i]);
            Console.WriteLine();*/

            /*Console.Write("    >>> ");
            for(int i=0; i<XList.Length; ++i)
                Console.Write("{0},{1},", XList[i], ylist[i]);
            Console.WriteLine();*/

            // "amplitude value synthesis" --
            // "Unwrap the always-positive-or-zero values read from the packet into +/- difference values, then apply to line prediction."


            bool[] step2Flag = step2FlagStorage;// new bool[XList.Length];
            int[] finalY = ylist;//new int[XList.Length];
            step2Flag[0] = true;
            step2Flag[1] = true;
            //finalY[0] = ylist[0];
            //finalY[1] = ylist[1];

            for (int i = 2; i<ylist.Length; ++i)
            {
                int lowNeighborOffset = LowNeighborLookup[i];// lowNeighbor(XList, i);
                int highNeighborOffset = HighNeighborLookup[i];// highNeighbor(XList, i);
                //Console.WriteLine("lo={0},hi={1}", lowNeighborOffset, highNeighborOffset);
                int predicted = renderPoint(XList[lowNeighborOffset], finalY[lowNeighborOffset],
                                            XList[highNeighborOffset], finalY[highNeighborOffset],
                                            XList[i]);

                int val = ylist[i];
                int highroom = range - predicted;
                int lowroom = predicted;

                int room = (highroom < lowroom) ? (highroom * 2) : (lowroom * 2);

                /*Console.WriteLine("lo={0}, hi={1}, loroom={2}, hiroom={3}, room={4}, val={5}, predicted={6}",
                    lowNeighborOffset, highNeighborOffset,
                    lowroom, highroom, room, val, predicted);

                Console.WriteLine("finalY[{0}]={1}, finalY[{2}]={3}",
                   lowNeighborOffset, finalY[lowNeighborOffset], highNeighborOffset, finalY[highNeighborOffset]);
                */

                if (val != 0)
                {
                    step2Flag[lowNeighborOffset] = true;
                    step2Flag[highNeighborOffset] = true;
                    step2Flag[i] = true;

                    if (val >= room)
                    {
                        if (highroom > lowroom)
                        {
                            finalY[i] = val - lowroom + predicted;
                        }
                        else // highroom <= lowroom
                        {
                            finalY[i] = predicted - val + highroom - 1;
                        }
                    }
                    else // val < room
                    {
                        // val is odd
                        if ((val % 2) != 0)
                        {
                            finalY[i] = predicted - ((val + 1) / 2);
                        }
                        // val is even
                        else
                        {
                            finalY[i] = predicted + (val / 2);
                        }
                    }
                }
                else
                {
                    step2Flag[i] = false;
                    finalY[i] = predicted;
                }

                //Console.WriteLine("Wrote finalY[{0}] = {1}", i, finalY[i]);
            }
            
            /*
            Console.Write("    reconstituted y vals: ");
            for (int i = 0; i < XList.Length; ++i)
                Console.Write("{0},", finalY[i]);
            Console.WriteLine();

            Console.Write("    Flags: ");
            for (int i = 0; i < XList.Length; ++i)
                Console.Write("{0},", step2Flag[i]?1:0);
            Console.WriteLine();
            */

            /*Console.Write("    >>> ");
            for(int i=0; i<XList.Length; ++i)
                if(step2Flag[i])
                    Console.Write("{0},{1},", XList[i], finalY[i]);
            Console.WriteLine();*/

            /*int[] indices = new int[XList.Length];
            for (int i = 0; i < indices.Length; ++i)
                indices[i] = i;*/
            
            /*int[] xlistSorted = new int[XList.Length];
            int[] finalYSorted = new int[XList.Length];
            bool[] step2FlagSorted = new bool[XList.Length];
            
            int n = 0;
            foreach(int i in SortTable)//indices.OrderBy((i) => XList[i]))
            {
                xlistSorted[n] = XList[i];
                finalYSorted[n] = finalY[i];
                step2FlagSorted[n] = step2Flag[i];
                ++n;
            }*/

           /* Console.Write("    $$$ ");
            for(int i=0; i<XList.Length; ++i)
                if(step2FlagSorted[i])
                    Console.Write("{0},{1},", xlistSorted[i], finalYSorted[i]);
            Console.WriteLine();*/

            //int maxX = XList.Max();
                        
            int hx = 0;
            int hy = 0;
            int lx = 0;
            int ly = finalY[SortTable[0]] * Multiplier;
            for (int i = 1; i < XList.Length; ++i)
            {
                int j = SortTable[i];

                if (step2Flag[j])
                {
                    hy = finalY[j] * Multiplier;
                    hx = XList[j];
                    renderLine(samples, lx, ly, hx, hy, data);
                    lx = hx;
                    ly = hy;
                }
            }

            // TODO: Not 100% certain what we ought to do here.
            // Check spec/libvorbis.
            if (hx < samples)
            {
                renderLine(samples, hx, hy, samples, hy, data);
            }

            /*Console.Write("    ### ");
            for (int i = 0; i < floor.Length; ++i)
                Console.Write("{0},", floor[i]);
            Console.WriteLine();*/

            /*float[] clippedFloor = new float[samples];
            for (int i = 0; i <= maxX && i < samples; ++i)
                clippedFloor[i] = inverseDecibelTable[floor[i]];*/

            /*Console.Write("FLOOR INVERSE: ");
            for (int i = 0; i < samples; ++i)
                Console.Write("{0}{1}", (i != 0) ? "," : "", data[i]);
            Console.WriteLine();*/

            return true;
        }
        
        private int lowNeighbor(int[] v, int x)
        {
            int bestOffset = -1;

            for (int i = 0; i < x; ++i)
            {
                if (v[i] < v[x])
                {
                    if (bestOffset == -1 || v[i] > v[bestOffset])
                        bestOffset = i;
                }
            }

            if (bestOffset < 0)
                throw new VorbisReadException("lowNeighbor failed");

            return bestOffset;
        }

        private int highNeighbor(int[] v, int x)
        {
            int bestOffset = -1;

            for (int i = 0; i < x; ++i)
            {
                if (v[i] > v[x])
                {
                    if (bestOffset == -1 || v[i] < v[bestOffset])
                        bestOffset = i;
                }
            }

            if (bestOffset < 0)
                throw new VorbisReadException("highNeighbor failed");

            return bestOffset;
        }

        private int renderPoint(int x0, int y0, int x1, int y1, int x)
        {
           // Console.WriteLine("RenderPoint({0},{1},{2},{3},{4})", x0, y0, x1, y1, x);
            int dy = y1 - y0;
            int adx = x1 - x0;
            int ady = (dy > 0) ? dy : -dy;
            int err = ady * (x - x0);
            int off = err / adx;
            if (dy < 0)
                return y0 - off;
            else
                return y0 + off;
        }

        private void renderLine(int n, int x0, int y0, int x1, int y1, float[] v)
        {
            int dy = y1 - y0;
            int adx = x1 - x0;
            int ady = dy >= 0 ? dy : -dy;
            int base_ = dy / adx;
            int x = x0;
            int y = y0;
            int err = 0;

            int sy = (dy < 0) ? base_ - 1 : base_ + 1;
            int absBase = base_ >= 0 ? base_ : -base_;
            ady = ady - (absBase * adx);

            if(x < n)
                v[x] = inverseDecibelTable[y];

            int maxX = x1 < n ? x1 : n;

            for (x = x0 + 1; x < maxX; ++x)
            {
                err = err + ady;
                if (err >= adx)
                {
                    err = err - adx;
                    y += sy;
                }
                else
                {
                    y += base_;
                }

                v[x] = inverseDecibelTable[y];
            }
        }

        private static float[] inverseDecibelTable = new float[] {
    1.0649863e-07f, 1.1341951e-07f, 1.2079015e-07f, 1.2863978e-07f, 
    1.3699951e-07f, 1.4590251e-07f, 1.5538408e-07f, 1.6548181e-07f, 
    1.7623575e-07f, 1.8768855e-07f, 1.9988561e-07f, 2.1287530e-07f, 
    2.2670913e-07f, 2.4144197e-07f, 2.5713223e-07f, 2.7384213e-07f, 
    2.9163793e-07f, 3.1059021e-07f, 3.3077411e-07f, 3.5226968e-07f, 
    3.7516214e-07f, 3.9954229e-07f, 4.2550680e-07f, 4.5315863e-07f, 
    4.8260743e-07f, 5.1396998e-07f, 5.4737065e-07f, 5.8294187e-07f, 
    6.2082472e-07f, 6.6116941e-07f, 7.0413592e-07f, 7.4989464e-07f, 
    7.9862701e-07f, 8.5052630e-07f, 9.0579828e-07f, 9.6466216e-07f, 
    1.0273513e-06f, 1.0941144e-06f, 1.1652161e-06f, 1.2409384e-06f, 
    1.3215816e-06f, 1.4074654e-06f, 1.4989305e-06f, 1.5963394e-06f, 
    1.7000785e-06f, 1.8105592e-06f, 1.9282195e-06f, 2.0535261e-06f, 
    2.1869758e-06f, 2.3290978e-06f, 2.4804557e-06f, 2.6416497e-06f, 
    2.8133190e-06f, 2.9961443e-06f, 3.1908506e-06f, 3.3982101e-06f, 
    3.6190449e-06f, 3.8542308e-06f, 4.1047004e-06f, 4.3714470e-06f, 
    4.6555282e-06f, 4.9580707e-06f, 5.2802740e-06f, 5.6234160e-06f, 
    5.9888572e-06f, 6.3780469e-06f, 6.7925283e-06f, 7.2339451e-06f, 
    7.7040476e-06f, 8.2047000e-06f, 8.7378876e-06f, 9.3057248e-06f, 
    9.9104632e-06f, 1.0554501e-05f, 1.1240392e-05f, 1.1970856e-05f, 
    1.2748789e-05f, 1.3577278e-05f, 1.4459606e-05f, 1.5399272e-05f, 
    1.6400004e-05f, 1.7465768e-05f, 1.8600792e-05f, 1.9809576e-05f, 
    2.1096914e-05f, 2.2467911e-05f, 2.3928002e-05f, 2.5482978e-05f, 
    2.7139006e-05f, 2.8902651e-05f, 3.0780908e-05f, 3.2781225e-05f, 
    3.4911534e-05f, 3.7180282e-05f, 3.9596466e-05f, 4.2169667e-05f, 
    4.4910090e-05f, 4.7828601e-05f, 5.0936773e-05f, 5.4246931e-05f, 
    5.7772202e-05f, 6.1526565e-05f, 6.5524908e-05f, 6.9783085e-05f, 
    7.4317983e-05f, 7.9147585e-05f, 8.4291040e-05f, 8.9768747e-05f, 
    9.5602426e-05f, 0.00010181521f, 0.00010843174f, 0.00011547824f, 
    0.00012298267f, 0.00013097477f, 0.00013948625f, 0.00014855085f, 
    0.00015820453f, 0.00016848555f, 0.00017943469f, 0.00019109536f, 
    0.00020351382f, 0.00021673929f, 0.00023082423f, 0.00024582449f, 
    0.00026179955f, 0.00027881276f, 0.00029693158f, 0.00031622787f, 
    0.00033677814f, 0.00035866388f, 0.00038197188f, 0.00040679456f, 
    0.00043323036f, 0.00046138411f, 0.00049136745f, 0.00052329927f, 
    0.00055730621f, 0.00059352311f, 0.00063209358f, 0.00067317058f, 
    0.00071691700f, 0.00076350630f, 0.00081312324f, 0.00086596457f, 
    0.00092223983f, 0.00098217216f, 0.0010459992f,  0.0011139742f, 
    0.0011863665f,  0.0012634633f,  0.0013455702f,  0.0014330129f, 
    0.0015261382f,  0.0016253153f,  0.0017309374f,  0.0018434235f, 
    0.0019632195f,  0.0020908006f,  0.0022266726f,  0.0023713743f, 
    0.0025254795f,  0.0026895994f,  0.0028643847f,  0.0030505286f, 
    0.0032487691f,  0.0034598925f,  0.0036847358f,  0.0039241906f, 
    0.0041792066f,  0.0044507950f,  0.0047400328f,  0.0050480668f, 
    0.0053761186f,  0.0057254891f,  0.0060975636f,  0.0064938176f, 
    0.0069158225f,  0.0073652516f,  0.0078438871f,  0.0083536271f, 
    0.0088964928f,  0.009474637f,   0.010090352f,   0.010746080f, 
    0.011444421f,   0.012188144f,   0.012980198f,   0.013823725f, 
    0.014722068f,   0.015678791f,   0.016697687f,   0.017782797f, 
    0.018938423f,   0.020169149f,   0.021479854f,   0.022875735f, 
    0.024362330f,   0.025945531f,   0.027631618f,   0.029427276f, 
    0.031339626f,   0.033376252f,   0.035545228f,   0.037855157f, 
    0.040315199f,   0.042935108f,   0.045725273f,   0.048696758f, 
    0.051861348f,   0.055231591f,   0.058820850f,   0.062643361f, 
    0.066714279f,   0.071049749f,   0.075666962f,   0.080584227f, 
    0.085821044f,   0.091398179f,   0.097337747f,   0.10366330f, 
    0.11039993f,    0.11757434f,    0.12521498f,    0.13335215f, 
    0.14201813f,    0.15124727f,    0.16107617f,    0.17154380f, 
    0.18269168f,    0.19456402f,    0.20720788f,    0.22067342f, 
    0.23501402f,    0.25028656f,    0.26655159f,    0.28387361f, 
    0.30232132f,    0.32196786f,    0.34289114f,    0.36517414f, 
    0.38890521f,    0.41417847f,    0.44109412f,    0.46975890f, 
    0.50028648f,    0.53279791f,    0.56742212f,    0.60429640f, 
    0.64356699f,    0.68538959f,    0.72993007f,    0.77736504f, 
    0.82788260f,    0.88168307f,    0.9389798f,     1f
        };
    }
    
    class Floor1Class
    {
        public int Dimensions; // number of points covered by this class
        public Codebook MasterCodebook;
        public int SubclassBits;
        public Codebook[] SubclassBooks; // length = 2 ^ (# subclasses)
    }
}
