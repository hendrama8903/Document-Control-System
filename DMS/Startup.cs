using DevExpress.AspNetCore;
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
            services.AddDevExpressControls();
            services.AddScoped<PrintingService>();
            services.AddControllersWithViews(options => options.Filters.Add<DynamicTitleFilter>());
            services.AddMemoryCache();
            // Hanya CSS/JS/SVG statis. HTML dan JSON tidak dikompres karena bisa berisi data
            // sesi di atas HTTPS (BREACH).
            services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.MimeTypes = new[] { "text/css", "application/javascript", "text/javascript", "image/svg+xml" };
                options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
                options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
            });
            services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Fastest);
            services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Fastest);
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
            app.UseResponseCompression();

            // File di /Upload (dokumen, PDF pengesahan, tanda tangan) dan /document (template)
            // dulu bisa diunduh tanpa login lewat URL langsung. Session dibaca sebelum file
            // statis dilayani, lalu hak lihatnya dicek dengan aturan yang sama seperti
            // ViewAttachment & pencarian (Services/DocumentAccessService, 2026-11-10).
            app.UseSession();
            app.Use(async (ctx, next) =>
            {
                string requestPath = ctx.Request.Path.Value ?? "";
                if (requestPath.StartsWith("/Upload/", StringComparison.OrdinalIgnoreCase)
                    || requestPath.StartsWith("/document/", StringComparison.OrdinalIgnoreCase))
                {
                    var db = ctx.RequestServices.GetRequiredService<DBContext>();
                    var access = DocumentAccessService.CheckFile(ctx, db, requestPath);
                    if (access != DocumentAccessService.Access.Allowed)
                    {
                        ctx.Response.StatusCode = access == DocumentAccessService.Access.NotLoggedIn ? 401 : 403;
                        ctx.Response.Headers.CacheControl = "no-store";
                        return;
                    }
                    ctx.Response.Headers.CacheControl = "private, no-store";
                }
                await next();
            });

            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = ctx =>
                {
                    // File aplikasi saja, bukan /Upload dan /Document yang berisi dokumen user.
                    string path = ctx.Context.Request.Path.Value ?? "";
                    bool isAsset = new[] { "/lib/", "/js/", "/css/", "/dist/", "/fonts/", "/images/" }
                        .Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));
                    if (!isAsset)
                    {
                        return;
                    }
                    // Link dengan ?v= berubah otomatis saat isi file berubah (asp-append-version).
                    ctx.Context.Response.Headers.CacheControl = ctx.Context.Request.Query.ContainsKey("v")
                        ? "public, max-age=31536000, immutable"
                        : "public, max-age=86400";
                }
            });
            app.UseDevExpressControls();
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

            // Full-text search: ekstrak teks isi dokumen baru / yang berganti file (2026-11-06)
            RecurringJob.AddOrUpdate<DMS.Services.DocumentContentIndexer>(
                "document-content-index",
                x => x.IndexPending(),
                "*/5 * * * *");

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
