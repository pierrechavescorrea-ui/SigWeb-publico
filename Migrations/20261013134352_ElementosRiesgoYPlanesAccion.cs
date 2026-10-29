using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class ElementosRiesgoYPlanesAccion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ElementosRiesgo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RiesgoId = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Texto = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElementosRiesgo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElementosRiesgo_Riesgos_RiesgoId",
                        column: x => x.RiesgoId,
                        principalTable: "Riesgos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanesAccion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Codigo = table.Column<string>(type: "text", nullable: false),
                    DescripcionHallazgo = table.Column<string>(type: "text", nullable: false),
                    Proceso = table.Column<string>(type: "text", nullable: false),
                    ItemCatalogoId = table.Column<int>(type: "integer", nullable: true),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AnalisisCausa = table.Column<string>(type: "text", nullable: false),
                    RiesgoId = table.Column<int>(type: "integer", nullable: true),
                    SeguimientoRiesgoId = table.Column<int>(type: "integer", nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanesAccion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanesAccion_Catalogos_ItemCatalogoId",
                        column: x => x.ItemCatalogoId,
                        principalTable: "Catalogos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PlanesAccion_Riesgos_RiesgoId",
                        column: x => x.RiesgoId,
                        principalTable: "Riesgos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PlanesAccion_SeguimientosRiesgo_SeguimientoRiesgoId",
                        column: x => x.SeguimientoRiesgoId,
                        principalTable: "SeguimientosRiesgo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AccionesPlan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlanAccionId = table.Column<int>(type: "integer", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Producto = table.Column<string>(type: "text", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Responsable = table.Column<string>(type: "text", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccionesPlan", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccionesPlan_PlanesAccion_PlanAccionId",
                        column: x => x.PlanAccionId,
                        principalTable: "PlanesAccion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccionesPlan_PlanAccionId",
                table: "AccionesPlan",
                column: "PlanAccionId");

            migrationBuilder.CreateIndex(
                name: "IX_ElementosRiesgo_RiesgoId_Tipo_Orden",
                table: "ElementosRiesgo",
                columns: new[] { "RiesgoId", "Tipo", "Orden" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanesAccion_Codigo",
                table: "PlanesAccion",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanesAccion_ItemCatalogoId",
                table: "PlanesAccion",
                column: "ItemCatalogoId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanesAccion_RiesgoId",
                table: "PlanesAccion",
                column: "RiesgoId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanesAccion_SeguimientoRiesgoId",
                table: "PlanesAccion",
                column: "SeguimientoRiesgoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccionesPlan");

            migrationBuilder.DropTable(
                name: "ElementosRiesgo");

            migrationBuilder.DropTable(
                name: "PlanesAccion");
        }
    }
}
