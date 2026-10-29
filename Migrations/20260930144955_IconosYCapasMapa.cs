using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class IconosYCapasMapa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CapaBaseMapa",
                table: "Configuracion",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ColorPata",
                table: "Configuracion",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ColorTorre",
                table: "Configuracion",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IconoPata",
                table: "Configuracion",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CapaBaseMapa",
                table: "Configuracion");

            migrationBuilder.DropColumn(
                name: "ColorPata",
                table: "Configuracion");

            migrationBuilder.DropColumn(
                name: "ColorTorre",
                table: "Configuracion");

            migrationBuilder.DropColumn(
                name: "IconoPata",
                table: "Configuracion");
        }
    }
}
