using Autofac;
using Majako.Plugin.Misc.SalesForecasting.Services;
using Nop.Core.Configuration;
using Nop.Core.Infrastructure;
using Nop.Core.Infrastructure.DependencyManagement;

namespace Majako.Plugin.Misc.SalesForecasting.Infrastructure
{
    public class DependencyRegistrar : IDependencyRegistrar
    {
        public virtual void Register(ContainerBuilder builder, ITypeFinder typeFinder, NopConfig config)
        {
            // services
            builder.RegisterType<SalesForecastingService>().InstancePerLifetimeScope();
        }

        public int Order => 1;
    }
}
