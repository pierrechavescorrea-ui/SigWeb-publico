using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class DocumentosVersionesYCargas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CargaDocumentosId",
                table: "DocumentosMaestro",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Estado",
                table: "DocumentosMaestro",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoDocumental",
                table: "DocumentosMaestro",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CargasDocumentos",
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
                    table.PrimaryKey("PK_CargasDocumentos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VersionesDocumento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentoMaestroId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<string>(type: "text", nullable: true),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Observaciones = table.Column<string>(type: "text", nullable: true),
                    NombreOriginal = table.Column<string>(type: "text", nullable: false),
                    RutaArchivo = table.Column<string>(type: "text", nullable: false),
                    FechaCarga = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VersionesDocumento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VersionesDocumento_DocumentosMaestro_DocumentoMaestroId",
                        column: x => x.DocumentoMaestroId,
                        principalTable: "DocumentosMaestro",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosMaestro_CargaDocumentosId",
                table: "DocumentosMaestro",
                column: "CargaDocumentosId");

            migrationBuilder.CreateIndex(
                name: "IX_VersionesDocumento_DocumentoMaestroId",
                table: "VersionesDocumento",
                column: "DocumentoMaestroId");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentosMaestro_CargasDocumentos_CargaDocumentosId",
                table: "DocumentosMaestro",
                column: "CargaDocumentosId",
                principalTable: "CargasDocumentos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentosMaestro_CargasDocumentos_CargaDocumentosId",
                table: "DocumentosMaestro");

            migrationBuilder.DropTable(
                name: "CargasDocumentos");

            migrationBuilder.DropTable(
                name: "VersionesDocumento");

            migrationBuilder.DropIndex(
                name: "IX_DocumentosMaestro_CargaDocumentosId",
                table: "DocumentosMaestro");

            migrationBuilder.DropColumn(
                name: "CargaDocumentosId",
                table: "DocumentosMaestro");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "DocumentosMaestro");

            migrationBuilder.DropColumn(
                name: "TipoDocumental",
                table: "DocumentosMaestro");
        }
    }
}
