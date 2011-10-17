using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OggVorbis
{
    class SubMap
    {
        public int FloorNumber;
        public Floor Floor;
        public int ResidueNumber;
        public Residue Residue;
    }

    class CouplingStep
    {
        public int MagnitudeChannel;
        public int AngleChannel;
    }

    class Mapping
    {
        public CouplingStep[] CouplingSteps;
        public SubMap[] SubMaps;
        public SubMap[] SubMapsByChannel;
    }
}
