var builder = DistributedApplication.CreateBuilder(args);

// One Postgres server; each service gets its own database.
var postgres = builder.AddPostgres("postgres");
var registryDb = postgres.AddDatabase("registrydb");
var reviewDb = postgres.AddDatabase("reviewdb");

var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin();

// Fixed port: the token issuer URL must be stable for the browser (React OIDC) and for agents.
var keycloak = builder.AddKeycloak("keycloak", port: 8080)
    .WithRealmImport("./Realms");

var demoMcpServer = builder.AddProject<Projects.DemoMcpServer>("demo-mcp-server");

// Registry's ports are explicit: REST is HTTP/1.1, GetServerSnapshot needs HTTP/2, and a cleartext Kestrel endpoint
// speaks only one of them. Registry binds GRPC_PORT as HTTP/2-only (Program.cs).
var registry = builder.AddProject<Projects.Registry_API>("registry", options => options.ExcludeLaunchProfile = true)
    .WithHttpEndpoint(name: "http", env: "HTTP_PORT")
    .WithHttpEndpoint(name: "grpc", env: "GRPC_PORT")
    .WithReference(registryDb).WaitFor(registryDb)
    .WithReference(rabbitmq).WaitFor(rabbitmq)
    .WithReference(keycloak).WaitFor(keycloak);

// Secrets below match Realms/agentgate-realm.json; dev only.
var review = builder.AddProject<Projects.Review_API>("review")
    .WithReference(reviewDb).WaitFor(reviewDb)
    .WithReference(rabbitmq).WaitFor(rabbitmq)
    .WithReference(keycloak).WaitFor(keycloak)
    .WithReference(registry) // GetServerSnapshot, resolved as http://_grpc.registry
    .WithEnvironment("ServiceIdentityOptions__ClientSecret", "dev-only-O1XvrDrtWdbpmlYYsQUwbZzA");

// Knows no MCP servers or grants at startup: Registry's and Review's events tell it what exists and who may call what.
var gateway = builder.AddProject<Projects.Gateway_API>("gateway")
    .WithReference(rabbitmq).WaitFor(rabbitmq)
    .WithReference(keycloak).WaitFor(keycloak);

builder.AddProject<Projects.DemoAgent>("demo-agent")
    .WithReference(gateway).WaitFor(gateway)
    .WithReference(registry).WaitFor(registry)
    .WithReference(review).WaitFor(review)
    .WithReference(demoMcpServer).WaitFor(demoMcpServer)
    .WithReference(keycloak)
    .WithEnvironment("DemoAgent__ClientSecret", "dev-only-rkU5VZ5fq7QWDiVRKw0q2Bf2")
    .WithEnvironment("DemoAgent__AdminClientSecret", "dev-only-Tq8mZ2cW4nLx7bVd3sKf9hRp")
    .WithEnvironment("DemoAgent__Reviewer1ClientSecret", "dev-only-WNa39VaVAfaGDchHBojPSTG8")
    .WithEnvironment("DemoAgent__Reviewer2ClientSecret", "dev-only-gBNYzPIAYcWyB9rNjCxb5b6C")
    .WithExplicitStart(); // run on demand from the dashboard

builder.Build().Run();
