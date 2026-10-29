using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class AgregarArchivosMaterial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RemisionOCertificado",
                table: "Materiales",
                newName: "NumeroRemision");

            migrationBuilder.AddColumn<string>(
                name: "NumeroCertificado",
                table: "Materiales",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RutaCertificadoPdf",
                table: "Materiales",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RutaRemisionPdf",
                table: "Materiales",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FotosMateriales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MaterialId = table.Column<int>(type: "integer", nullable: false),
                    RutaArchivo = table.Column<string>(type: "text", nullable: false),
                    FechaCarga = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FotosMateriales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FotosMateriales_Materiales_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materiales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FotosMateriales_MaterialId",
                table: "FotosMateriales",
                column: "MaterialId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FotosMateriales");

            migrationBuilder.DropColumn(
                name: "NumeroCertificado",
                table: "Materiales");

            migrationBuilder.DropColumn(
                name: "RutaCertificadoPdf",
                table: "Materiales");

            migrationBuilder.DropColumn(
                name: "RutaRemisionPdf",
                table: "Materiales");

            migrationBuilder.RenameColumn(
                name: "NumeroRemision",
                table: "Materiales",
                newName: "RemisionOCertificado");
        }
    }
}
