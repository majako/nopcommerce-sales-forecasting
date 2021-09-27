using System.Collections.Generic;
using System.Linq;
using Nop.Core;
using Nop.Core.Domain.Tasks;
using Nop.Services.Common;
using Nop.Services.Plugins;
using Nop.Services.Tasks;
using NUglify.Helpers;

namespace Majako.Plugin.Misc.SalesForecasting
{
    public class SalesForecastingPlugin : BasePlugin, IMiscPlugin
    {
        public const string SystemName = "Misc.SalesForecasting";

        private readonly IWebHelper _webHelper;

        public SalesForecastingPlugin(
            IWebHelper webHelper)
        {
            _webHelper = webHelper;
        }

        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/SalesForecasting/Configure";
        }

        public override void Install()
        {
            base.Install();
        }

        public override void Uninstall()
        {
            base.Uninstall();
        }
    }
}
