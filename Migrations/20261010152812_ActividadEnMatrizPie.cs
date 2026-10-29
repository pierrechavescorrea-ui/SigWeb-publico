using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class ActividadEnMatrizPie : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnMatrizPie",
                table: "Actividades",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnMatrizPie",
                table: "Actividades");
        }
    }
}
