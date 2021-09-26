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
        private readonly IWebHelper _webHelper;
        private readonly IScheduleTaskService _scheduleTaskService;

        public SalesForecastingPlugin(
            IWebHelper webHelper,
            IScheduleTaskService scheduleTaskService)
        {
            _webHelper = webHelper;
            _scheduleTaskService = scheduleTaskService;
        }

        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/SalesForecasting/Configure";
        }

        public override void Install()
        {
            InstallScheduleTasks();
            base.Install();
        }

        public override void Uninstall()
        {
            UninstallScheduleTasks();
            base.Uninstall();
        }

        private void InstallScheduleTasks()
        {
            new List<ScheduleTask>
            {
                new ScheduleTask
                {
                    Name = GenerateFeedFilesTask.Name,
                    Seconds = 3600,
                    Type = GenerateFeedFilesTask.Type,
                    Enabled = false,
                    StopOnError = false
                }
            }.ForEach(_scheduleTaskService.InsertTask);
        }

        private void UninstallScheduleTasks()
        {
            _scheduleTaskService
                .GetAllTasks(true)
                .Where(x =>
                    x.Type == GenerateFeedFilesTask.Type ||
                    x.Type == GenerateFeedFilesTask.Type)
                .ForEach(_scheduleTaskService.DeleteTask);
        }
    }
}
