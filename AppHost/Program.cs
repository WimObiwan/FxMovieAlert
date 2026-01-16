using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Site>("site")
    .WithExternalHttpEndpoints();

builder.Build().Run();
