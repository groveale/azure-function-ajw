using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ajwFunction.DataObjects;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;

namespace ajwFunction
{
    public static class Function1
    {
        private static readonly HttpClient client = new HttpClient();

        [FunctionName("Function1")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request.");

            // parse query parameter
            string name = req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, "name", true) == 0)
                .Value;

            // parse query parameter
            string partIds = req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, "parts", true) == 0)
                .Value;

            
            if (partIds == null)
            {
                // Get request body
                dynamic data = await req.Content.ReadAsAsync<object>();
                partIds = data?.parts;
                name = data?.name;
            }

            

            if (partIds != null)
            {
                var partIdsList = GetIdList(partIds);

                var partsList = GetPartsFromIds(partIdsList);

                var rates = await GetExcahngeRatesAsync();

                var partsWithPricing = GetPrices(partsList, rates);

                var quote = GetQuote(partsWithPricing, partIds, name);

                // Get missing part numbers
                quote.MissingPartNumbers = string.Join(";", partIdsList.Where(a => !quote.QuotedPartNumbers.Contains(a)).ToList());

                if (!string.IsNullOrEmpty(quote.MissingPartNumbers))
                {
                    quote.QuoteStatus = "Missing Parts";
                }

                return quote == null
                   ? req.CreateResponse(HttpStatusCode.BadRequest, "Please pass real Ids")
                   : req.CreateResponse<Quote>(HttpStatusCode.OK, quote);
            }



            return partIds == null
                ? req.CreateResponse(HttpStatusCode.BadRequest, "Please pass parts on the query string or in the request body")
                : req.CreateResponse(HttpStatusCode.OK, "Hello " + name);
        }

        public static async Task<Rates> GetExcahngeRatesAsync()
        {
            // API key fro currency converstions
            var apiKey = "5c3198ffae287129764fc4165a8d63b5";

            //var baseCurrency = "GBP";
            var symbols = $"USD,EUR,GBP";

            var url = $"http://data.fixer.io/api/latest?access_key={apiKey}&symbols={symbols}";

            var responseString = await client.GetStringAsync(url);

            dynamic json = JsonConvert.DeserializeObject<object>(responseString);

            return new Rates
            {
                GBP = json?.rates?.GBP,
                EUR = json?.rates?.EUR,
                USD = json?.rates?.USD,
            };
        }

        public static Quote GetQuote(List<PartItemPricing> partItemPricings, string partsFromQuery, string quoteName)
        {
            // this orders by price then groups by part number (this will get the cheapest suppliers (if more than one)
            var grouped = partItemPricings.OrderBy(x => x.EURPrice)
                            .GroupBy(x => x.Part.PartNumber);

            string partsListFound = "";

            Quote quote = new Quote
            {
                GBPPrice = 0,
                USDPrice = 0,
                EstimatedLeadTime = 0
            };

            List<PartItemPricing> partsForHTMLReport = new List<PartItemPricing>();
           
            foreach(var group in grouped)
            {
                quote.GBPPrice += Math.Round(group.FirstOrDefault().GBPPrice, 2);
                quote.USDPrice += Math.Round(group.FirstOrDefault().USDPrice, 2);

                quote.EstimatedLeadTime += group.FirstOrDefault().Part.LeadTime;

                partsForHTMLReport.Add(group.FirstOrDefault());

                partsListFound += $" {group.Key};";
            }

            quote.HTMLForReport = GenerateHTMLFromParts(partsForHTMLReport, quote, quoteName);

            quote.QuotedPartNumbers = partsListFound;
            quote.QuoteStatus = "Done";
  
            return quote;
        }

        private static string GenerateHTMLFromParts(List<PartItemPricing> partsForHTMLReport, Quote quote, string quoteName)
        {
            if (string.IsNullOrEmpty(quoteName))
            {
                quoteName = $"{partsForHTMLReport.Count} parts";
            }

            string htmlString = $@"<h1> Quote: {quoteName} </h1>
                                   <br/>
                                    <table class='table'>
                                      <tr>
                                        <th>Part Number</th>
                                        <th>Description</th>
                                        <th>Supplier</th>
                                        <th>Lead Time</th>
                                        <th>Price (£)</th>
                                        <th>Price ($)</th>
                                      </tr>";

            foreach (var part in partsForHTMLReport)
            {
                htmlString += $@"<tr>
                                <td>{part.Part.PartNumber}</td>
                                <td>{part.Part.Decription}</td>
                                <td>{part.Part.SupplierName}</td>
                                <td>{part.Part.LeadTime}</td>
                                <td>{part.GBPPrice}</td>
                                <td>{part.USDPrice}</td>
                              </tr>";
            }

            htmlString += $@"</table>
                            <h3> Total Price GBP - {quote.GBPPrice} </h3>
                            <h3> Total Price USD - {quote.USDPrice} </h3>
                            <h3> Total Lead Time - {quote.EstimatedLeadTime} </h3>
                            <br/>";

            htmlString += @"<style type='text/css'>
                    .table { background: beige; }
                    </style>";

            return htmlString;
        }

        public static List<PartItemPricing> GetPrices(List<PartItem> parts, Rates rates)
        {

            List<PartItemPricing> partsWithPricing = new List<PartItemPricing>();

            foreach (var item in parts)
            {
                PartItemPricing partItemPricing = new PartItemPricing { Part = item };

                if (item.ListUnitPrice > 0)
                {
                    if (item.CurrencyCode == "EUR")
                    {
                        partItemPricing.EURPrice = item.ListUnitPrice;
                        partItemPricing.GBPPrice = item.ListUnitPrice * rates.GBP;
                        partItemPricing.USDPrice = item.ListUnitPrice * rates.USD;
                    }
                    else if (item.CurrencyCode == "GBP")
                    {
                        partItemPricing.GBPPrice = item.ListUnitPrice;

                        // convert GBPs to UUR then to USD
                        partItemPricing.EURPrice = item.ListUnitPrice / rates.GBP;
                        partItemPricing.USDPrice = partItemPricing.EURPrice * rates.USD;
                    }
                    else
                    {
                        partItemPricing.USDPrice += item.ListUnitPrice;

                        // convert USD to EUR then to GBP
                        partItemPricing.EURPrice = item.ListUnitPrice / rates.USD;
                        partItemPricing.GBPPrice += partItemPricing.EURPrice * rates.GBP;
                    }
                }
                else
                {
                    partItemPricing.EURPrice = 0;
                    partItemPricing.USDPrice = 0;
                    partItemPricing.GBPPrice = 0;
                }

                partItemPricing.EURPrice = Math.Round(partItemPricing.EURPrice, 2);
                partItemPricing.USDPrice = Math.Round(partItemPricing.USDPrice, 2);
                partItemPricing.GBPPrice = Math.Round(partItemPricing.GBPPrice, 2);

                partsWithPricing.Add(partItemPricing);
            }

            return partsWithPricing;
        }
          
        public static string[] GetIdList(string partIds)
        {
            return partIds.Split(';');
        }

        public static List<PartItem> GetPartsFromIds(string[] partIds)
        {
            List<PartItem> parts = new List<PartItem>();

            var connectionString = Environment.GetEnvironmentVariable("DatabaseConnectionString");

            string queryString = "SELECT CurrencyCode, ListUnitPrice, PartNumber, LeadTime, SupplierName, Description  " +
                                 "FROM  [dbo].[MasterPriceList] " +
                                 "WHERE PartNumber = @partId";


            foreach (var partId in partIds)
            {

                var id = partId.Trim();

                // check id is 6 digit number else skip
                if (Regex.IsMatch(id, "^[0-9]{6,6}$"))
                {
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {

                        SqlCommand command = new SqlCommand(queryString, connection);
                        command.Parameters.AddWithValue("@partId", partId.Trim());
                        connection.Open();
                        SqlDataReader reader = command.ExecuteReader();

                        try
                        {
                            while (reader.Read())
                            {

                       
                                var part = new PartItem
                                {
                                    CurrencyCode = reader["CurrencyCode"].ToString(),
                                    PartNumber = reader["PartNumber"].ToString(),
                                    ListUnitPrice = float.Parse(reader["ListUnitPrice"].ToString()),
                                    SupplierName = reader["SupplierName"].ToString(),
                                    Decription = reader["Description"].ToString()
                                };

                                double leadTime = 0;

                                if(float.TryParse(reader["LeadTime"].ToString(), out float parsed))
                                {
                                    leadTime = parsed;
                                }

                                part.LeadTime = leadTime;

                                parts.Add(part);
                            }
                        }
                        finally
                        {
                            // Always call Close when done reading.
                            reader.Close();
                        }
                    }

                }
            }

            return parts;
        }
    }
}
