using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using SpendWise.Services;
using SpendWise.Helpers;
using SpendWise.Dialogs;
using SpendWise.Models;
using SpendWise.Bots;
using SpendWise.Data;
using System;

namespace SpendWise
{
    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpClient().AddControllers().AddNewtonsoftJson(options =>
            {
                options.SerializerSettings.MaxDepth = HttpHelper.BotMessageSerializerSettings.MaxDepth;
            });

            services.AddTransient<IUserDataService, UserDataService>();
            services.AddTransient<IGoalService, GoalService>();
            services.AddTransient<IBudgetService, BudgetService>();
            services.AddTransient<IExpenseService, ExpenseService>();
            services.AddTransient<ICategoryService, CategoryService>();


            // Create the Bot Framework Authentication to be used with the Bot Adapter.
            services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();

            // Create the Bot Adapter with error handling enabled.
            services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();

            // Create the storage for User and Conversation state.
            services.AddSingleton<IStorage, MemoryStorage>();

            // Register UserState and ConversationState
            services.AddSingleton<UserState>();
            services.AddSingleton<ConversationState>();


            // Register dialogs
            services.AddSingleton<MainDialog>();
            services.AddSingleton<ParameterCollectionDialog>();



            // Register IStatePropertyAccessor for UserProfile
            services.AddSingleton(provider =>
            {
                var userState = provider.GetService<UserState>();
                return userState.CreateProperty<UserProfile>("UserProfile");
            });

            var connectionString = _configuration.GetConnectionString("SpendWiseDatabase");

            services.AddDbContextPool<SpendWiseContext>(options =>
                options.UseMySql(
                    connectionString,
                    ServerVersion.AutoDetect(connectionString),
                    mySqlOptions =>
                    {
                        mySqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: null);
                    }
                )
            );

            // Register bot
            services.AddTransient<IBot, DialogAndWelcomeBot<MainDialog>>();

            // Register services
            services.AddSingleton<AzureOpenAIService>();

            services.AddSingleton<ExternalServiceHelper>();


            services.AddSingleton<AdaptiveCardHelper>();

            //services.AddSingleton(provider =>
            //{
            //    string storageConnectionString = _configuration.GetConnectionString("TableString");
            //    return new LeadService(storageConnectionString);
            //});
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseDefaultFiles()
               .UseStaticFiles()
               .UseWebSockets()
               .UseRouting()
               .UseAuthorization()
               .UseEndpoints(endpoints =>
               {
                   endpoints.MapControllers();
               });

            // Uncomment this line if you want to enforce HTTPS
            // app.UseHttpsRedirection();
        }
    }
}
