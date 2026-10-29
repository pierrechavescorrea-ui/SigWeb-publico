using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class AgregarUsaTorreYLocalizacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "UsaTorre",
                table: "TiposEnsayo",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEjecucion",
                table: "ResultadosEnsayo",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Localizacion",
                table: "ResultadosEnsayo",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UsaTorre",
                table: "TiposEnsayo");

            migrationBuilder.DropColumn(
                name: "FechaEjecucion",
                table: "ResultadosEnsayo");

            migrationBuilder.DropColumn(
                name: "Localizacion",
                table: "ResultadosEnsayo");
        }
    }
}
