using Lantern.Base;
using Lantern.Functions;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services.AddLanternBase(builder.Configuration).AddLanternFunctions();

await builder.Build().RunAsync();
