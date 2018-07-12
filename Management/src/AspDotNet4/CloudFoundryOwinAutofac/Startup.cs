using Autofac;
using Autofac.Integration.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Owin;
using Microsoft.Owin.BuilderProperties;
using Owin;
using Steeltoe.CloudFoundry.ConnectorAutofac;
using Steeltoe.Common.Configuration.Autofac;
using Steeltoe.Common.Diagnostics;
using Steeltoe.Extensions.Configuration.CloudFoundry;
using Steeltoe.Management.EndpointAutofac;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Routing;

[assembly: OwinStartup(typeof(CloudFoundryOwinAutofac.Startup))]

namespace CloudFoundryOwinAutofac
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            //Environment.SetEnvironmentVariable("VCAP_APPLICATION", @"{
            //      'cf_api': 'https://api.cf.beet.springapps.io',
            //      'limits': {
            //                'fds': 16384
            //      },
            //      'application_name': 'actuators-owin-autofac',
            //      'application_uris': [
            //        'actuators-owin-autofac.apps.beet.springapps.io'
            //      ],
            //      'name': 'actuators-owin-autofac',
            //      'space_name': 'samples',
            //      'space_id': 'a0c1db62-35b9-4b0f-8e0c-d180ff92e804',
            //      'uris': [
            //        'actuators-owin-autofac.apps.beet.springapps.io'
            //      ],
            //      'users': null,
            //      'application_id': 'f72fc20f-c205-4492-ae76-4d73f5a9b40b'
            //    }");

            // Code that runs on application startup
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            RouteConfig.RegisterRoutes(RouteTable.Routes);

            ApplicationConfig.Register("development");
            ApplicationConfig.ConfigureLogging();

            var builder = new ContainerBuilder();

            // Register all the controllers with Autofac
            builder.RegisterControllers(typeof(Startup).Assembly);
            builder.RegisterCloudFoundryOptions(ApplicationConfig.Configuration);
            builder.RegisterConfiguration(ApplicationConfig.Configuration);
            builder.RegisterMySqlConnection(ApplicationConfig.Configuration);
            builder.RegisterCloudFoundryActuators(ApplicationConfig.Configuration, GlobalConfiguration.Configuration.Services.GetApiExplorer());

            var container = ApplicationConfig.Container = builder.Build();

            container.StartActuators();

            DependencyResolver.SetResolver(new AutofacDependencyResolver(container));

            var startupLogger = ApplicationConfig.LoggerFactory.CreateLogger<Startup>();

            startupLogger.LogTrace("Configuring OWIN Pipeline");

            // using autofac...

            // app cors config here is not needed, but does not interfere with Steeltoe config
            // app.UseCors(CorsOptions.AllowAll);
            app.UseAutofacMiddleware(ApplicationConfig.Container);

            startupLogger.LogTrace("Application is online!");

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
    }
}
