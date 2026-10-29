using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class LugaresYAnexosPlanes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArchivosAccionPlan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccionPlanId = table.Column<int>(type: "integer", nullable: false),
                    NombreOriginal = table.Column<string>(type: "text", nullable: false),
                    RutaArchivo = table.Column<string>(type: "text", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivosAccionPlan", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchivosAccionPlan_AccionesPlan_AccionPlanId",
                        column: x => x.AccionPlanId,
                        principalTable: "AccionesPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LugaresPlan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlanAccionId = table.Column<int>(type: "integer", nullable: false),
                    ElementoTrazadoId = table.Column<int>(type: "integer", nullable: true),
                    ComponenteId = table.Column<int>(type: "integer", nullable: true),
                    ItemCatalogoId = table.Column<int>(type: "integer", nullable: true),
                    TiroId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LugaresPlan", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LugaresPlan_Catalogos_ItemCatalogoId",
                        column: x => x.ItemCatalogoId,
                        principalTable: "Catalogos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LugaresPlan_Componentes_ComponenteId",
                        column: x => x.ComponenteId,
                        principalTable: "Componentes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LugaresPlan_ElementosTrazado_ElementoTrazadoId",
                        column: x => x.ElementoTrazadoId,
                        principalTable: "ElementosTrazado",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LugaresPlan_PlanesAccion_PlanAccionId",
                        column: x => x.PlanAccionId,
                        principalTable: "PlanesAccion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LugaresPlan_Tiros_TiroId",
                        column: x => x.TiroId,
                        principalTable: "Tiros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosAccionPlan_AccionPlanId",
                table: "ArchivosAccionPlan",
                column: "AccionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_LugaresPlan_ComponenteId",
                table: "LugaresPlan",
                column: "ComponenteId");

            migrationBuilder.CreateIndex(
                name: "IX_LugaresPlan_ElementoTrazadoId",
                table: "LugaresPlan",
                column: "ElementoTrazadoId");

            migrationBuilder.CreateIndex(
                name: "IX_LugaresPlan_ItemCatalogoId",
                table: "LugaresPlan",
                column: "ItemCatalogoId");

            migrationBuilder.CreateIndex(
                name: "IX_LugaresPlan_PlanAccionId",
                table: "LugaresPlan",
                column: "PlanAccionId");

            migrationBuilder.CreateIndex(
                name: "IX_LugaresPlan_TiroId",
                table: "LugaresPlan",
                column: "TiroId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchivosAccionPlan");

            migrationBuilder.DropTable(
                name: "LugaresPlan");
        }
    }
}
