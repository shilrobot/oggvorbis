using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace OggVorbis
{
    // TODO: Faster, but later.
    public class BitReader
    {
        private byte[] bytes;
        private int length;
        //private int bitPos;
        //private bool reachedEnd;

        private int nextBytePos;
        private int buffer;
        private int bits = 0;
        
        public BitReader(byte[] b, int length)
        {
            if (length > b.Length)
                throw new ArgumentOutOfRangeException("length");

            bytes = b;
            this.length = length;
        }

        public void Reset()
        {
            nextBytePos = 0;
        }

        /*public int Position
        {
            get { return bitPos; }
            set
            {
                bitPos = value;
                reachedEnd = false;
            }
        }*/

        /*public int BitsLeft
        {
            get { return length * 8 - bitPos; }
        }*/

        private bool ReachedEnd
        {
            get { return bits == 0 && nextBytePos >= length; }
        }

        public bool ReadBit()
        {
            if (bits == 0)
            {
                if (nextBytePos >= length)
                    throw new EndOfStreamException();
                else
                {
                    bits = 8;
                    buffer = bytes[nextBytePos];
                    ++nextBytePos;
                }
            }

            bool result = (buffer & 0x1) != 0;
            buffer >>= 1;
            --bits;
            return result;
        }

        public int ReadSigned(int bits)
        {
            if (bits < 0 || bits > 32)
                throw new ArgumentOutOfRangeException("bits");

            if (bits == 0)
            {
                if (ReachedEnd)
                    throw new EndOfStreamException();
                else
                    return 0;
            }
            else
            {
                int tmp = 0;
                for (int i = 0; i < (bits - 1); ++i)
                    if (ReadBit())
                        tmp |= (1 << i);
                bool signBit = ReadBit();
                if (signBit)
                {
                    for (int i = bits; i < 32; ++i)
                        tmp |= (1 << i);
                }
                //Console.WriteLine("Read {0}-bit sint: {1}", bits, tmp);
                return tmp;
            }
        }

        public uint ReadUnsigned(int bits)
        {
            if (bits < 0 || bits > 32)
                throw new ArgumentOutOfRangeException("bits");

            if (bits == 0)
            {
                if (ReachedEnd)
                    throw new EndOfStreamException();
                else
                    return 0;
            }
            else
            {
                uint tmp = 0;
                for (int i = 0; i < bits; ++i)
                    if (ReadBit())
                        tmp |= (1U << i);
                //Console.WriteLine("Read {0}-bit uint: {1}", bits, tmp);
                return tmp;
            }
        }

        public string ReadString()
        {
            uint vendorLength = ReadUnsigned(32);
            byte[] vendorData = new byte[vendorLength];
            for (int i = 0; i < vendorLength; ++i)
                vendorData[i] = (byte)ReadUnsigned(8);
            return Encoding.UTF8.GetString(vendorData);
        }

        // This is NOT the same as IEEE 754 for some reason, it looks like!
        // (e.g. look at the bias on the exponent, mantissa works differently -- not 1.fraction, etc.)
        // kind of WTFing about this, I have no idea why vorbis works this way
        public float ReadVorbisFloat()
        {
            uint x = ReadUnsigned(32);
            int mantissa = (int)(x & 0x1FFFFFU);
            uint exponent = (x & 0x7fe00000U) >> 21;
            if ((x & 0x80000000U) != 0)
                mantissa = -mantissa;
            return (float)(mantissa * Math.Pow(2, (int)exponent - 788));
        }
    }
}
