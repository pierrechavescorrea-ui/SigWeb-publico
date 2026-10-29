using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class PlanosMaestro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlanosMaestro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Codigo = table.Column<string>(type: "text", nullable: false),
                    Titulo = table.Column<string>(type: "text", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RevisionVigente = table.Column<string>(type: "text", nullable: false),
                    Estado = table.Column<string>(type: "text", nullable: false),
                    Notas = table.Column<string>(type: "text", nullable: true),
                    RevisionAnterior = table.Column<string>(type: "text", nullable: true),
                    RutaArchivo = table.Column<string>(type: "text", nullable: true),
                    NombreArchivoOriginal = table.Column<string>(type: "text", nullable: true),
                    FechaCargaArchivo = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Origen = table.Column<string>(type: "text", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanosMaestro", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComponentesPlano",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ComponenteId = table.Column<int>(type: "integer", nullable: false),
                    PlanoMaestroId = table.Column<int>(type: "integer", nullable: false),
                    FechaVinculo = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComponentesPlano", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComponentesPlano_Componentes_ComponenteId",
                        column: x => x.ComponenteId,
                        principalTable: "Componentes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ComponentesPlano_PlanosMaestro_PlanoMaestroId",
                        column: x => x.PlanoMaestroId,
                        principalTable: "PlanosMaestro",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComponentesPlano_ComponenteId_PlanoMaestroId",
                table: "ComponentesPlano",
                columns: new[] { "ComponenteId", "PlanoMaestroId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComponentesPlano_PlanoMaestroId",
                table: "ComponentesPlano",
                column: "PlanoMaestroId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComponentesPlano");

            migrationBuilder.DropTable(
                name: "PlanosMaestro");
        }
    }
}
