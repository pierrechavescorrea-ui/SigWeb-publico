using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class PlanosCargasYVersiones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CargaPlanosId",
                table: "PlanosMaestro",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CargaArchivosPlanosId",
                table: "ArchivosPlanoMaestro",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Clase",
                table: "ArchivosPlanoMaestro",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "Fecha",
                table: "ArchivosPlanoMaestro",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Version",
                table: "ArchivosPlanoMaestro",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CargasArchivosPlanos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Origen = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: true),
                    Archivos = table.Column<int>(type: "integer", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CargasArchivosPlanos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CargasPlanos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NombreArchivo = table.Column<string>(type: "text", nullable: false),
                    Hoja = table.Column<string>(type: "text", nullable: true),
                    FilaEncabezados = table.Column<int>(type: "integer", nullable: true),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FilasCreadas = table.Column<int>(type: "integer", nullable: false),
                    FilasActualizadas = table.Column<int>(type: "integer", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CargasPlanos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanosMaestro_CargaPlanosId",
                table: "PlanosMaestro",
                column: "CargaPlanosId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosPlanoMaestro_CargaArchivosPlanosId",
                table: "ArchivosPlanoMaestro",
                column: "CargaArchivosPlanosId");

            migrationBuilder.AddForeignKey(
                name: "FK_ArchivosPlanoMaestro_CargasArchivosPlanos_CargaArchivosPlan~",
                table: "ArchivosPlanoMaestro",
                column: "CargaArchivosPlanosId",
                principalTable: "CargasArchivosPlanos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PlanosMaestro_CargasPlanos_CargaPlanosId",
                table: "PlanosMaestro",
                column: "CargaPlanosId",
                principalTable: "CargasPlanos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ArchivosPlanoMaestro_CargasArchivosPlanos_CargaArchivosPlan~",
                table: "ArchivosPlanoMaestro");

            migrationBuilder.DropForeignKey(
                name: "FK_PlanosMaestro_CargasPlanos_CargaPlanosId",
                table: "PlanosMaestro");

            migrationBuilder.DropTable(
                name: "CargasArchivosPlanos");

            migrationBuilder.DropTable(
                name: "CargasPlanos");

            migrationBuilder.DropIndex(
                name: "IX_PlanosMaestro_CargaPlanosId",
                table: "PlanosMaestro");

            migrationBuilder.DropIndex(
                name: "IX_ArchivosPlanoMaestro_CargaArchivosPlanosId",
                table: "ArchivosPlanoMaestro");

            migrationBuilder.DropColumn(
                name: "CargaPlanosId",
                table: "PlanosMaestro");

            migrationBuilder.DropColumn(
                name: "CargaArchivosPlanosId",
                table: "ArchivosPlanoMaestro");

            migrationBuilder.DropColumn(
                name: "Clase",
                table: "ArchivosPlanoMaestro");

            migrationBuilder.DropColumn(
                name: "Fecha",
                table: "ArchivosPlanoMaestro");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "ArchivosPlanoMaestro");
        }
    }
}
