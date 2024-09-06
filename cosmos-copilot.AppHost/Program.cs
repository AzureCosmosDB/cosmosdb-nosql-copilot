var builder = DistributedApplication.CreateBuilder(args);

//var tododb = builder.AddAzureCosmosDB("cosmos")
//    .AddDatabase("cosmoscopilotdb");

builder.AddProject<Projects.cosmos_copilot>("webfrontend")
    .WithExternalHttpEndpoints();
    //.WithReference(tododb);

builder.Build().Run();
