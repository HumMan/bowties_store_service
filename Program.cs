using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using store_service.Helpers;
using store_service.Services;
using store_service.App.Repository;
using RepositoryInternal = store_service.App.Repository.Internal;
using System;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using NLog.Web;

var builder = WebApplication.CreateBuilder(args);
ConfigurationManager configuration = builder.Configuration;

// Adding Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})

// Adding Jwt Bearer
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = false;
    options.TokenValidationParameters = new TokenValidationParameters()
    {
        //RoleClaimType = "Role",
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = configuration["JwtBearer:ValidIssuer"],
        ValidAudience = configuration["JwtBearer:ValidAudience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JwtBearer:JwtSecurityKey"]))
    };
});

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "my api", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
});

builder.Services.AddSingleton<ICartsRepository, RepositoryInternal.CartsRepository>();
builder.Services.AddSingleton<IFilesRepository, RepositoryInternal.FilesRepository>();
builder.Services.AddSingleton<IOrdersRepository, RepositoryInternal.OrdersRepository>();
builder.Services.AddSingleton<IGroupsRepository, RepositoryInternal.GroupsRepository>();
builder.Services.AddSingleton<IPaymentsRepository, RepositoryInternal.PaymentsRepository>();
builder.Services.AddSingleton<IProductsRepository, RepositoryInternal.ProductsRepository>();
builder.Services.AddSingleton<IUsersRepository, RepositoryInternal.UsersRepository>();
builder.Services.AddSingleton<IPasswordResetTicketRepository, RepositoryInternal.PasswordResetTickets>();
builder.Services.AddSingleton<IEmailVerifyTicketRepository, RepositoryInternal.EmailVerifyTickets>();
builder.Services.AddSingleton<IMailer, Mailer>();
builder.Services.AddSingleton<IGlobalParameters, GlobalParameters>();
builder.Services.AddSingleton<ITelegramBot, TelegramBot>();
builder.Services.Configure<FormOptions>(x =>
{
    x.ValueLengthLimit = int.MaxValue;
    x.MultipartBodyLengthLimit = int.MaxValue; // In case of multipart
});

builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
builder.Host.UseNLog();

var app = builder.Build();

app.Urls.Add("http://+:" + Environment.GetEnvironmentVariable("PORT"));

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(x => { x.SwaggerEndpoint("/swagger/v1/swagger.yaml", "Open API"); });
}

app.UseDeveloperExceptionPage();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();