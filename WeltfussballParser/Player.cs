using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WeltfussballParser
{
    class Player
    {
        public string Name { get; set; }

        public DateTime DateOfBirth { get; set; }

        public string Nation { get; set; }

        public string Position { get; set; }

        public string DetailsUrl { get; set; }

        public string Team { get; set; }

        public Statistics TeamStatistics { get; set; }

        public Statistics NationStatistics { get; set; }
        public Statistics QualificationStatistics { get; set; }
    }
}
