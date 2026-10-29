using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class RevisionesDocumentoYCarpetasPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RevisionDocumentoId",
                table: "VersionesDocumento",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CarpetasPlan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlanAccionId = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarpetasPlan", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CarpetasPlan_PlanesAccion_PlanAccionId",
                        column: x => x.PlanAccionId,
                        principalTable: "PlanesAccion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RevisionesDocumento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentoMaestroId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<string>(type: "text", nullable: true),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Cambio = table.Column<string>(type: "text", nullable: true),
                    Responsable = table.Column<string>(type: "text", nullable: true),
                    Creada = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevisionesDocumento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RevisionesDocumento_DocumentosMaestro_DocumentoMaestroId",
                        column: x => x.DocumentoMaestroId,
                        principalTable: "DocumentosMaestro",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RiesgosPlan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlanAccionId = table.Column<int>(type: "integer", nullable: false),
                    RiesgoId = table.Column<int>(type: "integer", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiesgosPlan", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiesgosPlan_PlanesAccion_PlanAccionId",
                        column: x => x.PlanAccionId,
                        principalTable: "PlanesAccion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RiesgosPlan_Riesgos_RiesgoId",
                        column: x => x.RiesgoId,
                        principalTable: "Riesgos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArchivosCarpetaPlan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CarpetaPlanId = table.Column<int>(type: "integer", nullable: false),
                    NombreOriginal = table.Column<string>(type: "text", nullable: false),
                    RutaArchivo = table.Column<string>(type: "text", nullable: false),
                    FechaCarga = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivosCarpetaPlan", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchivosCarpetaPlan_CarpetasPlan_CarpetaPlanId",
                        column: x => x.CarpetaPlanId,
                        principalTable: "CarpetasPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VersionesDocumento_RevisionDocumentoId",
                table: "VersionesDocumento",
                column: "RevisionDocumentoId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosCarpetaPlan_CarpetaPlanId",
                table: "ArchivosCarpetaPlan",
                column: "CarpetaPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_CarpetasPlan_PlanAccionId",
                table: "CarpetasPlan",
                column: "PlanAccionId");

            migrationBuilder.CreateIndex(
                name: "IX_RevisionesDocumento_DocumentoMaestroId",
                table: "RevisionesDocumento",
                column: "DocumentoMaestroId");

            migrationBuilder.CreateIndex(
                name: "IX_RiesgosPlan_PlanAccionId_RiesgoId",
                table: "RiesgosPlan",
                columns: new[] { "PlanAccionId", "RiesgoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RiesgosPlan_RiesgoId",
                table: "RiesgosPlan",
                column: "RiesgoId");

            migrationBuilder.AddForeignKey(
                name: "FK_VersionesDocumento_RevisionesDocumento_RevisionDocumentoId",
                table: "VersionesDocumento",
                column: "RevisionDocumentoId",
                principalTable: "RevisionesDocumento",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VersionesDocumento_RevisionesDocumento_RevisionDocumentoId",
                table: "VersionesDocumento");

            migrationBuilder.DropTable(
                name: "ArchivosCarpetaPlan");

            migrationBuilder.DropTable(
                name: "RevisionesDocumento");

            migrationBuilder.DropTable(
                name: "RiesgosPlan");

            migrationBuilder.DropTable(
                name: "CarpetasPlan");

            migrationBuilder.DropIndex(
                name: "IX_VersionesDocumento_RevisionDocumentoId",
                table: "VersionesDocumento");

            migrationBuilder.DropColumn(
                name: "RevisionDocumentoId",
                table: "VersionesDocumento");
        }
    }
}
