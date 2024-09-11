var builder = DistributedApplication.CreateBuilder(args);

// Uncomment if you want to use the Azure Cosmos DB emulator
//var cosmos = builder.AddAzureCosmosDB("cosmos")
//  .AddDatabase("cosmoscopilotdb")
//  .RunAsEmulator();

builder.AddProject<Projects.cosmos_copilot>("webfrontend")
    .WithExternalHttpEndpoints();
    //.WithReference(cosmos);

builder.Build().Run();
