using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class AgegarPapeleraMateriales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Eliminado",
                table: "Materiales",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEliminacion",
                table: "Materiales",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Eliminado",
                table: "Materiales");

            migrationBuilder.DropColumn(
                name: "FechaEliminacion",
                table: "Materiales");
        }
    }
}
