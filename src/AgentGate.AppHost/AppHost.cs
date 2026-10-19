var builder = DistributedApplication.CreateBuilder(args);

// One Postgres server; each service gets its own database when it is added (M2+).
builder.AddPostgres("postgres");

builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin();

// Fixed port: the token issuer URL must be stable for the browser (React OIDC) and for agents.
builder.AddKeycloak("keycloak", port: 8080)
    .WithRealmImport("./Realms");

builder.Build().Run();
