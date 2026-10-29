using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Materiales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Localizacion = table.Column<string>(type: "text", nullable: false),
                    NombreMaterial = table.Column<string>(type: "text", nullable: false),
                    RemisionOCertificado = table.Column<string>(type: "text", nullable: false),
                    Proveedor = table.Column<string>(type: "text", nullable: false),
                    Lote = table.Column<string>(type: "text", nullable: false),
                    CriterioAceptacion = table.Column<string>(type: "text", nullable: false),
                    DocumentoReferencia = table.Column<string>(type: "text", nullable: false),
                    CodigoComunicadoEnvio = table.Column<string>(type: "text", nullable: false),
                    AprobadoPorInterventoria = table.Column<bool>(type: "boolean", nullable: false),
                    FechaAprobacionInterventoria = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CodigoComunicacionAprobacion = table.Column<string>(type: "text", nullable: false),
                    Observaciones = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Materiales", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Materiales");
        }
    }
}
