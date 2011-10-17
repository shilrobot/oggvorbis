using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OggVorbis
{
    class OggSyncState
    {
        enum OggReadPageResult
        {
            Repeat,
            NeedMoreData,
            Success,
        }

        private byte[] data;
        private int filled;

        private int headerBytes;
        private int bodyBytes;

        public OggSyncState()
        {
            data = new byte[4096];
        }

        public void Reset()
        {
            filled = 0;
            headerBytes = 0;
            bodyBytes = 0;
        }

        public void SupplyData(byte[] srcData, int offset, int count)
        {
            if(srcData == null)
                throw new ArgumentNullException("srcData");
            if(offset < 0 || offset > srcData.Length)
                throw new ArgumentOutOfRangeException("offset");
            if(count < 0 || offset+count > srcData.Length)
                throw new ArgumentOutOfRangeException("count");

            if(filled + count > data.Length)
            {
                // arithmetically increasing buffer size.
                // libogg does this so there is probably a reason,
                // although I am not sure we really need to be so stingy
                byte[] newData = new byte[filled + count + 4096];
                Array.Copy(data, newData, filled);
                data = newData;

            }

            Array.Copy(srcData, offset, data, filled, count);
            filled += count;
        }

        private void TrimFront(int count)
        {
            if (count < 0 || count > filled)
                throw new ArgumentOutOfRangeException("count");

            // TODO: verify this operates correctly
            Array.Copy(data, count, data, 0, filled - count);
            filled -= count;
        }

        private OggReadPageResult Resynchronize()
        {
            headerBytes = 0;
            bodyBytes = 0;

            int maxSearch = filled - 4;
            int i;
            for (i = 1; i <= maxSearch; ++i)
            {
                if (data[i] == 'O' &&
                    data[i + 1] == 'g' &&
                    data[i + 2] == 'g' &&
                    data[i + 3] == 'S')
                {
                    //Console.WriteLine("Resyncing: skipping {0} bytes", i);
                    TrimFront(i);
                    return OggReadPageResult.Repeat;
                }
            }

            //Console.WriteLine("Resyncing: skipping {0} bytes", i);
            TrimFront(i);
            return OggReadPageResult.NeedMoreData;
        }
        
        private OggReadPageResult ReadPageInternal(out OggPage page)
        {
            page = null;

            if (headerBytes == 0)
            {
                if (filled < 27)
                    return OggReadPageResult.NeedMoreData;

                if (data[0] != 'O' ||
                    data[1] != 'g' ||
                    data[2] != 'g' ||
                    data[3] != 'S')
                {
                    return Resynchronize();
                }

                int hbytes = data[26] + 27;
                if (filled < hbytes)
                    return OggReadPageResult.NeedMoreData;

                headerBytes = hbytes;

                for (int i = 0; i < (int)data[26]; ++i)
                {
                    bodyBytes += data[27 + i];
                }
            }

            if (headerBytes + bodyBytes <= filled)
            {
                // Check the CRC.
                // Have to temporarily zero out the CRC field first, though.

                byte crc0, crc1, crc2, crc3;

                crc0 = data[22];
                crc1 = data[23];
                crc2 = data[24];
                crc3 = data[25];

                data[22] = 0;
                data[23] = 0;
                data[24] = 0;
                data[25] = 0;

                uint actualCrc = OggCrc32.Compute(data, 0, headerBytes + bodyBytes);

                data[22] = crc0;
                data[23] = crc1;
                data[24] = crc2;
                data[25] = crc3;
                
                if (crc0 != (byte)(actualCrc & 0xFF) ||
                   crc1 != (byte)((actualCrc >> 8) & 0xFF) ||
                   crc2 != (byte)((actualCrc >> 16) & 0xFF) ||
                   crc3 != (byte)((actualCrc >> 24) & 0xFF))
                {
                    Console.WriteLine("CRC fail!");
                    return Resynchronize();
                }

                page = new OggPage();
                byte headerType = data[5];
                page.Continuation       = (headerType & 0x1) != 0;
                page.BeginningOfStream  = (headerType & 0x2) != 0;
                page.EndOfStream        = (headerType & 0x4) != 0;
                page.GranulePosition = BitConverter.ToUInt64(data, 6);
                page.BitstreamSerialNumber = BitConverter.ToUInt32(data, 14);
                page.PageSequenceNumber = BitConverter.ToUInt32(data, 18);

                // TODO: Copy page bytes

                // TODO: Return the page somehow

                /*Console.WriteLine("---------------------------------");
                Console.WriteLine("Header Bytes: {0}", headerBytes);
                Console.WriteLine("Body Bytes: {0}", bodyBytes);
                Console.WriteLine("Incomplete: {0}", data[headerBytes - 1] == 255);
                Console.WriteLine("Continuation: {0}", page.Continuation);
                Console.WriteLine("BeginningOfStream: {0}", page.BeginningOfStream);
                Console.WriteLine("EndOfStream: {0}", page.EndOfStream);
                Console.WriteLine("GranulePosition: {0}", page.GranulePosition);
                Console.WriteLine("BitstreamSerialNumber: {0}", page.BitstreamSerialNumber);
                Console.WriteLine("PageSequenceNumber: {0}", page.PageSequenceNumber);
                Console.WriteLine("CRC: {0:X8}", actualCrc);
                Console.WriteLine();
                Console.WriteLine("Segment count: {0}", data[26]);
                Console.WriteLine("Segment table:");
                Util.DumpBytes(data, 27, headerBytes - 27);
                Console.WriteLine();
                Console.WriteLine("Body:");
                Util.DumpBytes(data, headerBytes, bodyBytes);*/

                // TODO: Share buffers instead
                int numSegments = headerBytes - 27;
                page.SegmentTable = new byte[numSegments];
                Array.Copy(data, 27, page.SegmentTable, 0, numSegments);

                page.Data = new byte[bodyBytes];
                Array.Copy(data, headerBytes, page.Data, 0, bodyBytes);

                TrimFront(headerBytes + bodyBytes);
                headerBytes = 0;
                bodyBytes = 0;
                return OggReadPageResult.Success;
            }
            else
                return OggReadPageResult.NeedMoreData;
        }
        

        public bool TryReadPage(out OggPage page)
        {
            OggReadPageResult result;

            do
            {
                result = ReadPageInternal(out page);
            }
            while (result == OggReadPageResult.Repeat);

            return result == OggReadPageResult.Success;
        }
    }
}
