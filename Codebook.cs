using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OggVorbis
{
    public class Codebook
    {
        public int Entries;
        public int Dimensions;
        public int LookupType;
        public HuffLeafNode[] CodewordNodes;
        public HuffInternalNode RootNode;
        public uint[] Multiplicands;
        public float MinimumValue;
        public float DeltaValue;
        public bool SequenceP;
        public float[][] Lookup;

        public void BuildLookup()
        {
            if (LookupType == 0)
            {
            }
            else if (LookupType == 1)
            {
                Lookup = new float[Entries][];

                for (int i = 0; i < Entries; ++i)
                    Lookup[i] = BuildLookupEntry(i);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        // lookup type 1 only atm
        private float[] BuildLookupEntry(int entry)
        {
            float[] result = new float[Dimensions];

            float last = 0;
            int indexDivisor = 1;

            for (int dim = 0; dim < Dimensions; ++dim)
            {
                int multiplicandOffset = (entry / indexDivisor) % Multiplicands.Length;
                result[dim] = Multiplicands[multiplicandOffset] * DeltaValue + MinimumValue + last;

                if (SequenceP)
                    last = result[dim];

                indexDivisor *= Multiplicands.Length;
            }

            return result;
        }

        public void Dump()
        {
            bool[] seen = new bool[Entries];
            HuffLeafNode[] entryNodes = new HuffLeafNode[Entries];

            RootNode.Visit(delegate(HuffLeafNode node)
            {
                seen[node.Code] = true;
                entryNodes[node.Code] = node;
            });

            for (int i = 0; i < Entries; ++i)
            {
                if (seen[i])
                {
                    Console.Write("    {0}: {1}", i, entryNodes[i].Codeword());

                    if (LookupType == 1)
                    {
                        Console.Write(": ");

                        float[] vq = Lookup[i];

                        for (int dim = 0; dim < Dimensions; ++dim)
                        {
                            if (dim != 0)
                                Console.Write(", ");
                            Console.Write(vq[dim]);
                        }
                    }

                    Console.WriteLine();
                }
            }
        }

        public int ScalarLookup(BitReader r)
        {
            HuffNode cur = RootNode;

            while (cur.Internal)
            {
                if (r.ReadBit())
                    cur = cur.One;
                else
                    cur = cur.Zero;

                if (cur == null)
                    throw new VorbisReadException("Bad huffman code encountered");
            }

            return cur.Code;
        }
               
        public float[] VectorLookup(BitReader r)
        {
            return Lookup[ScalarLookup(r)];
        }        
    }
}
