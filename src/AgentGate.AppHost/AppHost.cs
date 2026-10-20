var builder = DistributedApplication.CreateBuilder(args);

// One Postgres server; each service gets its own database.
var postgres = builder.AddPostgres("postgres");
var registryDb = postgres.AddDatabase("registrydb");

var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin();

// Fixed port: the token issuer URL must be stable for the browser (React OIDC) and for agents.
var keycloak = builder.AddKeycloak("keycloak", port: 8080)
    .WithRealmImport("./Realms");

var demoMcpServer = builder.AddProject<Projects.DemoMcpServer>("demo-mcp-server");

var registry = builder.AddProject<Projects.Registry_API>("registry")
    .WithReference(registryDb).WaitFor(registryDb)
    .WithReference(rabbitmq).WaitFor(rabbitmq)
    .WithReference(keycloak).WaitFor(keycloak);

// Knows no MCP servers at startup: Registry's ServerApproved events tell it what exists.
var gateway = builder.AddProject<Projects.Gateway_API>("gateway")
    .WithReference(rabbitmq).WaitFor(rabbitmq)
    .WithReference(keycloak).WaitFor(keycloak);

// Secrets below match Realms/agentgate-realm.json; dev only.
builder.AddProject<Projects.DemoAgent>("demo-agent")
    .WithReference(gateway).WaitFor(gateway)
    .WithReference(registry).WaitFor(registry)
    .WithReference(demoMcpServer).WaitFor(demoMcpServer)
    .WithReference(keycloak)
    .WithEnvironment("DemoAgent__ClientSecret", "dev-only-rkU5VZ5fq7QWDiVRKw0q2Bf2")
    .WithEnvironment("DemoAgent__AdminClientSecret", "dev-only-Tq8mZ2cW4nLx7bVd3sKf9hRp")
    .WithExplicitStart(); // run on demand from the dashboard

builder.Build().Run();
