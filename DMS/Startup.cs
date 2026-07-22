using DMS.Hubs;
using DMS.Models;
using DMS.Services;
using Hangfire;
using Hangfire.SqlServer;
using MDC.Models;
using MDC.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace DMS
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddScoped<PrintingService>();
            services.AddControllersWithViews(options => options.Filters.Add<DynamicTitleFilter>());
            services.AddMemoryCache();
            services.AddSession();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddControllers().AddJsonOptions(opts => opts.JsonSerializerOptions.PropertyNamingPolicy = null);

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration.GetSection("AppSettings:Token").Value)),
                        ValidateIssuer = false,
                        ValidateAudience = false
                    };
                }
                );

            services.AddOptions();
            services.Configure<AWSConfiguration>(Configuration.GetSection("AWSConfiguration"));
            services.Configure<EmailConfiguration>(Configuration.GetSection("EmailConfiguration"));

            var connectionString = Configuration.GetConnectionString("defaultConnection");
            services.AddHangfire(configuration => configuration
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
                {
                    CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                    SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                    QueuePollInterval = TimeSpan.Zero,
                    UseRecommendedIsolationLevel = true,
                    DisableGlobalLocks = true
                }));
            services.AddHangfireServer();
            services.AddSignalR();

            services.AddDbContext<DBContext>(options => options.UseSqlServer(Configuration.GetConnectionString("defaultConnection")));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            //var policyCollection = new HeaderPolicyCollection()
            //        .AddDefaultSecurityHeaders()
            //        .AddStrictTransportSecurity(maxAgeInSeconds: 31536000, includeSubdomains: false, preload: false)
            //        .AddContentSecurityPolicy(builder =>
            //        {
            //            builder.AddUpgradeInsecureRequests();
            //            builder.AddStyleSrc()
            //                     .Self()
            //                     .UnsafeEval()
            //                     .UnsafeInline()
            //                     .From("https://fonts.googleapis.com");
            //            builder.AddScriptSrc()
            //                     .Self()
            //                     .UnsafeEval()
            //                     .UnsafeInline();
            //        });

            //app.UseSecurityHeaders(policyCollection);

            app.Use(async (ctx, next) =>
            {
                await next();

                if (ctx.Response.StatusCode == 404 && !ctx.Response.HasStarted)
                {
                    ctx.Request.Path = "/Home/Error";
                    await next();
                }
            });

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseSession();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseHangfireDashboard("/HangfireDashboard", new DashboardOptions
            {
                Authorization = new[] { new HangfireAuthorization() }
            });

            RecurringJob.AddOrUpdate<DocumentReviewReminderService>(
                "document-review-reminder",
                x => x.SendRemindersAsync(),
                Cron.Daily);

            RecurringJob.AddOrUpdate<ExternalDocumentReviewReminderService>(
                "external-document-review-reminder",
                x => x.SendRemindersAsync(),
                Cron.Daily);

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<NotificationsHub>("/notificationsHub");
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Auth}/{action=Login}/{id?}");
            });
        }
    }
}
