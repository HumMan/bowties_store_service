using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using store_service.App.Repository;
using Swashbuckle.AspNetCore.Swagger;
using RepositoryInternal = store_service.App.Repository.Internal;
using System.IO;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using YandexMoney.Checkout.Managers;
using store_service.Helpers;
using store_service.Services;

namespace store_service {
    public class Startup {
        public Startup (IHostingEnvironment env, IConfiguration configuration) {
            Configuration = configuration;
            Environment = env;
        }

        public IConfiguration Configuration { get; }
        public IHostingEnvironment Environment { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices (IServiceCollection services) {
            if (Environment.IsDevelopment ()) {
                services.AddSwaggerGen (c => {
                    c.SwaggerDoc ("v1", new Info { Title = "my api", Version = "v1" });
                    var security = new Dictionary<string, IEnumerable<string>> { { "Bearer", new string[] { } }
                        };
                    c.AddSecurityDefinition ("Bearer", new ApiKeyScheme {
                        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                            Name = "Authorization",
                            In = "header",
                            Type = "apiKey"
                    });
                    c.AddSecurityRequirement (security);
                });
            }

            services.AddAuthentication (JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer (options => {
                    options.TokenValidationParameters = new TokenValidationParameters {
                    RoleClaimType = "Role",
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = Configuration["JwtBearer:ValidIssuer"],
                    ValidAudience = Configuration["JwtBearer:ValidAudience"],
                    IssuerSigningKey = new SymmetricSecurityKey (Encoding.UTF8.GetBytes (Configuration["JwtBearer:JwtSecurityKey"]))
                    };
                });

            services.AddSingleton<ICartsRepository, RepositoryInternal.CartsRepository> ();
            services.AddSingleton<IFilesRepository, RepositoryInternal.FilesRepository> ();
            services.AddSingleton<IOrdersRepository, RepositoryInternal.OrdersRepository> ();
            services.AddSingleton<IGroupsRepository, RepositoryInternal.GroupsRepository> ();
            services.AddSingleton<IPaymentsRepository, RepositoryInternal.PaymentsRepository> ();
            services.AddSingleton<IProductsRepository, RepositoryInternal.ProductsRepository> ();
            services.AddSingleton<IUsersRepository, RepositoryInternal.UsersRepository> ();
            services.AddSingleton<IPasswordResetTicketRepository, RepositoryInternal.PasswordResetTickets>();
            services.AddSingleton<IEmailVerifyTicketRepository, RepositoryInternal.EmailVerifyTickets>();
            services.AddSingleton<IYandexMoneyCheckoutPayment, YandexMoneyCheckoutPayment> ();
            services.AddSingleton<IMailer, Mailer>();
            services.AddSingleton<IGlobalParameters, GlobalParameters>();
            services.AddSingleton<ITelegramBot, TelegramBot>();

            services.AddMvc ();
            services.Configure<FormOptions> (x => {
                x.ValueLengthLimit = int.MaxValue;
                x.MultipartBodyLengthLimit = int.MaxValue; // In case of multipart
            });

            services.AddHostedService<TimedHostedService> ();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure (IApplicationBuilder app, IHostingEnvironment env) {
            if (env.IsDevelopment ()) {
                app.UseDefaultFiles ();
                app.UseStaticFiles ();
            }

            app.UseAuthentication ();

            if (env.IsDevelopment ()) {
                app.UseDeveloperExceptionPage ();
            }

            if (env.IsDevelopment ()) {
                app.UseMvc (routes => {
                    routes.MapRoute (
                        name: "default",
                        template: "{controller=Home}/{action=Index}/{id?}");
                });

                // ## this serves my index.html from the wwwroot folder when 
                // ## a route not containing a file extension is not handled by MVC.  
                // ## If the route contains a ".", a 404 will be returned instead.
                app.MapWhen (context =>
                    !Path.HasExtension (context.Request.Path.Value) && !context.Request.Path.Value.StartsWith ("/swagger"),
                    branch => {
                        branch.Use ((context, next) => {
                            context.Request.Path = new PathString ("/index.html");
                            Console.WriteLine ("Path changed to:" + context.Request.Path.Value);
                            return next ();
                        });

                        branch.UseStaticFiles ();
                    });

                app.UseSwagger ();
                app.UseSwaggerUI (c => {
                    c.SwaggerEndpoint ("/swagger/v1/swagger.json", "my api v1");
                });

            } else {
                app.UseMvc ();
            }
        }
    }
}