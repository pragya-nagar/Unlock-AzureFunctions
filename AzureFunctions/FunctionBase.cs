using AzureFunctions.FreeTrialFunction;
using AzureFunctions.FreeTrialFunction.Interface;
using AzureFunctions.Repository;
using AzureFunctions.Repository.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace AzureFunctions
{
    public class Global
    {
        public static string envVariable = Environment.GetEnvironmentVariable("EnvironmentName");
       
    }
    public class FunctionBase : Global
    {
        protected readonly IServiceProvider _serviceProvider;
        public static string EnvironmentName = envVariable;

        public FunctionBase()
        {
            _serviceProvider = ConfigureServices();
        }


        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
               // .AddJsonFile($"appsettings.{EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables()
              
                .Build();

            services.AddSingleton(config);
          

            services.AddTransient<IAdminRepository, AdminRepository>();
            services.AddTransient<INotificationRepository, NotificationRepository>();
            services.AddTransient<IOkrServiceRepository, OkrServiceRepository>();
            services.AddTransient<IArchieveFunctions, ArchieveFunctions>();
            services.AddTransient<INotificationFunctions, NotificationFunctions>();

            var newServiceProvider = services.BuildServiceProvider();

            return newServiceProvider;
        }
    }
}
