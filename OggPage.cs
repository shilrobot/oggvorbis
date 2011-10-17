using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OggVorbis
{
    public class OggPage
    {
        public bool Continuation;
        public bool BeginningOfStream;
        public bool EndOfStream;

        // TODO: is this long or ulong?
        public ulong GranulePosition;
        public uint BitstreamSerialNumber;
        public uint PageSequenceNumber;

        public byte[] SegmentTable;

        public byte[] Data;


    }
}
