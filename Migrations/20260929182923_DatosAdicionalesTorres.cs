using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class DatosAdicionalesTorres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Eliminado",
                table: "ArchivosRegistroParametro",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEliminacion",
                table: "ArchivosRegistroParametro",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ArchivosComponente",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ComponenteId = table.Column<int>(type: "integer", nullable: false),
                    NombreOriginal = table.Column<string>(type: "text", nullable: false),
                    RutaArchivo = table.Column<string>(type: "text", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivosComponente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchivosComponente_Componentes_ComponenteId",
                        column: x => x.ComponenteId,
                        principalTable: "Componentes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CamposExtraComponente",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ComponenteId = table.Column<int>(type: "integer", nullable: false),
                    Clave = table.Column<string>(type: "text", nullable: false),
                    Valor = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CamposExtraComponente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CamposExtraComponente_Componentes_ComponenteId",
                        column: x => x.ComponenteId,
                        principalTable: "Componentes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CamposExtraTorre",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ElementoTrazadoId = table.Column<int>(type: "integer", nullable: false),
                    Clave = table.Column<string>(type: "text", nullable: false),
                    Valor = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CamposExtraTorre", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CamposExtraTorre_ElementosTrazado_ElementoTrazadoId",
                        column: x => x.ElementoTrazadoId,
                        principalTable: "ElementosTrazado",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosComponente_ComponenteId",
                table: "ArchivosComponente",
                column: "ComponenteId");

            migrationBuilder.CreateIndex(
                name: "IX_CamposExtraComponente_ComponenteId",
                table: "CamposExtraComponente",
                column: "ComponenteId");

            migrationBuilder.CreateIndex(
                name: "IX_CamposExtraTorre_ElementoTrazadoId",
                table: "CamposExtraTorre",
                column: "ElementoTrazadoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchivosComponente");

            migrationBuilder.DropTable(
                name: "CamposExtraComponente");

            migrationBuilder.DropTable(
                name: "CamposExtraTorre");

            migrationBuilder.DropColumn(
                name: "Eliminado",
                table: "ArchivosRegistroParametro");

            migrationBuilder.DropColumn(
                name: "FechaEliminacion",
                table: "ArchivosRegistroParametro");
        }
    }
}
