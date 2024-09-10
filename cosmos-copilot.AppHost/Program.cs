var builder = DistributedApplication.CreateBuilder(args);

//var cosmos = builder.AddAzureCosmosDB("cosmos")
//  .AddDatabase("cosmoscopilotdb")
//  .RunAsEmulator();

builder.AddProject<Projects.cosmos_copilot>("webfrontend")
    .WithExternalHttpEndpoints();
    //.WithReference(cosmos);

builder.Build().Run();
