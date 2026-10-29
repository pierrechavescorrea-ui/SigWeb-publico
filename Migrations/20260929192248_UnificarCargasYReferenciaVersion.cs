using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class UnificarCargasYReferenciaVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Version",
                table: "Capas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Origen",
                table: "CamposExtraTorre",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Origen",
                table: "CamposExtraComponente",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "Capas");

            migrationBuilder.DropColumn(
                name: "Origen",
                table: "CamposExtraTorre");

            migrationBuilder.DropColumn(
                name: "Origen",
                table: "CamposExtraComponente");
        }
    }
}
