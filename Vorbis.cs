using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace OggVorbis
{
    [Serializable]
    public class VorbisReadException : Exception
    {
        public VorbisReadException() { }
        public VorbisReadException(string message) : base(message) { }
        public VorbisReadException(string message, params object[] args) : base(String.Format(message, args)) { }
        public VorbisReadException(string message, Exception inner) : base(message, inner) { }
        protected VorbisReadException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }

    public class Vorbis
    {
        // Stuff from identifiation packet
        private int channels;
        private uint sampleRate;
        private int bitrateMax;
        private int bitrateNominal;
        private int bitrateMin;
        private int blocksize0;
        private int blocksize1;
        //private bool framingFlag;

        // Comment packet
        private string vendor;
        private List<string> userComments = new List<string>();

        // Setup packet
        private List<Codebook> codebooks = new List<Codebook>();
        private List<Floor> floors = new List<Floor>();
        private List<Residue> residues = new List<Residue>();
        private List<Mapping> mappings = new List<Mapping>();
        private List<Mode> modes = new List<Mode>();

        public void HandlePacket(byte[] packet)
        {
            if (packet.Length == 0)
                return;
            
            byte firstByte = packet[0];

            // header packet
            if ((firstByte & 0x01) != 0)
            {
                ReadHeader(packet);
            }
            // compressed wave data
            else
            {
                ReadWavePacket(packet);
            }
        }

        public void ReadHeader(byte[] data)
        {
            BitReader r = new BitReader(data, data.Length);

            byte firstByte = data[0];

            // skip 1st byte
            r.ReadUnsigned(8);

            string sig = "vorbis";
            for (int i = 0; i < 6; ++i)
            {
                if (r.ReadUnsigned(8) != (uint)sig[i])
                {
                    throw new VorbisReadException("Bad header signature, should be 'vorbis'");
                }
            }

            if (firstByte == 0x01)
            {
                WriteLine("Identification header");

                uint vorbisVersion = r.ReadUnsigned(32);

                if (vorbisVersion != 0)
                {
                    throw new VorbisReadException("Bad vorbis version: {0}", vorbisVersion);
                }

                channels = (int)r.ReadUnsigned(8);
                sampleRate = (uint)r.ReadUnsigned(32);
                bitrateMax = r.ReadSigned(32);
                bitrateNominal = r.ReadSigned(32);
                bitrateMin = r.ReadSigned(32);
                uint blocksize0exp = r.ReadUnsigned(4);
                uint blocksize1exp = r.ReadUnsigned(4);
                //framingFlag = r.ReadBit();
                if (channels == 0)
                    throw new VorbisReadException("Zero channels");
                if (sampleRate == 0)
                    throw new VorbisReadException("Zero sample rate");
                if (!r.ReadBit())
                    throw new VorbisReadException("Framing error on identification header");

                if (blocksize0exp < 6 || blocksize0exp > 13)
                {
                    throw new VorbisReadException("Bad blocksize0: 2^{0}", blocksize0exp);
                }

                if (blocksize1exp < 6 || blocksize1exp > 13)
                {
                    throw new VorbisReadException("Bad blocksize1: 2^{0}", blocksize1exp);
                }

                blocksize0 = 1 << (int)blocksize0exp;
                blocksize1 = 1 << (int)blocksize1exp;

                WriteLine("  Version: {0}", vorbisVersion);
                WriteLine("  Channels: {0}", channels);
                WriteLine("  Sample rate: {0} Hz", sampleRate);
                WriteLine("  Bitrate (maximum): {0:0.0} kbps", bitrateMax / 1000.0);
                WriteLine("  Bitrate (nominal): {0:0.0} kbps", bitrateNominal / 1000.0);
                WriteLine("  Bitrate (minimum): {0:0.0} kbps", bitrateMin / 1000.0);
                WriteLine("  Block size 0: {0} (2^{1})", blocksize0, blocksize0exp);
                WriteLine("  Block size 1: {0} (2^{1})", blocksize1, blocksize1exp);
                //Console.WriteLine("  Framing flag: {0}", framingFlag ? 1 : 0);
            }
            else if (firstByte == 0x03)
            {
                WriteLine("Comment header");

                vendor = r.ReadString();
                WriteLine("  Vendor: \"{0}\"", vendor);
                uint comments = r.ReadUnsigned(32);
                for (int i = 0; i < comments; ++i)
                {
                    string comment = r.ReadString();
                    userComments.Add(comment);
                    WriteLine("  Comment[{0}]: \"{1}\"", i, comment);
                }
                if (!r.ReadBit())
                    throw new VorbisReadException("Framing error on comment header");
            }
            else if (firstByte == 0x05)
            {
                WriteLine("Setup header");
                ReadSetupHeader(r);
            }
            else
            {
                throw new VorbisReadException("Invalid header type: 0x{0:2X}", firstByte);
            }
        }

        private void ReadSetupHeader(BitReader r)
        {
            uint numCodebooks = r.ReadUnsigned(8) + 1;
            WriteLine("  Codebooks: {0}", numCodebooks);

            for (int i = 0; i < numCodebooks; ++i)
            {
                WriteLine("  Codebook[{0}]", i);

                if (r.ReadUnsigned(24) != 0x564342)
                {
                    throw new VorbisReadException("Lost codebook sync");
                }

                Codebook cb = ReadCodebook(r);

                cb.BuildLookup();
                //cb.Dump();
                codebooks.Add(cb);
            }

            // just ignore some shit
            int timeCount = (int)r.ReadUnsigned(6) + 1;
            for (int i = 0; i < timeCount; ++i)
            {
                uint val = r.ReadUnsigned(16);
                if (val != 0)
                {
                    throw new VorbisReadException("Bad timecount value. Ought to be zero.");
                }
            }

            int floorCount = (int)r.ReadUnsigned(6) + 1;
            WriteLine("  Floors: {0}", floorCount);
            for (int i = 0; i < floorCount; ++i)
            {
                WriteLine("  Floor[{0}]", i);
                Floor floor = ReadFloor(r);
                floors.Add(floor);
            }

            int residueCount = (int)r.ReadUnsigned(6) + 1;
            WriteLine("  Residues: {0}", residueCount);

            for (int i = 0; i < residueCount; ++i)
            {
                WriteLine("  Residue[{0}]", i);
                Residue res = ReadResidue(r);
                residues.Add(res);
            }

            int mappingCount = (int)r.ReadUnsigned(6) + 1;
            WriteLine("  Mappings: {0}", mappingCount);

            for (int i = 0; i < mappingCount; ++i)
            {
                WriteLine("  Mapping[{0}]", i);
                mappings.Add(ReadMapping(r));
            }

            int modeCount = (int)r.ReadUnsigned(6) + 1;
            WriteLine("  Modes: {0}", modeCount);

            for (int i = 0; i < modeCount; ++i)
            {
                WriteLine("  Mode[{0}]", i);
                modes.Add(ReadMode(r));
            }

            bool framingFlag = r.ReadBit();
            if (!framingFlag)
            {
                throw new VorbisReadException("Framing flag was 0; should be 1.");
            }

            //WriteLine("  Remaining bits in packet: {0}", r.BitsLeft);
            //Console.WriteLine("Framing flag OK. Completed setup read.");
        }

        private Codebook ReadCodebook(BitReader r)
        {
            Codebook cb = new Codebook();

            cb.Dimensions = (int)r.ReadUnsigned(16);
            cb.Entries = (int)r.ReadUnsigned(24);
            int dimensions = cb.Dimensions;
            int entries = cb.Entries;
            
            //Console.WriteLine("    Dimensions: {0}", cb.Dimensions);
            //Console.WriteLine("    Entries: {0}", cb.Entries);

            cb.RootNode = ReadHuffman(cb.Entries, r);
            //root.Dump();

            int lookupType = (int)r.ReadUnsigned(4);
            cb.LookupType = lookupType;

            //Console.WriteLine("    Lookup type: {0}", lookupType);

            if (lookupType == 0)
            {
                // nothing to do
            }
            else if (lookupType == 1 || lookupType == 2)
            {
                float minimumValue = r.ReadVorbisFloat();
                float deltaValue = r.ReadVorbisFloat();
                uint valueBits = r.ReadUnsigned(4) + 1;
                bool sequenceP = r.ReadBit();
                int lookupValues;
                //Console.WriteLine("      Minimum value: {0}", minimumValue);
                //Console.WriteLine("      Delta value: {0}", deltaValue);
                //Console.WriteLine("      Value bits: {0}", valueBits);
                //Console.WriteLine("      Sequence P: {0}", sequenceP ? 1 : 0);

                cb.MinimumValue = minimumValue;
                cb.DeltaValue = deltaValue;
                cb.SequenceP = sequenceP;

                if (lookupType == 1)
                {
                    lookupValues = lookup1_values(entries, dimensions);

                    if (Math.Pow(lookupValues, dimensions) > entries)
                    {
                        // this is actually not a problem w/ the file, it's a problem w/ the decoder if this happens
                        throw new VorbisReadException("lookup1 is busted");
                    }
                }
                else
                {
                    lookupValues = (int)entries * (int)dimensions;
                }

                //Console.WriteLine("      Lookup values: {0}", lookupValues);

                uint[] multiplicands = new uint[lookupValues];

                for (int i = 0; i < lookupValues; ++i)
                {
                    multiplicands[i] = r.ReadUnsigned((int)valueBits);

                    //Console.WriteLine("      Multiplicands[{0}] = {1}", i, multiplicands[i]);
                }

                cb.Multiplicands = multiplicands;
            }
            else if (lookupType > 2)
            {
                throw new VorbisReadException("Invalid lookup type. Only types 0, 1 and 2 are supported");
            }

            return cb;
        }

        private HuffInternalNode ReadHuffman(int entries, BitReader r)
        {
            bool ordered = r.ReadBit();
            //Console.WriteLine("    Ordered: {0}", ordered ? 1 : 0);
            int[] codewordLengths = new int[entries];
            bool[] codewordUsed = new bool[entries];

            if (!ordered)
            {
                bool sparse = r.ReadBit();
                //Console.WriteLine("    Sparse: {0}", sparse ? 1 : 0);

                for (int i = 0; i < entries; ++i)
                {
                    if (sparse)
                    {
                        if (r.ReadBit())
                        {
                            codewordLengths[i] = (int)r.ReadUnsigned(5) + 1;
                            codewordUsed[i] = true;
                        }
                    }
                    else
                    {
                        codewordLengths[i] = (int)r.ReadUnsigned(5) + 1;
                        codewordUsed[i] = true;
                    }
                }
            }
            else
            {
                int currentEntry = 0;
                int currentLength = (int)r.ReadUnsigned(5) + 1;
                while (currentEntry < entries)
                {
                    uint number = r.ReadUnsigned(VorbisUtil.InverseLog(entries - currentEntry));
                    for (int i = 0; i < number; ++i)
                    {
                        codewordUsed[currentEntry] = true;
                        codewordLengths[currentEntry++] = currentLength;
                    }
                    ++currentLength;
                }
            }

            HuffInternalNode root = new HuffInternalNode();

            for (int i = 0; i < entries; ++i)
            {
                if (codewordUsed[i])
                {
                    HuffLeafNode leaf = root.Insert(i, (int)codewordLengths[i]);

                    if (leaf == null)
                    {
                        throw new VorbisReadException("Failed to reserve codeword of length {0} for entry #{1}. Possibly malformed codebook.", codewordLengths[i], i);
                    }

                    //Console.WriteLine("    Entry[{0}]: {1}", i, leaf.Codeword());
                }
            }

            return root;
        }

        private static int lookup1_values(int entries, int dimensions)
        {
            int cur = 0;
            while (true)
            {
                int j = 1;
                for (uint i = 0; i < dimensions; ++i)
                    j *= cur;

                if (j <= entries)
                    ++cur;
                else
                    return (int)(cur - 1);
            }
        }

        private Floor ReadFloor(BitReader r)
        {
            int type = (int)r.ReadUnsigned(16);

            WriteLine("    Type: {0}", type);

            if (type == 0)
            {
                return ReadFloor0(r);
            }
            else if (type == 1)
            {
                return ReadFloor1(r);
            }
            else
            {
                throw new VorbisReadException("Bad floor type: {0}", type);
            }
        }

        private Floor0 ReadFloor0(BitReader r)
        {
            Floor0 floor = new Floor0();
            floor.Type = 0;
            floor.Order = (int)r.ReadUnsigned(8);
            floor.Rate = (int)r.ReadUnsigned(16);
            floor.BarkMapSize = (int)r.ReadUnsigned(16);
            floor.AmplitudeBits = (int)r.ReadUnsigned(6);
            floor.AmplitudeOffset = (int)r.ReadUnsigned(8);

            WriteLine("    Order: {0}", floor.Order);
            WriteLine("    Rate: {0}", floor.Rate);
            WriteLine("    Bark map size: {0}", floor.BarkMapSize);
            WriteLine("    Amplitude bits: {0}", floor.AmplitudeBits);
            WriteLine("    Amplitude offset: {0}", floor.AmplitudeOffset);

            int numBooks = (int)r.ReadUnsigned(4) + 1;
            WriteLine("    Codebooks: {0}", numBooks);

            floor.Codebooks = new Codebook[numBooks];
            for (int i = 0; i < numBooks; ++i)
            {
                int codebookIndex = (int)r.ReadUnsigned(8);
                if (codebookIndex >= codebooks.Count)
                    throw new VorbisReadException("Invalid codebook index in floor0: {0}", codebookIndex);
                WriteLine("    Codebook[{0}] = {1}", i, codebookIndex);
                floor.Codebooks[i] = codebooks[codebookIndex];
            }

            return floor;
        }

        private Floor1 ReadFloor1(BitReader r)
        {
            Floor1 floor = new Floor1();
            floor.Type = 1;

            int partitions = (int)r.ReadUnsigned(5);
            WriteLine("    Partitions: {0}", partitions);
            int maximumClass = -1;

            int[] partitionClasses = new int[partitions];

            for (int i = 0; i < partitions; ++i)
            {
                partitionClasses[i] = (int)r.ReadUnsigned(4);
                WriteLine("    Partition[{0}]: Class={1}", i, partitionClasses[i]);

                if(partitionClasses[i] > maximumClass)
                    maximumClass = partitionClasses[i];
            }

            int numClasses = maximumClass + 1;
            WriteLine("    Total classes: {0}", numClasses);
            
            Floor1Class[] classes = new Floor1Class[numClasses];

            for (int i = 0; i < numClasses; ++i)
            {
                Floor1Class cls = new Floor1Class();

                WriteLine("    Class[{0}]", i);

                cls.Dimensions = (int)r.ReadUnsigned(3) + 1;
                WriteLine("      Dimensions: {0}", cls.Dimensions);
                cls.SubclassBits = (int)r.ReadUnsigned(2);
                WriteLine("      Subclasses: {0} (2^{1})", 1 << cls.SubclassBits, cls.SubclassBits);

                if (cls.SubclassBits != 0)
                {
                    int masterBook = (int)r.ReadUnsigned(8);
                    if (masterBook >= codebooks.Count)
                        throw new VorbisReadException("Invalid codebook index for floor1 class {1} master book: {0}", masterBook, i);
                    WriteLine("      Master book: {0}", masterBook);
                    cls.MasterCodebook = codebooks[masterBook];
                }

                int subclassCount = (1 << cls.SubclassBits);

                cls.SubclassBooks = new Codebook[subclassCount];

                for (int j = 0; j < subclassCount; ++j)
                {
                    int book = (int)r.ReadUnsigned(8);
                    book -= 1;
                    if (book >= codebooks.Count)
                        throw new VorbisReadException("Invalid codebook index for floor1 subclassBooks[{0}][{1}]: {2}", i, j, book);
                    WriteLine("      Subclass book[{0}]: {1}", j, book);
                    cls.SubclassBooks[j] = book >= 0 ? codebooks[book] : null;
                }

                classes[i] = cls;
            }

            int multiplier = (int)r.ReadUnsigned(2) + 1;
            WriteLine("    Multiplier: {0}", multiplier);
            int rangeBits = (int)r.ReadUnsigned(4);
            WriteLine("    Range bits: {0}", rangeBits);

            List<int> xlist = new List<int>();
            xlist.Add(0);
            xlist.Add(1 << rangeBits);
            
            for (int i = 0; i < partitions; ++i)
            {
                int currentClassNumber = partitionClasses[i];

                for (int j = 0; j < classes[currentClassNumber].Dimensions; ++j)
                {
                    xlist.Add((int)r.ReadUnsigned(rangeBits));
                }
            }

            for (int i = 0; i < xlist.Count; ++i)
                WriteLine("    Xlist[{0}] = {1}", i, xlist[i]);

            floor.Multiplier = multiplier;
            floor.ClassesByPartition = new Floor1Class[partitions];

            for (int i = 0; i < partitions; ++i)
            {
                floor.ClassesByPartition[i] = classes[partitionClasses[i]];
            }

            floor.XList = xlist.ToArray();

            floor.CacheLookups();

            return floor;
        }

        private Residue ReadResidue(BitReader r)
        {
            int residueType = (int)r.ReadUnsigned(16);

            int begin = (int)r.ReadUnsigned(24);
            int end = (int)r.ReadUnsigned(24);
            int partitionSize = (int)r.ReadUnsigned(24) + 1;
            int classifications = (int)r.ReadUnsigned(6) + 1;
            int classBook = (int)r.ReadUnsigned(8);

            if (begin > end)
                throw new VorbisReadException("Residue begin should not be greater than end");

            if (classBook >= codebooks.Count)
                throw new VorbisReadException("Bad codebook index for residue's classBook: {0}", classBook);

            WriteLine("    Residue type: {0}", residueType);
            WriteLine("    Begin: {0}", begin);
            WriteLine("    End: {0}", end);
            WriteLine("    Partition size: {0}", partitionSize);
            WriteLine("    Classifications: {0}", classifications);
            WriteLine("    Class book: {0}", classBook);

            int[] residueCascade = new int[classifications];
            //int[,] residueBooks = new int[classifications, 8];
            //bool[,] residueBooksUsed = new bool[classifications, 8];
            Codebook[,] residueBooks = new Codebook[classifications, 8];

            for (int i = 0; i < classifications; ++i)
            {
                int highBits = 0;
                int lowBits = (int)r.ReadUnsigned(3);
                bool bitFlag = r.ReadBit();
                if (bitFlag)
                    highBits = (int)r.ReadUnsigned(5);
                residueCascade[i] = highBits * 8 + lowBits;

                WriteLine("    Residue cascade[{0}] = {1}", i, residueCascade[i]);
            }
                        

            for (int i = 0; i < classifications; ++i)
            {
                for (int j = 0; j < 8; ++j)
                {
                    if ((residueCascade[i] & (1 << j)) != 0)
                    {
                        int booknum = (int)r.ReadUnsigned(8);
                        if (booknum >= codebooks.Count)
                            throw new VorbisReadException("Bad codebook index when reading residueBooks[{0}][{1}]: {2}", i, j, booknum);

                        WriteLine("    Residue books[{0},{1}] = {2}", i, j, booknum);
                        residueBooks[i, j] = codebooks[booknum];
                    }
                    else
                    {
                        WriteLine("    Residue books[{0},{1}] = unused", i, j);
                    }
                }
            }

            Residue residue = new Residue();
            residue.Type = residueType;
            residue.Begin = begin;
            residue.End = end;
            residue.PartitionSize = partitionSize;
            residue.ClassificationCount = classifications;
            residue.ClassBook = codebooks[classBook];
            residue.Cascade = residueCascade;
            residue.Books = residueBooks;
            return residue;
        }

        private Mapping ReadMapping(BitReader r)
        {
            Mapping mapping = new Mapping();

            int mappingType = (int)r.ReadUnsigned(16);

            if (mappingType != 0)
            {
                throw new VorbisReadException("Bad mapping type: {0}", mappingType);
            }
            //Console.WriteLine("    Mapping type: {0}", mappingType);

            int submaps = 1;

            if (r.ReadBit())
                submaps = (int)r.ReadUnsigned(4) + 1;
            
            //Console.WriteLine("    Submaps: {0}", submaps);

            /*
            int[] magnitudes;
            int[] angles;
            */

            int couplingSteps = 0;

            if (r.ReadBit())
            {
                couplingSteps = (int)r.ReadUnsigned(8) + 1;
            }

            mapping.CouplingSteps = new CouplingStep[couplingSteps];

            int channelBits = VorbisUtil.InverseLog(channels-1);
            
            //Console.WriteLine("    Coupling steps: {0}",couplingSteps);

            for (int j = 0; j < couplingSteps; ++j)
            {
                CouplingStep step = new CouplingStep();

                step.MagnitudeChannel = (int)r.ReadUnsigned(channelBits);
                step.AngleChannel = (int)r.ReadUnsigned(channelBits);

                mapping.CouplingSteps[j] = step;

                WriteLine("    Coupling step {0}: Magnitude from channel {1}, angle from channel {2}", j, step.MagnitudeChannel, step.AngleChannel);
            }

            int reserved = (int)r.ReadUnsigned(2);
            if (reserved != 0)
            {
                throw new VorbisReadException("Bad 2-bit reserved value in mapping: {0}", reserved);
            }

            int[] mux = new int[channels];

            if (submaps > 1)
            {
                for (int j = 0; j < channels; ++j)
                {
                    mux[j] = (int)r.ReadUnsigned(4);
                    if (mux[j] >= submaps)
                    {
                        throw new VorbisReadException("Bad multiplex value in mapping: {0}", mux[j]);
                    }
                }
            }
            else
            {
                // All channels use the single submap
                for (int j = 0; j < channels; ++j)
                    mux[j] = 0;
            }

            for (int j = 0; j < channels; ++j)
            {
                //Console.WriteLine("    Multiplex: Channel {0} uses submap {1}", j, mux[j]);
            }

            //mapping.ChannelToSubmapTable = mux;

            SubMap[] submapTable = new SubMap[submaps];

            for (int j = 0; j < submaps; ++j)
            {
                // discard
                r.ReadUnsigned(8);

                SubMap sm = new SubMap();

                int floorNum = (int)r.ReadUnsigned(8);
                if (floorNum >= floors.Count)
                    throw new VorbisReadException("Invalid floor index in mapping: {0}", floorNum);

                sm.FloorNumber = floorNum;
                sm.Floor = floors[floorNum];

                int residueNum = (int)r.ReadUnsigned(8);
                if (floorNum >= residues.Count)
                    throw new VorbisReadException("Invalid residue index in mapping: {0}", floorNum);

                sm.ResidueNumber = residueNum;
                sm.Residue = residues[residueNum];

                //Console.WriteLine("    Submap[{0}]", j);
                //Console.WriteLine("      Floor: {0}", floorNum);
                //Console.WriteLine("      Residue: {0}", residueNum);

                submapTable[j] = sm;
            }

            mapping.SubMaps = submapTable;
            mapping.SubMapsByChannel = new SubMap[channels];
            for (int j = 0; j < channels; ++j)
            {
                mapping.SubMapsByChannel[j] = submapTable[mux[j]];
                WriteLine("    Channel {0} uses Floor {1}, Residue {2}", j, mapping.SubMapsByChannel[j].FloorNumber, mapping.SubMapsByChannel[j].ResidueNumber);
            }

            return mapping;
        }

        private Mode ReadMode(BitReader r)
        {
            Mode mode = new Mode();

            mode.BlockFlag = r.ReadBit();
            int windowType = (int)r.ReadUnsigned(16);

            if (windowType != 0)
                throw new VorbisReadException("Bad window type in mode: {0}", windowType);

            int transformType = (int)r.ReadUnsigned(16);

            if (transformType != 0)
                throw new VorbisReadException("Bad transform type in mode: {0}", transformType);

            int mapping = (int)r.ReadUnsigned(8);

            if (mapping >= mappings.Count)
                throw new VorbisReadException("Bad mapping index in mode: {0}", mapping);

            mode.Mapping = mappings[mapping];

            WriteLine("    Block flag: {0}", mode.BlockFlag ? 1 : 0);
            WriteLine("    Window type: {0}", windowType);
            WriteLine("    Transform type: {0}", transformType);
            WriteLine("    Mapping: {0}", mapping);

            return mode;
        }


        BinaryWriter wr;
        List<float> lastLap = new List<float>();
        int samples = 0;

        private void ReadWavePacket(byte[] packet)
        {
            if (wr == null)
                wr = new BinaryWriter(File.Create("out.pcm"));

            BitReader r = new BitReader(packet, packet.Length);

            if (r.ReadBit())
                throw new VorbisReadException("First bit of packet should be zero (audio)");

            //Console.WriteLine("Audio Packet");

            int modeNumber = (int)r.ReadUnsigned(VorbisUtil.InverseLog(modes.Count - 1));
            if(modeNumber >= modes.Count)
                throw new VorbisReadException("Bad mode count");
            Mode mode = modes[modeNumber];

            //Console.WriteLine("  Mode: {0}", modeNumber);

            int blocksize = mode.BlockFlag ? blocksize1 : blocksize0;

            bool previousWindowFlag = false;
            bool nextWindowFlag = false;

            if (mode.BlockFlag)
            {
                previousWindowFlag = r.ReadBit();
                nextWindowFlag = r.ReadBit();
            }

            List<float[]> floors = new List<float[]>();
            bool[] noResidue = new bool[channels];
            bool[] doNotDecodeFlags = new bool[channels];

            Mapping map = mode.Mapping;

            for (int i = 0; i < channels; ++i)
            {
                //Console.WriteLine("  Channel[{0}]", i);
                Floor floor = map.SubMapsByChannel[i].Floor;

                float[] data = new float[blocksize / 2];
                if (!floor.Decode(r, blocksize / 2, data))
                {
                    noResidue[i] = true;
                }
                
                floors.Add(data);
            }

            // verify some stuff
            foreach (CouplingStep step in map.CouplingSteps)
            {
                if(noResidue[step.MagnitudeChannel] != noResidue[step.AngleChannel])
                    throw new VorbisReadException("Magnitude and angle channel must both have no_residue[i] set to true or both set to false");
            }

            float[][] channelResidues = new float[channels][];

            // TODO: Necessary?
            for (int i = 0; i < channels; ++i)
                channelResidues[i] = new float[blocksize / 2];

            for (int i = 0; i < map.SubMaps.Length; ++i)
            {
                int ch = 0;

                for (int j = 0; j < channels; ++j)
                {
                    if (map.SubMapsByChannel[j] == map.SubMaps[i])
                    {
                        doNotDecodeFlags[ch] = noResidue[j];
                        ++ch;
                    }
                }

                Residue residue = map.SubMaps[i].Residue;

                // TODO:
                // "decode [ch] vectors using residue [residue_number], according to type [residue_type],
                // also passing vector [do_not_decode_flag] to indicate which vectors in the bundle should not be decoded.
                // Correct per-vector decode length is [n]/2."
                float[][] residueResult = residue.Decode(r, ch,  blocksize/2, doNotDecodeFlags);

                ch = 0;
                for (int j = 0; j < channels; ++j)
                {
                    if (map.SubMapsByChannel[j] == map.SubMaps[i])
                    {
                        // TODO: "residue vector for channel [j] is set to decoded residue vector [ch]"
                        channelResidues[j] = residueResult[ch];
                        ++ch;
                    }
                }
            }


            // Inverse channel coupling
            for (int i = map.CouplingSteps.Length - 1; i >= 0; --i)
            {
                float[] magnitude = channelResidues[map.CouplingSteps[i].MagnitudeChannel];
                float[] angle = channelResidues[map.CouplingSteps[i].AngleChannel];

                for (int j = 0; j < magnitude.Length; ++j)
                {
                    float M = magnitude[j];
                    float A = angle[j];

                    float new_M;
                    float new_A;

                    if (M > 0)
                    {
                        if (A > 0)
                        {
                            new_M = M;
                            new_A = M - A;
                        }
                        else
                        {
                            new_A = M;
                            new_M = M + A;
                        }
                    }
                    else
                    {
                        if (A > 0)
                        {
                            new_M = M;
                            new_A = M+A;
                        }
                        else
                        {
                            new_A = M;
                            new_M = M - A;
                        }
                    }

                    magnitude[j] = new_M;
                    angle[j] = new_A;
                }
            }

            /*for (int i = 0; i < channels; ++i)
            {
                Console.Write("RESIDUE INVERSE: ");
                for (int k = 0; k < blocksize / 2; ++k)
                    Console.Write("{0}{1}", (k != 0) ? "," : "", floors[i][k] * channelResidues[i][k]);
                Console.WriteLine();
            }*/



            for (int i = 0; i < 1 /*channels*/; ++i)
            {
                float[] floor = floors[i];
                float[] residue = channelResidues[i];
                float[] mdctInput = new float[blocksize / 2];
                for (int j = 0; j < floor.Length; ++j)
                    mdctInput[j] = floor[j] * residue[j];

                float[] mdctOutput = new float[blocksize];

                MDCT(mdctInput, mdctOutput, blocksize / 2);

                int n = blocksize;
                int windowCenter = n / 2;

                int leftStart, leftEnd, leftN;
                int rightStart, rightEnd, rightN;

                if (mode.BlockFlag && !previousWindowFlag)
                {
                    leftStart = n / 4 - blocksize0 / 4;
                    leftEnd = n / 4 + blocksize0 / 4;
                    leftN = blocksize0 / 2;
                }
                else
                {
                    leftStart = 0;
                    leftEnd = windowCenter;
                    leftN = n / 2;
                }

                if (mode.BlockFlag && !nextWindowFlag)
                {
                    rightStart = n * 3/ 4 - blocksize0 / 4;
                    rightEnd = n * 3 / 4 + blocksize0 / 4;
                    rightN = blocksize0 / 2;
                }
                else
                {
                    rightStart = windowCenter;
                    rightEnd = n;
                    rightN = n / 2;
                }

                for (int j = 0; j < leftStart; ++j)
                    mdctOutput[j] = 0;

                for (int j = leftStart; j < leftEnd; ++j)
                    mdctOutput[j] *= VorbisWindow(j - leftStart, leftN*2);

                for (int j = rightStart; j < rightEnd; ++j)
                    mdctOutput[j] *= VorbisWindow(rightN + (j - rightStart), rightN*2);

                for (int j = rightEnd; j < n; ++j)
                    mdctOutput[j] = 0;

                if (i == 0)
                {
                    if (lastLap.Count > 0)
                    {
                        for (int j = leftStart; j < leftEnd; ++j)
                            mdctOutput[j] += lastLap[j - leftStart];
                    }

                    lastLap.Clear();

                    for (int j = leftStart; j < rightStart; ++j)
                    {
                        wr.Write(mdctOutput[j]);
                        ++samples;
                        if (samples % 4410 == 0)
                        {
                            Console.WriteLine("{0:0.0} seconds", samples / 44100.0);
                        }
                    }

                    for (int j = rightStart; j < rightEnd;  ++j)
                    {
                        lastLap.Add(mdctOutput[j]);
                    }
                }
            }
        }

        static Dictionary<int, float[,]> mdctLookup = new Dictionary<int, float[,]>();

        void MDCT(float[] x, float[] y, int N)
        {
            float[,] lookup;

            if (!mdctLookup.TryGetValue(N, out lookup))
            {
                float pi_over_N = (float)Math.PI / N;
                float half_plus_half_N = 0.5f + N * 0.5f;

                lookup = new float[2 * N, N];
                for (int n = 0; n < 2 * N; ++n)
                {
                    for (int k = 0; k < N; ++k)
                    {
                        lookup[n, k] = (float)Math.Cos(pi_over_N * (n + half_plus_half_N) * (k + 0.5));
                    }
                }

                mdctLookup[N] = lookup;
            }
            
            for (int n = 0; n < 2 * N; ++n)
            {
                y[n] = 0;
                for (int k = 0; k < N; ++k)
                {
                    y[n] += x[k] * lookup[n, k];
                }
            }
        }

        float VorbisWindow(int j, int K)
        {
            double z = Math.Sin(Math.PI / K * (j + 0.5));
            return (float)Math.Sin(Math.PI * 0.5 * z * z);
        }

        [Conditional("FUCK")]
        void WriteLine(object x)
        {
            Console.WriteLine(x);
        }

        [Conditional("FUCK")]
        void WriteLine(string fmt, params object[] args)
        {
            Console.WriteLine(fmt, args);
        }

        [Conditional("FUCK")]
        void Write(object x)
        {
            Console.Write(x);
        }

        [Conditional("FUCK")]
        void Write(string fmt, params object[] args)
        {
            Console.Write(fmt, args);
        }
    }
}
