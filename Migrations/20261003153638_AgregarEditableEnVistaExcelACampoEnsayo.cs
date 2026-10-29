using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class AgregarEditableEnVistaExcelACampoEnsayo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EditableEnVistaExcel",
                table: "CamposEnsayo",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EditableEnVistaExcel",
                table: "CamposEnsayo");
        }
    }
}
