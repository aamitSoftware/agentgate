var builder = DistributedApplication.CreateBuilder(args);

// One Postgres server; each service gets its own database when it is added (M2+).
builder.AddPostgres("postgres");

builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin();

// Fixed port: the token issuer URL must be stable for the browser (React OIDC) and for agents.
var keycloak = builder.AddKeycloak("keycloak", port: 8080)
    .WithRealmImport("./Realms");

var demoMcpServer = builder.AddProject<Projects.DemoMcpServer>("demo-mcp-server");

// M1: one statically registered upstream; M2 replaces this with servers registered in Registry
var gateway = builder.AddProject<Projects.Gateway_API>("gateway")
    .WithReference(keycloak)
    .WaitFor(keycloak)
    .WithEnvironment("GatewayOptions__Servers__crm__Url", ReferenceExpression.Create($"{demoMcpServer.GetEndpoint("http")}/mcp"));

builder.AddProject<Projects.DemoAgent>("demo-agent")
    .WithReference(gateway)
    .WithReference(keycloak)
    .WaitFor(gateway)
    .WaitFor(demoMcpServer)
    .WithEnvironment("DemoAgent__ClientSecret", "dev-only-rkU5VZ5fq7QWDiVRKw0q2Bf2") // matches Realms/agentgate-realm.json; dev only
    .WithExplicitStart(); // run on demand from the dashboard

builder.Build().Run();
