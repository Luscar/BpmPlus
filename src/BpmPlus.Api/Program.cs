using System.Data;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using BpmPlus.Abstractions;
using BpmPlus.Api.Infrastructure;
using BpmPlus.Core.Persistance;
using BpmPlus.Persistance.Sqlite;
using BpmPlus.Registration;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(cb =>
{
    var dbPath = builder.Configuration["Sqlite:Path"] ?? "bpmplus.db";

    cb.Register(_ =>
    {
        var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();
        return (IDbConnection)conn;
    }).InstancePerLifetimeScope();

    cb.RegisterModule(new BpmModule(cfg =>
        cfg.UseSqlite().ScanHandlers(typeof(Program).Assembly)));

    // Service de recherche avancée (SQL dynamique, indépendant du moteur BPM)
    cb.Register(ctx =>
        new InstanceSearchService(ctx.Resolve<IDbConnection>(), "BPM"))
      .AsSelf()
      .InstancePerLifetimeScope();
});

builder.Services.AddControllers().AddJsonOptions(opts =>
{
    opts.JsonSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter());
    opts.JsonSerializerOptions.DefaultIgnoreCondition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "BpmPlus Monitor API", Version = "v1" }));

builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var conn    = scope.ServiceProvider.GetRequiredService<IDbConnection>();
    var creator = scope.ServiceProvider.GetRequiredService<SchemaCreator>();
    await creator.CreerToutesLesTablesAsync(conn);

    var bpm = scope.ServiceProvider.GetRequiredService<IServiceBpm>();
    await SeedData.InitialiserAsync(bpm);
}

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

app.Run();
