using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Hub.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Agents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ClientId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Owner = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    RegisteredOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Grants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentClientId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Tools = table.Column<List<string>>(type: "text[]", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StatusAsOf = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Grants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboxState",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LockId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    Received = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceiveCount = table.Column<int>(type: "integer", nullable: false),
                    ExpirationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Consumed = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Delivered = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxState", x => x.Id);
                    table.UniqueConstraint("AK_InboxState_MessageId_ConsumerId", x => new { x.MessageId, x.ConsumerId });
                });

            migrationBuilder.CreateTable(
                name: "OutboxState",
                columns: table => new
                {
                    OutboxId = table.Column<Guid>(type: "uuid", nullable: false),
                    LockId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Delivered = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxState", x => x.OutboxId);
                });

            migrationBuilder.CreateTable(
                name: "Servers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StatusAsOf = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PinnedHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PinnedToolsJson = table.Column<string>(type: "text", nullable: true),
                    PendingHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    OpenReviewId = table.Column<Guid>(type: "uuid", nullable: true),
                    OpenReviewRisk = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    LastReviewRisk = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    FirstSeenOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Servers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Timeline",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ActorId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: true),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    GrantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Timeline", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ToolCalls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AgentClientId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ServerName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Method = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ToolName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Decision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    TraceId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolCalls", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                columns: table => new
                {
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EnqueueTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Headers = table.Column<string>(type: "text", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    InboxMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    InboxConsumerId = table.Column<Guid>(type: "uuid", nullable: true),
                    OutboxId = table.Column<Guid>(type: "uuid", nullable: true),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    MessageType = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                    InitiatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DestinationAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ResponseAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FaultAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ExpirationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessage", x => x.SequenceNumber);
                    table.ForeignKey(
                        name: "FK_OutboxMessage_InboxState_InboxMessageId_InboxConsumerId",
                        columns: x => new { x.InboxMessageId, x.InboxConsumerId },
                        principalTable: "InboxState",
                        principalColumns: new[] { "MessageId", "ConsumerId" });
                    table.ForeignKey(
                        name: "FK_OutboxMessage_OutboxState_OutboxId",
                        column: x => x.OutboxId,
                        principalTable: "OutboxState",
                        principalColumn: "OutboxId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Agents_ClientId",
                table: "Agents",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Grants_AgentId",
                table: "Grants",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_Grants_ServerId",
                table: "Grants",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_InboxState_Delivered",
                table: "InboxState",
                column: "Delivered");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_EnqueueTime",
                table: "OutboxMessage",
                column: "EnqueueTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_ExpirationTime",
                table: "OutboxMessage",
                column: "ExpirationTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber",
                table: "OutboxMessage",
                columns: new[] { "InboxMessageId", "InboxConsumerId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_OutboxId_SequenceNumber",
                table: "OutboxMessage",
                columns: new[] { "OutboxId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxState_Created",
                table: "OutboxState",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "IX_Servers_Name",
                table: "Servers",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Timeline_AgentId",
                table: "Timeline",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_Timeline_OccurredOn",
                table: "Timeline",
                column: "OccurredOn");

            migrationBuilder.CreateIndex(
                name: "IX_Timeline_ServerId",
                table: "Timeline",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_ToolCalls_AgentClientId_OccurredOn",
                table: "ToolCalls",
                columns: new[] { "AgentClientId", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_ToolCalls_OccurredOn",
                table: "ToolCalls",
                column: "OccurredOn");

            migrationBuilder.CreateIndex(
                name: "IX_ToolCalls_ServerName_OccurredOn",
                table: "ToolCalls",
                columns: new[] { "ServerName", "OccurredOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Agents");

            migrationBuilder.DropTable(
                name: "Grants");

            migrationBuilder.DropTable(
                name: "OutboxMessage");

            migrationBuilder.DropTable(
                name: "Servers");

            migrationBuilder.DropTable(
                name: "Timeline");

            migrationBuilder.DropTable(
                name: "ToolCalls");

            migrationBuilder.DropTable(
                name: "InboxState");

            migrationBuilder.DropTable(
                name: "OutboxState");
        }
    }
}
