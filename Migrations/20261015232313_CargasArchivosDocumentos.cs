using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class CargasArchivosDocumentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CargaArchivosDocumentosId",
                table: "VersionesDocumento",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CargasArchivosDocumentos",
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
                    table.PrimaryKey("PK_CargasArchivosDocumentos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VersionesDocumento_CargaArchivosDocumentosId",
                table: "VersionesDocumento",
                column: "CargaArchivosDocumentosId");

            migrationBuilder.AddForeignKey(
                name: "FK_VersionesDocumento_CargasArchivosDocumentos_CargaArchivosDo~",
                table: "VersionesDocumento",
                column: "CargaArchivosDocumentosId",
                principalTable: "CargasArchivosDocumentos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VersionesDocumento_CargasArchivosDocumentos_CargaArchivosDo~",
                table: "VersionesDocumento");

            migrationBuilder.DropTable(
                name: "CargasArchivosDocumentos");

            migrationBuilder.DropIndex(
                name: "IX_VersionesDocumento_CargaArchivosDocumentosId",
                table: "VersionesDocumento");

            migrationBuilder.DropColumn(
                name: "CargaArchivosDocumentosId",
                table: "VersionesDocumento");
        }
    }
}
