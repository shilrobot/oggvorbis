using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OggVorbis
{
    class Residue
    {
        public int Type;
        public int Begin;
        public int End;
        public int PartitionSize;
        public int ClassificationCount;
        public Codebook ClassBook;

        public int[] Cascade;
        public Codebook[,] Books;

        public float[][] Decode(BitReader r, int numVectors, int vectorLength, bool[] doNotDecodeFlags)
        {
            if (Type == 0 || Type == 1)
            {
                return Decode01(Type, r, numVectors, vectorLength, doNotDecodeFlags);
            }
            else if (Type == 2)
            {
                float[][] result = new float[numVectors][];
                for (int i = 0; i < numVectors; ++i)
                    result[i] = new float[vectorLength];

                if (doNotDecodeFlags.All((x) => (x == true)))
                {
                    // done early
                    return result;
                }
                else
                {
                    float[][] tmp = Decode01(1, r, 1, vectorLength*numVectors, doNotDecodeFlags);
                    //float[] tmp0 = tmp[0];
                    //int m = 0;
                    for (int i = 0; i < vectorLength; ++i)
                    {
                        for (int j = 0; j < numVectors; ++j)
                        {
                            result[j][i] = tmp[0][i * numVectors + j];
                        }
                    }

                    return result;
                }
            }
            else
                throw new VorbisReadException("bad residue type from setup");

        }

        private float[][] Decode01(int type, BitReader r, int numVectors, int vectorLength, bool[] doNotDecodeFlags)
        {
            int limitResidueBegin = Math.Min(Begin, vectorLength);
            int limitResidueEnd = Math.Min(End, vectorLength);

            int classWordsPerCodeWord = ClassBook.Dimensions;
            int nToRead = limitResidueEnd - limitResidueBegin;
            int partitionsToRead = nToRead / PartitionSize;

            float[][] result = new float[numVectors][];

            for (int i = 0; i < numVectors; ++i)
                result[i] = new float[vectorLength];

            if (nToRead == 0)
                return result;

            int[,] classifications = new int[numVectors, classWordsPerCodeWord + partitionsToRead];

            for (int pass = 0; pass < 8; ++pass)
            {
                int partitionCount = 0;
                

                while (partitionCount < partitionsToRead)
                {
                    if (pass == 0)
                    {
                        for (int j = 0; j < numVectors; ++j)
                        {
                            if (!doNotDecodeFlags[j])
                            {
                                int temp = ClassBook.ScalarLookup(r);
                                for (int i = classWordsPerCodeWord - 1; i >= 0; --i)
                                {
                                    // TODO: Isn't this indexing fucking weird?
                                    classifications[j,i+partitionCount] = temp % ClassificationCount;

                                    temp = temp / ClassificationCount;
                                }
                            }
                        }
                    }

                    for (int i = 0; i < classWordsPerCodeWord && partitionCount < partitionsToRead; ++i )
                    {
                        for (int j = 0; j < numVectors; ++j)
                        {
                            int vqclass = classifications[j, partitionCount];
                            Codebook vqbook = Books[vqclass, pass];
                            if (vqbook != null)
                            {
                                int offset = limitResidueBegin + partitionCount * PartitionSize;

                                if (type == 0)
                                {
                                    //DecodeFormat0(r, vqbook, offset, result[j]);
                                    throw new NotImplementedException();
                                }
                                else if (type == 1)
                                {
                                    DecodeFormat1(r, vqbook, offset, result[j]);
                                }
                            }
                        }

                        ++partitionCount;
                    }
                }
            }

            return result;
        }

        private void DecodeFormat1(BitReader r, Codebook vqbook, int offset, float[] v)
        {
            int n = PartitionSize;
            int i=0;
            do
            {
                float[] entryTemp = vqbook.VectorLookup(r);
                for (int j = 0; j < vqbook.Dimensions; ++j)
                {
                    v[offset + i] += entryTemp[j];
                    ++i;
                }
            } while(i <n);
        }
    }
}
