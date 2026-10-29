using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class CargasPlanesAccion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CargaPlanesAccionId",
                table: "PlanesAccion",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CargasPlanesAccion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NombreArchivo = table.Column<string>(type: "text", nullable: false),
                    Hoja = table.Column<string>(type: "text", nullable: true),
                    FilaEncabezados = table.Column<int>(type: "integer", nullable: true),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PlanesCreados = table.Column<int>(type: "integer", nullable: false),
                    AccionesCreadas = table.Column<int>(type: "integer", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CargasPlanesAccion", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanesAccion_CargaPlanesAccionId",
                table: "PlanesAccion",
                column: "CargaPlanesAccionId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlanesAccion_CargasPlanesAccion_CargaPlanesAccionId",
                table: "PlanesAccion",
                column: "CargaPlanesAccionId",
                principalTable: "CargasPlanesAccion",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlanesAccion_CargasPlanesAccion_CargaPlanesAccionId",
                table: "PlanesAccion");

            migrationBuilder.DropTable(
                name: "CargasPlanesAccion");

            migrationBuilder.DropIndex(
                name: "IX_PlanesAccion_CargaPlanesAccionId",
                table: "PlanesAccion");

            migrationBuilder.DropColumn(
                name: "CargaPlanesAccionId",
                table: "PlanesAccion");
        }
    }
}
