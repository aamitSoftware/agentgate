using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Registry.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgentsAndReviewDecisions : Migration
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
                    CreatedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LastModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agents", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ServerStatusTransitions",
                columns: new[] { "ActionType", "CurrentStatus", "DestinationStatus" },
                values: new object[,]
                {
                    { "Reject", "Quarantined", "Rejected" },
                    { "Reject", "UnderReview", "Rejected" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Agents_ClientId",
                table: "Agents",
                column: "ClientId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Agents");

            migrationBuilder.DeleteData(
                table: "ServerStatusTransitions",
                keyColumns: new[] { "ActionType", "CurrentStatus" },
                keyValues: new object[] { "Reject", "Quarantined" });

            migrationBuilder.DeleteData(
                table: "ServerStatusTransitions",
                keyColumns: new[] { "ActionType", "CurrentStatus" },
                keyValues: new object[] { "Reject", "UnderReview" });
        }
    }
}
