using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class CargasMaterialesYEquipos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CargaMaterialesId",
                table: "Materiales",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Eliminado",
                table: "IntervencionesEquipo",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEliminacion",
                table: "IntervencionesEquipo",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEliminacion",
                table: "Equipos",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Eliminado",
                table: "CargasEquipos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEliminacion",
                table: "CargasEquipos",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FilaEncabezados",
                table: "CargasEquipos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Hoja",
                table: "CargasEquipos",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CargasMateriales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NombreArchivo = table.Column<string>(type: "text", nullable: false),
                    Hoja = table.Column<string>(type: "text", nullable: true),
                    FilaEncabezados = table.Column<int>(type: "integer", nullable: true),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FilasCreadas = table.Column<int>(type: "integer", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CargasMateriales", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Materiales_CargaMaterialesId",
                table: "Materiales",
                column: "CargaMaterialesId");

            migrationBuilder.AddForeignKey(
                name: "FK_Materiales_CargasMateriales_CargaMaterialesId",
                table: "Materiales",
                column: "CargaMaterialesId",
                principalTable: "CargasMateriales",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Materiales_CargasMateriales_CargaMaterialesId",
                table: "Materiales");

            migrationBuilder.DropTable(
                name: "CargasMateriales");

            migrationBuilder.DropIndex(
                name: "IX_Materiales_CargaMaterialesId",
                table: "Materiales");

            migrationBuilder.DropColumn(
                name: "CargaMaterialesId",
                table: "Materiales");

            migrationBuilder.DropColumn(
                name: "Eliminado",
                table: "IntervencionesEquipo");

            migrationBuilder.DropColumn(
                name: "FechaEliminacion",
                table: "IntervencionesEquipo");

            migrationBuilder.DropColumn(
                name: "FechaEliminacion",
                table: "Equipos");

            migrationBuilder.DropColumn(
                name: "Eliminado",
                table: "CargasEquipos");

            migrationBuilder.DropColumn(
                name: "FechaEliminacion",
                table: "CargasEquipos");

            migrationBuilder.DropColumn(
                name: "FilaEncabezados",
                table: "CargasEquipos");

            migrationBuilder.DropColumn(
                name: "Hoja",
                table: "CargasEquipos");
        }
    }
}
