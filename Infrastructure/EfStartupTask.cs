using Nop.Core.Data;
using Nop.Core.Infrastructure;
using Nop.Data;

namespace Majako.Plugin.Misc.ChannableApi.Infrastructure
{
    public class EfStartupTask : IStartupTask
    {
        public int Order => 0;

        public void Execute()
        {
            if (!DataSettingsManager.DatabaseIsInstalled)
                return;

            var context = EngineContext.Current.Resolve<IDbContext>();
            ExecuteErrorMessageToUtcSql(context);
        }

        private static void ExecuteErrorMessageToUtcSql(IDbContext context)
        {
            var dbCreationScript = @"

              BEGIN TRANSACTION
              IF NOT EXISTS (SELECT 1 FROM [ScheduleTask] WHERE [Type] = 'Majako.Plugin.Misc.ChannableApi.ScheduleTasks.GenerateFeedFilesTask')
              BEGIN
              INSERT INTO [ScheduleTask] ([Name], [Seconds], [Type], [Enabled], [StopOnError])
              VALUES ('Generate feed files', 2147483647, 'Majako.Plugin.Misc.ChannableApi.ScheduleTasks.GenerateFeedFilesTask', 0, 0)
              END
                
             COMMIT TRANSACTION";

            context.ExecuteSqlCommand(dbCreationScript);
            context.SaveChanges();
        }
    }
}
