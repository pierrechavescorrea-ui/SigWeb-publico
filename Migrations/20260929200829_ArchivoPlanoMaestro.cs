using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class ArchivoPlanoMaestro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FechaCargaArchivo",
                table: "PlanosMaestro");

            migrationBuilder.DropColumn(
                name: "NombreArchivoOriginal",
                table: "PlanosMaestro");

            migrationBuilder.DropColumn(
                name: "RutaArchivo",
                table: "PlanosMaestro");

            migrationBuilder.CreateTable(
                name: "ArchivosPlanoMaestro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlanoMaestroId = table.Column<int>(type: "integer", nullable: false),
                    NombreOriginal = table.Column<string>(type: "text", nullable: false),
                    RutaArchivo = table.Column<string>(type: "text", nullable: false),
                    FechaCarga = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivosPlanoMaestro", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchivosPlanoMaestro_PlanosMaestro_PlanoMaestroId",
                        column: x => x.PlanoMaestroId,
                        principalTable: "PlanosMaestro",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosPlanoMaestro_PlanoMaestroId",
                table: "ArchivosPlanoMaestro",
                column: "PlanoMaestroId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchivosPlanoMaestro");

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCargaArchivo",
                table: "PlanosMaestro",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NombreArchivoOriginal",
                table: "PlanosMaestro",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RutaArchivo",
                table: "PlanosMaestro",
                type: "text",
                nullable: true);
        }
    }
}
