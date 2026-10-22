var builder = DistributedApplication.CreateBuilder(args);

// One Postgres server; each service gets its own database.
var postgres = builder.AddPostgres("postgres");
var registryDb = postgres.AddDatabase("registrydb");
var reviewDb = postgres.AddDatabase("reviewdb");
var hubDb = postgres.AddDatabase("hubdb");

var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin();

// Fixed port: the token issuer URL must be stable for the browser (React OIDC) and for agents. Plain HTTP: Aspire
// would otherwise switch Keycloak to HTTPS whenever a .NET dev certificate exists, and the issuer would depend on the machine.
// 8080 by default; where it is taken or reserved (Hyper-V and WSL reserve port ranges on Windows), choose another:
// dotnet run --project src/AgentGate.AppHost -- --KeycloakPort=18080
var keycloakPort = int.TryParse(builder.Configuration["KeycloakPort"], out var port) ? port : 8080;
#pragma warning disable ASPIRECERTIFICATES001 // evaluation API; the only switch for this in Aspire 13
var keycloak = builder.AddKeycloak("keycloak", port: keycloakPort)
    .WithoutHttpsCertificate()
    .WithRealmImport("./Realms");
#pragma warning restore ASPIRECERTIFICATES001

// Endpoints are declared here, not taken from launch profiles (which are machine-local and not in the repo), so a
// fresh clone wires every service the same way.
var demoMcpServer = builder.AddProject<Projects.DemoMcpServer>("demo-mcp-server", options => options.ExcludeLaunchProfile = true)
    .WithHttpEndpoint();

// Registry has two ports: REST is HTTP/1.1, GetServerSnapshot needs HTTP/2, and a cleartext Kestrel endpoint
// speaks only one of them. Registry binds GRPC_PORT as HTTP/2-only (Program.cs).
var registry = builder.AddProject<Projects.Registry_API>("registry", options => options.ExcludeLaunchProfile = true)
    .WithHttpEndpoint(name: "http", env: "HTTP_PORT")
    .WithHttpEndpoint(name: "grpc", env: "GRPC_PORT")
    .WithReference(registryDb).WaitFor(registryDb)
    .WithReference(rabbitmq).WaitFor(rabbitmq)
    .WithReference(keycloak).WaitFor(keycloak);

// Secrets below match Realms/agentgate-realm.json; dev only.
var review = builder.AddProject<Projects.Review_API>("review", options => options.ExcludeLaunchProfile = true)
    .WithHttpEndpoint()
    .WithReference(reviewDb).WaitFor(reviewDb)
    .WithReference(rabbitmq).WaitFor(rabbitmq)
    .WithReference(keycloak).WaitFor(keycloak)
    .WithReference(registry) // GetServerSnapshot, resolved as http://_grpc.registry
    .WithEnvironment("ServiceIdentityOptions__ClientSecret", "dev-only-O1XvrDrtWdbpmlYYsQUwbZzA");

// Optional: with a key, Review adds Claude's reading of each manifest to the heuristic scan (ADR 0006).
// Set it once with: dotnet user-secrets set Parameters:anthropic-api-key <key> --project src/AgentGate.AppHost
if (builder.Configuration["Parameters:anthropic-api-key"] is { Length: > 0 })
    review.WithEnvironment("ClaudeRiskScannerOptions__ApiKey", builder.AddParameter("anthropic-api-key", secret: true));

// Reads the servers and grants it enforces from Registry and Review at startup, then follows their events.
var gateway = builder.AddProject<Projects.Gateway_API>("gateway", options => options.ExcludeLaunchProfile = true)
    .WithHttpEndpoint()
    .WithReference(rabbitmq).WaitFor(rabbitmq)
    .WithReference(keycloak).WaitFor(keycloak)
    .WithReference(registry)
    .WithReference(review)
    .WithEnvironment("ServiceIdentityOptions__ClientSecret", "dev-only-erdcHJamzzttyhE5bu7BssPf");

// Read model built only from events: knows nothing at startup, catches up from the queues.
var hub = builder.AddProject<Projects.Hub_API>("hub", options => options.ExcludeLaunchProfile = true)
    .WithHttpEndpoint()
    .WithReference(hubDb).WaitFor(hubDb)
    .WithReference(rabbitmq).WaitFor(rabbitmq)
    .WithReference(keycloak).WaitFor(keycloak);

// The console: Vite's dev server proxies /api/{registry,review,hub} to these references, so the browser sees one
// origin. Fixed port: the Keycloak client's redirect URI names it. Humans sign in as alice/bob (reviewers),
// carol (admin) or dave (auditor); passwords are '{name}-dev' (Realms/agentgate-realm.json, dev only).
builder.AddViteApp("console", "../../web")
    .WithNpm(install: true) // runs npm install before the dev server starts
    .WithEndpoint("http", endpoint => endpoint.Port = 5173)
    .WithReference(registry)
    .WithReference(review)
    .WithReference(hub)
    .WithReference(keycloak)
    .WaitFor(hub)
    .WaitFor(keycloak);

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

// Without launch profiles nothing tells a service its environment, and it would start as Production (no migrations,
// HTTPS-only token metadata). Every project runs in the AppHost's environment instead.
foreach (var project in builder.Resources.OfType<ProjectResource>())
    builder.CreateResourceBuilder(project).WithEnvironment("DOTNET_ENVIRONMENT", builder.Environment.EnvironmentName);

builder.Build().Run();
