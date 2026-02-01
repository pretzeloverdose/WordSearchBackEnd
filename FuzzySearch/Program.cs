// Program.cs
using Amazon.Extensions.NETCore.Setup; // Add this using directive at the top of the file
using Amazon.S3;
using FuzzySearch.Data;
using FuzzySearch.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer;
using Microsoft.OpenApi.Models;
using Npgsql.EntityFrameworkCore.PostgreSQL; // Add this using directive at the top of the file
using System;

var builder = WebApplication.CreateBuilder(args);

// Register your database service here
builder.Services.AddScoped<IDatabaseService, DatabaseService>();

// Get the PostgreSQL connection string from configuration
string connectionString = builder.Configuration.GetConnectionString("PostgreSQL");

// Example: Register your DbContext with the connection string
builder.Services.AddDbContext<YourDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => { c.AddServer(new OpenApiServer { Url = "/wordsearch" }); });
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
builder.Services.AddAWSService<IAmazonS3>();
builder.Services.AddSingleton<S3WordService>();

var app = builder.Build();
// Tell ASP.NET Core it's running under /wordsearch
app.UsePathBase("/wordsearch");
// Configure the HTTP request pipeline
// if (app.Environment.IsDevelopment())
// {
app.UseSwagger();
    app.UseSwaggerUI();
// }

// Then use:
app.UseCors("AllowAll");

// commented out for lightsail deployment nginx
// app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();