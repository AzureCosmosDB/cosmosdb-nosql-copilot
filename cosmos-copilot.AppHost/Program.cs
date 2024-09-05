var builder = DistributedApplication.CreateBuilder(args);

var tododb = builder.AddAzureCosmosDB("cosmos")// .AddDatabase("cosmoscopilotdb")
    .RunAsEmulator();

builder.AddProject<Projects.cosmos_copilot>("webfrontend")
    .WithExternalHttpEndpoints();

builder.Build().Run();
