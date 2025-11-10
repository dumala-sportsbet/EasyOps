using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyOps.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Environments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EnvironmentType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AwsProfile = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AccountId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SamlRole = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Environments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Monorepos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    JobPath = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Monorepos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReplayGames",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    GameName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FetchedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    TotalEvents = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReplayGames", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clusters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ClusterName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    AwsProfile = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AccountId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    EnvironmentId = table.Column<int>(type: "INTEGER", nullable: false),
                    MonorepoId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clusters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clusters_Environments_EnvironmentId",
                        column: x => x.EnvironmentId,
                        principalTable: "Environments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Clusters_Monorepos_MonorepoId",
                        column: x => x.MonorepoId,
                        principalTable: "Monorepos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ReplayGameEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReplayGameId = table.Column<int>(type: "INTEGER", nullable: false),
                    EventIdentifier = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    PayloadType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReplayGameEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReplayGameEvents_ReplayGames_ReplayGameId",
                        column: x => x.ReplayGameId,
                        principalTable: "ReplayGames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clusters_EnvironmentId",
                table: "Clusters",
                column: "EnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Clusters_MonorepoId",
                table: "Clusters",
                column: "MonorepoId");

            migrationBuilder.CreateIndex(
                name: "IX_ReplayGameEvents_ReplayGameId",
                table: "ReplayGameEvents",
                column: "ReplayGameId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Clusters");

            migrationBuilder.DropTable(
                name: "ReplayGameEvents");

            migrationBuilder.DropTable(
                name: "Environments");

            migrationBuilder.DropTable(
                name: "Monorepos");

            migrationBuilder.DropTable(
                name: "ReplayGames");
        }
    }
}
