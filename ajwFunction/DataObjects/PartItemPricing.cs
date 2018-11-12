using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ajwFunction.DataObjects
{
    public class PartItemPricing
    {
        public PartItem Part { get; set; }
        public double EURPrice { get; set; }
        public double GBPPrice { get; set; }
        public double USDPrice { get; set; }
    }
}