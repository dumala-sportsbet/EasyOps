using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyOps.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePayloadToBinary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte[]>(
                name: "Payload",
                table: "ReplayGameEvents",
                type: "BLOB",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Payload",
                table: "ReplayGameEvents",
                type: "text",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "BLOB");
        }
    }
}
