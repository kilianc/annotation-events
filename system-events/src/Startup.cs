using Amazon.SimpleNotificationService;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Prometheus;
using SystemEvents.Configuration;
using SystemEvents.ServiceExtensions;
using SystemEvents.Services;
using SystemEvents.Utils;
using SystemEvents.Utils.Interfaces;

namespace SystemEvents
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseOpenApi();
            app.UseSwaggerUi3();

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseHttpMetrics();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHealthChecks("/health");
                endpoints.MapControllers();
                endpoints.MapMetrics();
            });
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var configuration = new AppConfiguration();
            
            // Inject the configuration
            services.AddSingleton<IAppConfiguration>(provider => configuration);
            services.AddSingleton<IElasticsearchClientConfiguration>(provider => configuration);
            services.AddAdvanceConfiguration(configuration);

            services.AddSingleton<IElasticsearchTimeStampFactory, ElasticsearchTimeStampFactory>();
            services.AddElasticsearch(configuration);
            services.AddSingleton<IElasticsearchIndexFactory, ElasticsearchIndexFactory>();
            services.AddSingleton<IMonitoredElasticsearchClient, PrometheusMonitoredElasticsearchClient>();

            // Inject Notification Channel Clients
            if (!string.IsNullOrWhiteSpace(configuration.AdvanceConfigurationPath))
            {
                services.AddHttpClient<SlackWebhookService>();
                services.AddDefaultAWSOptions(Configuration.GetAWSOptions());
                services.AddAWSService<IAmazonSimpleNotificationService>();
                services.AddSingleton<IMonitoredAmazonSimpleNotificationService, MonitoredAmazonSimpleNotificationService>();
                services.AddSingleton<ICategorySubscriptionNotifier, CategorySubscriptionNotifier>();
            }

            services.AddHealthChecks();

            services.AddControllers()
                    .AddNewtonsoftJson(
                        options =>
                        {
                            options.SerializerSettings.Converters.Add(
                                new StringEnumConverter(new CamelCaseNamingStrategy()));
                                
                            options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                        });

            // Register the Swagger generator, defining 1 or more Swagger documents
            services.AddSwaggerDocument(settings =>
            {
                settings.PostProcess = document =>
                {
                    document.Info.Version = "v1";
                    document.Info.Title = "System Events API";
                    document.Info.Description = "REST API for managing system events";
                };
            });
        }
    }
}
