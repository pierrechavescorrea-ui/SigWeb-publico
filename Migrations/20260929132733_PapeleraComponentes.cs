using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class PapeleraComponentes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Eliminado",
                table: "Componentes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEliminacion",
                table: "Componentes",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Eliminado",
                table: "Componentes");

            migrationBuilder.DropColumn(
                name: "FechaEliminacion",
                table: "Componentes");
        }
    }
}
