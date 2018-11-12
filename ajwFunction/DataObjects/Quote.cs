using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ajwFunction.DataObjects
{
    public class Quote
    {
        public double USDPrice { get; set; }
        public double GBPPrice { get; set; }
        public double EstimatedLeadTime { get; set; }
        public string QuotedPartNumbers { get; set; }
        public string MissingPartNumbers { get; set; }
        public string QuoteStatus { get; set; }
        public string HTMLForReport { get; set; }
    }
}
