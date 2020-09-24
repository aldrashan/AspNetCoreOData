﻿// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Extensions;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Attributes;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.AspNetCore.OData.TestCommon;
using Microsoft.AspNetCore.OData.Tests.Extensions;
using Microsoft.AspNetCore.OData.Tests.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using Microsoft.OData.UriParser;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.AspNetCore.OData.Tests.Routing
{
    public class ODataCountTest : WebODataTestBase<ODataCountTest.Startup>
    {
        public class Startup : TestStartupBase
        {
            public override void ConfigureServices(IServiceCollection services)
            {
                services.ConfigureControllers(typeof(DollarCountEntitiesController));

                IEdmModel model = GetEdmModel();
                services.AddOData(options => options.AddModel(model)
                    .Count().SetMaxTop(null).Expand().Select().OrderBy().Filter());
            }
        }

        public ODataCountTest(WebODataTestFixture<Startup> factory)
            : base(factory)
        {
        }

        public static TheoryDataSet<string, int> DollarCountData
        {
            get
            {
                var data = new TheoryDataSet<string, int>();

                // $count follows entity set, structural collection property or navigation collection property.
                data.Add("DollarCountEntities/$count", 10);
                data.Add("DollarCountEntities/$count?$filter=Id gt 5", 5);
                data.Add("DollarCountEntities/Microsoft.AspNetCore.OData.Tests.Routing.DerivedDollarCountEntity/$count", 5);
                data.Add("DollarCountEntities(5)/StringCollectionProp/$count", 2);
                data.Add("DollarCountEntities(5)/StringCollectionProp/$count?$filter=$it eq '2'", 1);
                data.Add("DollarCountEntities(5)/EnumCollectionProp/$count", 3);
                data.Add("DollarCountEntities(5)/EnumCollectionProp/$count?$filter=$it has Microsoft.AspNetCore.OData.Tests.Models.Color'Green'", 2);
                data.Add("DollarCountEntities(5)/TimeSpanCollectionProp/$count", 4);
                data.Add("DollarCountEntities(5)/ComplexCollectionProp/$count", 5);
                data.Add("DollarCountEntities(5)/EntityCollectionProp/$count", 4);

                // $count follows unbound function that returns collection.
                data.Add("UnboundFunctionReturnsPrimitveCollection()/$count", 6);
                data.Add("UnboundFunctionReturnsEnumCollection()/$count", 7);
                data.Add("UnboundFunctionReturnsDateTimeOffsetCollection()/$count", 8);
                data.Add("UnboundFunctionReturnsDateCollection()/$count", 18);
                data.Add("UnboundFunctionReturnsComplexCollection()/$count", 9);
                data.Add("UnboundFunctionReturnsEntityCollection()/$count", 10);
                data.Add("UnboundFunctionReturnsEntityCollection()/Microsoft.AspNetCore.OData.Tests.Routing.DerivedDollarCountEntity/$count", 11);

                // $count follows bound function that returns collection.
                data.Add("DollarCountEntities/Default.BoundFunctionReturnsPrimitveCollection()/$count", 12);
                data.Add("DollarCountEntities/Default.BoundFunctionReturnsEnumCollection()/$count", 13);
                data.Add("DollarCountEntities/Default.BoundFunctionReturnsDateTimeOffsetCollection()/$count", 14);
                data.Add("DollarCountEntities/Default.BoundFunctionReturnsComplexCollection()/$count", 15);
                data.Add("DollarCountEntities/Default.BoundFunctionReturnsComplexCollection()/$count?$filter=contains(StringProp,'1')", 7);
                data.Add("DollarCountEntities/Default.BoundFunctionReturnsEntityCollection()/$count", 10);
                data.Add("DollarCountEntities/Default.BoundFunctionReturnsEntityCollection()/Microsoft.AspNetCore.OData.Tests.Routing.DerivedDollarCountEntity/$count", 5);

                return data;
            }
        }

        [Theory]
        [MemberData(nameof(DollarCountData))]
        public async Task DollarCount_Works(string uri, int expectedCount)
        {
            // Arrange & Act
            HttpResponseMessage response = await Client.GetAsync(uri);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            int actualCount = Int32.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(expectedCount, actualCount);
        }

        [Fact]
        public async Task GetCollection_Works_WithoutDollarCount()
        {
            // Arrange
            var uri = "DollarCountEntities(5)/StringCollectionProp";

            // Act
            HttpResponseMessage response = await Client.GetAsync(uri);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            JObject result = await response.Content.ReadAsObject<JObject>();
            Assert.Equal(2, result["value"].Count());
            Assert.Equal("1", result["value"][0]);
            Assert.Equal("2", result["value"][1]);
        }

        [Fact]
        public async Task Function_Works_WithDollarCountInQueryOption()
        {
            // Arrange
            var uri = "DollarCountEntities/Default.BoundFunctionReturnsComplexCollection()?$count=true";

            // Act
            HttpResponseMessage response = await Client.GetAsync(uri);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            JObject result = await response.Content.ReadAsObject<JObject>();
            Assert.Equal(15, result["@odata.count"]);
        }

        [Fact]
        public async Task GetCount_Throws_DollarCountNotAllowed()
        {
            // Arrange
            var uri = "DollarCountEntities(5)/DollarCountNotAllowedCollectionProp/$count";

            // Act
            HttpResponseMessage response = await Client.GetAsync("http://localhost/" + uri);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadAsStringAsync();
            Assert.Contains(
                "The query specified in the URI is not valid. Query option 'Count' is not allowed. " +
                "To allow it, set the 'AllowedQueryOptions' property on EnableQueryAttribute or QueryValidationSettings.",
                result);
        }

        public static IEdmModel GetEdmModel()
        {
            var builder = new ODataConventionModelBuilder();
            var entityCollection = builder.EntitySet<DollarCountEntity>("DollarCountEntities").EntityType.Collection;

            // Add unbound functions that return collection.
            FunctionConfiguration function = builder.Function("UnboundFunctionReturnsPrimitveCollection");
            function.IsComposable = true;
            function.ReturnsCollection<int>();

            function = builder.Function("UnboundFunctionReturnsEnumCollection");
            function.IsComposable = true;
            function.ReturnsCollection<Color>();

            function = builder.Function("UnboundFunctionReturnsDateTimeOffsetCollection");
            function.IsComposable = true;
            function.ReturnsCollection<DateTimeOffset>();

            function = builder.Function("UnboundFunctionReturnsDateCollection");
            function.IsComposable = true;
            function.ReturnsCollection<Date>();

            function = builder.Function("UnboundFunctionReturnsComplexCollection");
            function.IsComposable = true;
            function.ReturnsCollection<DollarCountComplex>();

            function = builder.Function("UnboundFunctionReturnsEntityCollection");
            function.IsComposable = true;
            function.ReturnsCollectionFromEntitySet<DollarCountEntity>("DollarCountEntities");

            // Add bound functions that return collection.
            function = entityCollection.Function("BoundFunctionReturnsPrimitveCollection");
            function.IsComposable = true;
            function.ReturnsCollection<DateTimeOffset>();

            function = entityCollection.Function("BoundFunctionReturnsEnumCollection");
            function.IsComposable = true;
            function.ReturnsCollection<Color>();

            function = entityCollection.Function("BoundFunctionReturnsDateTimeOffsetCollection");
            function.IsComposable = true;
            function.ReturnsCollection<DateTimeOffset>();

            function = entityCollection.Function("BoundFunctionReturnsComplexCollection");
            function.IsComposable = true;
            function.ReturnsCollection<DollarCountComplex>();

            function = entityCollection.Function("BoundFunctionReturnsEntityCollection");
            function.IsComposable = true;
            function.ReturnsCollectionFromEntitySet<DollarCountEntity>("DollarCountEntities");

            return builder.GetEdmModel();
        }

        public class DollarCountEntitiesController : ODataController
        {
            public IList<DollarCountEntity> Entities;

            public DollarCountEntitiesController()
            {
                Entities = new List<DollarCountEntity>();
                for (int i = 1; i <= 10; i++)
                {
                    if (i % 2 == 0)
                    {
                        var newEntity = new DollarCountEntity
                        {
                            Id = i,
                            StringCollectionProp = Enumerable.Range(1, 2).Select(index => index.ToString()).ToArray(),
                            EnumCollectionProp = new[] { Color.Red, Color.Blue | Color.Green, Color.Green },
                            TimeSpanCollectionProp = Enumerable.Range(1, 4).Select(_ => TimeSpan.Zero).ToArray(),
                            ComplexCollectionProp =
                                Enumerable.Range(1, 5).Select(_ => new DollarCountComplex()).ToArray(),
                            EntityCollectionProp = Entities.ToArray(),
                            DollarCountNotAllowedCollectionProp = new[] { 1, 2, 3, 4 }
                        };
                        Entities.Add(newEntity);
                    }
                    else
                    {
                        var newEntity = new DerivedDollarCountEntity
                        {
                            Id = i,
                            StringCollectionProp = Enumerable.Range(1, 2).Select(index => index.ToString()).ToArray(),
                            EnumCollectionProp = new[] { Color.Red, Color.Blue | Color.Green, Color.Green },
                            TimeSpanCollectionProp = Enumerable.Range(1, 4).Select(_ => TimeSpan.Zero).ToArray(),
                            ComplexCollectionProp =
                                Enumerable.Range(1, 5).Select(_ => new DollarCountComplex()).ToArray(),
                            EntityCollectionProp = Entities.ToArray(),
                            DollarCountNotAllowedCollectionProp = new[] { 1, 2, 3, 4 },
                            DerivedProp = "DerivedProp"
                        };
                        Entities.Add(newEntity);
                    }
                }
            }

            [EnableQuery(PageSize = 3)]
            public IActionResult Get()
            {
                return Ok(Entities);
            }

            [EnableQuery]
            public IActionResult GetDollarCountEntitiesFromDerivedDollarCountEntity()
            {
                return Ok(Entities.OfType<DerivedDollarCountEntity>());
            }

            [EnableQuery]
            public IActionResult Get(int key)
            {
                return Ok(Entities.Single(e => e.Id == key));
            }

            public IActionResult GetStringCollectionProp(int key, ODataQueryOptions<string> options)
            {
                IQueryable<string> result = Entities.Single(e => e.Id == key).StringCollectionProp.AsQueryable();

                if (options.Filter != null)
                {
                    result = options.Filter.ApplyTo(result, new ODataQuerySettings()).Cast<string>();
                }

                if (Request.ODataFeature().Path.OfType<CountSegment>().Any())
                {
                    return Ok(result.Count());
                }

                return Ok(result);
            }

            [HttpGet]
            [ODataRoute("DollarCountEntities({key})/EnumCollectionProp/$count")]
            public IActionResult GetCountForEnumCollectionProp(int key, ODataQueryOptions<Color> options)
            {
                IQueryable<Color> result = Entities.Single(e => e.Id == key).EnumCollectionProp.AsQueryable();

                if (options.Filter != null)
                {
                    result = options.Filter.ApplyTo(result, new ODataQuerySettings()).Cast<Color>();
                }

                return Ok(result.Count());
            }

            [EnableQuery]
            public IActionResult GetTimeSpanCollectionProp(int key)
            {
                return Ok(Entities.Single(e => e.Id == key).TimeSpanCollectionProp);
            }

            [EnableQuery]
            public IActionResult GetComplexCollectionProp(int key)
            {
                return Ok(Entities.Single(e => e.Id == key).ComplexCollectionProp);
            }

            [EnableQuery]
            public IActionResult GetEntityCollectionProp(int key)
            {
                return Ok(Entities.Single(e => e.Id == key).EntityCollectionProp);
            }

            [EnableQuery(AllowedQueryOptions = AllowedQueryOptions.All ^ AllowedQueryOptions.Count)]
            public IActionResult GetDollarCountNotAllowedCollectionProp(int key)
            {
                return Ok(Entities.Single(e => e.Id == key).EntityCollectionProp);
            }

            [HttpGet]
            [ODataRoute("UnboundFunctionReturnsPrimitveCollection()/$count")]
            public IActionResult UnboundFunctionReturnsPrimitveCollectionWithDollarCount()
            {
                return Ok(6);
            }

            [HttpGet]
            [ODataRoute("UnboundFunctionReturnsEnumCollection()/$count")]
            public IActionResult UnboundFunctionReturnsEnumCollectionWithDollarCount()
            {
                return Ok(7);
            }

            [HttpGet]
            [ODataRoute("UnboundFunctionReturnsDateTimeOffsetCollection()/$count")]
            public IActionResult UnboundFunctionReturnsDateTimeOffsetCollectionWithDollarCount()
            {
                return Ok(8);
            }

            [HttpGet]
            [ODataRoute("UnboundFunctionReturnsDateCollection()/$count")]
            public IActionResult UnboundFunctionReturnsDateCollectionWithDollarCount()
            {
                return Ok(18);
            }

            [HttpGet]
            [ODataRoute("UnboundFunctionReturnsComplexCollection()/$count")]
            public IActionResult UnboundFunctionReturnsComplexCollectionWithDollarCount()
            {
                return Ok(9);
            }

            [HttpGet]
            [ODataRoute("UnboundFunctionReturnsEntityCollection()/$count")]
            public IActionResult UnboundFunctionReturnsEntityCollectionWithDollarCount()
            {
                return Ok(10);
            }

            [HttpGet]
            [ODataRoute("UnboundFunctionReturnsEntityCollection()/Microsoft.AspNetCore.OData.Tests.Routing.DerivedDollarCountEntity/$count")]
            public IActionResult UnboundFunctionReturnsDerivedEntityCollectionWithDollarCount()
            {
                return Ok(11);
            }

            [HttpGet]
            [EnableQuery]
            [ODataRoute("DollarCountEntities/Default.BoundFunctionReturnsPrimitveCollection()/$count")]
            public IActionResult BoundFunctionReturnsPrimitveCollectionOnCollectionOfDollarCountEntity()
            {
                return Ok(Enumerable.Range(1, 12).Select(_ => DateTimeOffset.Now));
            }

            [HttpGet]
            [EnableQuery]
            [ODataRoute("DollarCountEntities/Default.BoundFunctionReturnsEnumCollection()/$count")]
            public IActionResult BoundFunctionReturnsEnumCollectionOnCollectionOfDollarCountEntity()
            {
                return Ok(Enumerable.Range(1, 13).Select(_ => Color.Green));
            }

            [HttpGet]
            [EnableQuery]
            [ODataRoute("DollarCountEntities/Default.BoundFunctionReturnsDateTimeOffsetCollection()/$count")]
            public IActionResult BoundFunctionReturnsDateTimeOffsetCollectionOnCollectionOfDollarCountEntity()
            {
                return Ok(Enumerable.Range(1, 14).Select(_ => DateTimeOffset.Now));
            }

            [HttpGet]
            [EnableQuery]
            [ODataRoute("DollarCountEntities/Default.BoundFunctionReturnsComplexCollection()/$count")]
            public IActionResult BoundFunctionReturnsComplexCollectionOnCollectionOfDollarCountEntity()
            {
                return Ok(Enumerable.Range(1, 15).Select(i => new DollarCountComplex { StringProp = i.ToString() }));
            }

            [HttpGet]
            [EnableQuery]
            [ODataRoute("DollarCountEntities/Default.BoundFunctionReturnsEntityCollection()/$count")]
            public IActionResult BoundFunctionReturnsEntityCollectionOnCollectionOfDollarCountEntity()
            {
                return Ok(Entities);
            }

            [HttpGet]
            [EnableQuery]
            [ODataRoute("DollarCountEntities/Default.BoundFunctionReturnsEntityCollection()/Microsoft.AspNetCore.OData.Tests.Routing.DerivedDollarCountEntity/$count")]
            public IActionResult BoundFunctionReturnsDerivedEntityCollectionOnCollectionOfDollarCountEntity()
            {
                return Ok(Entities.OfType<DerivedDollarCountEntity>());
            }
        }

        public class DollarCountEntity
        {
            public int Id { get; set; }
            public string[] StringCollectionProp { get; set; }
            public Color[] EnumCollectionProp { get; set; }
            public TimeSpan[] TimeSpanCollectionProp { get; set; }
            public DollarCountComplex[] ComplexCollectionProp { get; set; }
            public DollarCountEntity[] EntityCollectionProp { get; set; }
            public int[] DollarCountNotAllowedCollectionProp { get; set; }
        }

        public class DerivedDollarCountEntity : DollarCountEntity
        {
            public string DerivedProp { get; set; }
        }

        public class DollarCountComplex
        {
            public string StringProp { get; set; }
        }
    }
}