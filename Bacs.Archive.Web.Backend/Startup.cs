using System.Security.Claims;
using System.Text;
using Bacs.Archive.Client.CSharp;
using Bacs.Archive.TestFetcher;
using Bacs.Archive.Web.Backend.BackgroundServices;
using Bacs.Archive.Web.Backend.Contexts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Bacs.Archive.Web.Backend
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        private IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors();
            services.AddAuthorization(auth =>
            {
                var authorizationPolicy = new AuthorizationPolicyBuilder()
                    .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                    .RequireClaim(ClaimTypes.Name)
                    .Build();
                auth.DefaultPolicy = authorizationPolicy;
            });
            
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = Configuration["SecurityDomain"],
                        ValidAudience = Configuration["SecurityDomain"],
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(Configuration["SecurityKey"])),
                    };
                });
            services.AddMvc();
            
            services.AddSingleton<ITestsFetcher, TestsFetcher>();

            services.AddSingleton(ArchiveClientFactory.CreateFromFiles(
                Configuration["ArchiveHost"],
                int.Parse(Configuration["ArchivePort"]),
                Configuration["ArchiveClientCertificatePath"],
                Configuration["ArchiveClienKeyPath"],
                Configuration["ArchiveCACertificatePath"]
            ));

            services.AddDbContext<UsersDbContext>(options => options.UseInMemoryDatabase("users"));
            services.AddDbContext<ProblemsDbContext>(options => options.UseInMemoryDatabase("problems"));
            services.AddSingleton<IHostedService, ProblemsSynchronizerService>();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseCors(builder =>
            {
                builder.AllowAnyOrigin();
                builder.AllowAnyMethod();
                builder.AllowAnyHeader();
            });
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseMvc();
        }
    }
}