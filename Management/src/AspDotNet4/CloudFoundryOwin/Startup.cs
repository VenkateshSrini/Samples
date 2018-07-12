using Microsoft.Owin;
using Microsoft.Owin.BuilderProperties;
using MySql.Data.MySqlClient;
using Owin;
using Steeltoe.CloudFoundry.Connector;
using Steeltoe.CloudFoundry.Connector.MySql;
using Steeltoe.CloudFoundry.Connector.Relational;
using Steeltoe.CloudFoundry.Connector.Relational.MySql;
using Steeltoe.CloudFoundry.Connector.Services;
using Steeltoe.Common.Diagnostics;
using Steeltoe.Common.HealthChecks;
using Steeltoe.Management.Endpoint.Health;
using Steeltoe.Management.Endpoint.Health.Contributor;
using Steeltoe.Management.EndpointOwin.CloudFoundry;
using Steeltoe.Management.EndpointOwin.Diagnostics;
using Steeltoe.Management.EndpointOwin.Env;
using Steeltoe.Management.EndpointOwin.Health;
using Steeltoe.Management.EndpointOwin.HeapDump;
using Steeltoe.Management.EndpointOwin.Info;
using Steeltoe.Management.EndpointOwin.Loggers;
using Steeltoe.Management.EndpointOwin.Mappings;
using Steeltoe.Management.EndpointOwin.Metrics;
using Steeltoe.Management.EndpointOwin.Refresh;
using Steeltoe.Management.EndpointOwin.ThreadDump;
using Steeltoe.Management.EndpointOwin.Trace;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Routing;

[assembly: OwinStartup(typeof(CloudFoundryOwin.Startup))]

namespace CloudFoundryOwin
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            var config = GlobalConfiguration.Configuration;

            // Add WebApi
            WebApiConfig.Register(config);

            // Add MVC
            RouteConfig.RegisterRoutes(RouteTable.Routes);

            ApplicationConfig.Configure("development");
            ApplicationConfig.ConfigureLogging();

            app
                .UseDiagnosticSourceMiddleware(ApplicationConfig.LoggerFactory)
                .UseCloudFoundrySecurityMiddleware(ApplicationConfig.Configuration, ApplicationConfig.LoggerFactory)
                .UseCloudFoundryEndpointMiddleware(ApplicationConfig.Configuration, ApplicationConfig.LoggerFactory)
                .UseEnvEndpointMiddleware(ApplicationConfig.Configuration, ApplicationConfig.LoggerFactory)
                .UseHealthEndpointMiddleware(new HealthOptions(ApplicationConfig.Configuration), new DefaultHealthAggregator(), GetHealthContributors(), ApplicationConfig.LoggerFactory)
                .UseHeapDumpEndpointMiddleware(ApplicationConfig.Configuration, ApplicationConfig.GetContentRoot(), ApplicationConfig.LoggerFactory)
                .UseInfoEndpointMiddleware(ApplicationConfig.Configuration, ApplicationConfig.LoggerFactory)
                .UseLoggersEndpointMiddleware(ApplicationConfig.Configuration, ApplicationConfig.LoggerProvider, ApplicationConfig.LoggerFactory)
                .UseMappingEndpointMiddleware(ApplicationConfig.Configuration, config.Services.GetApiExplorer(), ApplicationConfig.LoggerFactory)
                .UseMetricsEndpointMiddleware(ApplicationConfig.Configuration, ApplicationConfig.LoggerFactory)
                .UseRefreshEndpointMiddleware(ApplicationConfig.Configuration, ApplicationConfig.LoggerFactory)
                .UseThreadDumpEndpointMiddleware(ApplicationConfig.Configuration, ApplicationConfig.LoggerFactory)
                .UseTraceEndpointMiddleware(ApplicationConfig.Configuration, null, ApplicationConfig.LoggerFactory);

            config.EnsureInitialized();

            DiagnosticsManager.Instance.Start();

            var properties = new AppProperties(app.Properties);
            CancellationToken token = properties.OnAppDisposing;
            Task.Run(() => RunApp(token));
        }

        private static void RunApp(CancellationToken cancelToken)
        {
            while (true)
            {
                if (cancelToken.IsCancellationRequested)
                {
                    DiagnosticsManager.Instance.Stop();
                    return;
                }
            }
        }

        private IEnumerable<IHealthContributor> GetHealthContributors()
        {
            var info = ApplicationConfig.Configuration.GetSingletonServiceInfo<MySqlServiceInfo>();
            var mySqlConfig = new MySqlProviderConnectorOptions(ApplicationConfig.Configuration);
            var factory = new MySqlProviderConnectorFactory(info, mySqlConfig, MySqlTypeLocator.MySqlConnection);

            var healthContributors = new List<IHealthContributor>
            {
                new DiskSpaceContributor(),
                new RelationalHealthContributor(new MySqlConnection(factory.CreateConnectionString()))
            };

            return healthContributors;
        }
    }
}
