using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class EquiposOrigen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CargaEquiposId",
                table: "IntervencionesEquipo",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CargaEquiposId",
                table: "Equipos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CargasEquipos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NombreArchivo = table.Column<string>(type: "text", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CargasEquipos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntervencionesEquipo_CargaEquiposId",
                table: "IntervencionesEquipo",
                column: "CargaEquiposId");

            migrationBuilder.CreateIndex(
                name: "IX_Equipos_CargaEquiposId",
                table: "Equipos",
                column: "CargaEquiposId");

            migrationBuilder.AddForeignKey(
                name: "FK_Equipos_CargasEquipos_CargaEquiposId",
                table: "Equipos",
                column: "CargaEquiposId",
                principalTable: "CargasEquipos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_IntervencionesEquipo_CargasEquipos_CargaEquiposId",
                table: "IntervencionesEquipo",
                column: "CargaEquiposId",
                principalTable: "CargasEquipos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Equipos_CargasEquipos_CargaEquiposId",
                table: "Equipos");

            migrationBuilder.DropForeignKey(
                name: "FK_IntervencionesEquipo_CargasEquipos_CargaEquiposId",
                table: "IntervencionesEquipo");

            migrationBuilder.DropTable(
                name: "CargasEquipos");

            migrationBuilder.DropIndex(
                name: "IX_IntervencionesEquipo_CargaEquiposId",
                table: "IntervencionesEquipo");

            migrationBuilder.DropIndex(
                name: "IX_Equipos_CargaEquiposId",
                table: "Equipos");

            migrationBuilder.DropColumn(
                name: "CargaEquiposId",
                table: "IntervencionesEquipo");

            migrationBuilder.DropColumn(
                name: "CargaEquiposId",
                table: "Equipos");
        }
    }
}
