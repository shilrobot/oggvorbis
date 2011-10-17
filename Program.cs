using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace OggVorbis
{
    class Program
    {
        static void Main(string[] args)
        {
            byte[] buf = new byte[4096];
            OggSyncState sync = new OggSyncState();

            List<byte> segments = new List<byte>();
            List<byte> data = new List<byte>();

            Console.WriteLine("Extracting pages from OGG stream");

            var sw = new Stopwatch();

            using (var fs = File.OpenRead(@"P:\\csharp\\OggVorbis\\night.ogg"))
            {
                //while (true)
                {
                    fs.Seek(0, SeekOrigin.Begin);
                    sync.Reset();
                    sw.Start();

                    while (true)
                    {
                        OggPage page;


                        if (sync.TryReadPage(out page))
                        {
                            //Console.WriteLine("Successfully read a page!");
                            segments.AddRange(page.SegmentTable);
                            data.AddRange(page.Data);
                        }
                        else
                        {
                            int read = fs.Read(buf, 0, buf.Length);
                            //Console.WriteLine("Read {0} bytes from file", read);
                            if (read == 0)
                            {
                                //Console.WriteLine("Reached end of file");
                                break;
                            }
                            else
                                sync.SupplyData(buf, 0, read);
                        }
                    }

                    sw.Stop();
                    Console.WriteLine("{0:0.0}", sw.ElapsedTicks / (double)Stopwatch.Frequency * 1000.0);
                }
            }

            Console.WriteLine("Extracting packets from pages");

            sw.Reset();
            sw.Start();
            List<byte> currPacket = new List<byte>();
            List<byte[]> packets = new List<byte[]>();
            int pos = 0;

            foreach (byte segmentLen in segments)
            {
                for (int i = 0; i < segmentLen; ++i)
                    currPacket.Add(data[pos++]);
                if (segmentLen < 255)
                {
                    packets.Add(currPacket.ToArray());
                    currPacket.Clear();
                }
            }
            sw.Stop();
            Console.WriteLine("{0:0.0}", sw.ElapsedTicks / (double)Stopwatch.Frequency * 1000.0);

            while (true)
            {
                sw.Reset();
                Vorbis v = new Vorbis();
                int n = 0;
                foreach (byte[] packet in packets)
                {
                    /*Console.WriteLine("Packet {0} - {1} bytes", ++n, packet.Length);
                    Util.DumpBytes(packet, 0, packet.Length);
                    Console.WriteLine();
                    Console.WriteLine("==========================================");
                    Console.WriteLine();*/

                    if (n == 3)
                        sw.Restart();

                    ++n;
                    v.HandlePacket(packet);
                }
                sw.Stop();
                Console.WriteLine("Vorbis decode: {0:0.0}", sw.ElapsedTicks / (double)Stopwatch.Frequency * 1000.0);
            }
        }

        
    }
}
