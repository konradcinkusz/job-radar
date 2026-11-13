// Composition root for development only (P1, ADR-0002). Production is .github/workflows/daily.yml.
var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.JobRadar_Cli>("jobradar")
    .WithArgs("doctor") // T-01.22 changes this to: run-all --profile fake
    .WithEnvironment("JOBRADAR_PROFILE", "fake");

builder.Build().Run();
