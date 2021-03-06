﻿using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Dispatcher;
using System.Web.OData.Extensions;
using Newtonsoft.Json.Linq;
using Nuwa;
using WebStack.QA.Test.OData.Common;
using Xunit;
using Xunit.Extensions;

namespace WebStack.QA.Test.OData.DollarLevels
{
    [NuwaFramework]
    public class DollarLevelsTest
    {
        private const string NameSpace = "WebStack.QA.Test.OData.DollarLevels";

        [NuwaBaseAddress]
        public string BaseAddress { get; set; }

        [NuwaHttpClient]
        public HttpClient Client { get; set; }

        [NuwaConfiguration]
        public static void UpdateConfiguration(HttpConfiguration configuration)
        {
            var controllers = new[] { typeof(DLManagersController) };
            TestAssemblyResolver resolver = new TestAssemblyResolver(new TypesInjectionAssembly(controllers));

            configuration.IncludeErrorDetailPolicy = IncludeErrorDetailPolicy.Always;
            configuration.Services.Replace(typeof(IAssembliesResolver), resolver);

            configuration.Routes.Clear();
            configuration.MapODataServiceRoute("OData", "odata", DollarLevelsEdmModel.GetConventionModel());
            configuration.EnsureInitialized();
        }

        [Theory]
        [InlineData("$expand=Manager($levels=-1)", 
            "Levels option must be a non-negative integer or 'max', it is set to '-1' instead.")]
        [InlineData("$expand=Manager($levels=2;$expand=DirectReports($levels=-1))", 
            "Levels option must be a non-negative integer or 'max', it is set to '-1' instead.")]
        [InlineData("$expand=Manager($levels=-1;$expand=DirectReports($levels=max))",
            "Levels option must be a non-negative integer or 'max', it is set to '-1' instead.")]
        [InlineData("$expand=DirectReports($expand=Manager($levels=10))",
            "The request includes a $expand path which is too deep. The maximum depth allowed is 4.")]
        [InlineData("$expand=DirectReports($levels=3;$expand=Manager($levels=3))",
            "The request includes a $expand path which is too deep. The maximum depth allowed is 4.")]
        public async Task LevelsWithInvalidValue(string query, string errorMessage) 
        {
            string requestUri = this.BaseAddress + "/odata/DLManagers(5)?" + query;

            HttpResponseMessage response = await this.Client.GetAsync(requestUri);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            var result = await response.Content.ReadAsAsync<JObject>();
            Assert.Contains(errorMessage,
                result["error"]["innererror"]["message"].Value<string>());
        }

        [Theory]
        [InlineData("$expand=Manager($levels=3)", 
            "$expand=Manager($expand=Manager($expand=Manager))")]
        [InlineData("$expand=Manager($levels=0)", 
            "")]
        [InlineData("$expand=Manager($levels=2;$expand=DirectReports($levels=2))",
            "$expand=Manager($expand=Manager($expand=DirectReports($expand=DirectReports)),DirectReports($expand=DirectReports))")]
        [InlineData("$expand=Manager($select=ID;$expand=DirectReports($levels=2;$select=Name))",
            "$expand=Manager($select=ID;$expand=DirectReports($expand=DirectReports($select=Name);$select=Name))")]
        public async Task LevelsWithValidNumber(string originalQuery, string expandedQuery) 
        {
            string requestUri = this.BaseAddress + "/odata/DLManagers(5)?" + originalQuery;

            HttpResponseMessage response = await this.Client.GetAsync(requestUri);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadAsAsync<JObject>();

            requestUri = this.BaseAddress + "/odata/DLManagers(5)?" + expandedQuery;
            response = await this.Client.GetAsync(requestUri);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var baseline = await response.Content.ReadAsAsync<JObject>();

            Assert.True(JToken.DeepEquals(baseline, result));
        }

        [Theory]
        [InlineData("$expand=Manager($levels=max)",
            "$expand=Manager($expand=Manager($expand=Manager($expand=Manager)))")]
        [InlineData("$expand=Manager($levels=max;$expand=DirectReports($levels=2))",
            "$expand=Manager($expand=Manager($expand=DirectReports($expand=DirectReports)),DirectReports($expand=DirectReports))")]
        [InlineData("$expand=DirectReports($levels=2;$expand=Manager($levels=max))",
            "$expand=DirectReports($expand=DirectReports($expand=Manager($expand=Manager)),Manager($expand=Manager($expand=Manager)))")]
        [InlineData("$expand=DirectReports($levels=max;$expand=Manager($levels=max))",
            "$expand=DirectReports($expand=DirectReports($expand=DirectReports($expand=DirectReports,Manager),Manager($expand=Manager)),Manager($expand=Manager($expand=Manager)))")]
        public async Task LevelsWithMaxValue(string originalQuery, string expandedQuery) 
        {
            // $expand=Manager($levels=max)
            string requestUri = this.BaseAddress + "/odata/DLManagers(5)?" + originalQuery;

            HttpResponseMessage response = await this.Client.GetAsync(requestUri);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadAsAsync<JObject>();

            requestUri = this.BaseAddress + "/odata/DLManagers(5)?" + expandedQuery;
            response = await this.Client.GetAsync(requestUri);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var baseline = await response.Content.ReadAsAsync<JObject>();

            Assert.True(JToken.DeepEquals(baseline, result));
        }
    }
}
