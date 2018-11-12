using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ajwFunction.DataObjects
{
    public class PartItem
    {
        public string SupplierName { get; set; }
        public string PartNumber { get; set; }
        public string Decription { get; set; }
        public double LeadTime { get; set; }
        public string CurrencyCode { get; set; }
        public double ListUnitPrice { get; set; }
        public string UOM { get; set; }
        public string CageCode { get; set; }
    }
}
