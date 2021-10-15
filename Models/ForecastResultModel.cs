using System;
using System.Collections.Generic;
using Nop.Web.Areas.Admin.Models.Catalog;
using Nop.Web.Areas.Admin.Factories;
using Nop.Core.Infrastructure;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Majako.Plugin.Misc.SalesForecasting.Models
{
    public class ForecastResultModel : BaseSearchModel
    {
        public string ResultsJson { get; set; }
        public ForecastSearchModel SearchModel { get; set; }

        public ForecastResultModel()
        {
            ResultsJson = "[]";
        }
    }
}
