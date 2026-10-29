using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class UbicacionesMaterial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UbicacionesMaterial",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MaterialId = table.Column<int>(type: "integer", nullable: false),
                    ElementoTrazadoId = table.Column<int>(type: "integer", nullable: true),
                    ComponenteId = table.Column<int>(type: "integer", nullable: true),
                    TiroId = table.Column<int>(type: "integer", nullable: true),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UbicacionesMaterial", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UbicacionesMaterial_Componentes_ComponenteId",
                        column: x => x.ComponenteId,
                        principalTable: "Componentes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UbicacionesMaterial_ElementosTrazado_ElementoTrazadoId",
                        column: x => x.ElementoTrazadoId,
                        principalTable: "ElementosTrazado",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UbicacionesMaterial_Materiales_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materiales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UbicacionesMaterial_Tiros_TiroId",
                        column: x => x.TiroId,
                        principalTable: "Tiros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UbicacionesMaterial_ComponenteId",
                table: "UbicacionesMaterial",
                column: "ComponenteId");

            migrationBuilder.CreateIndex(
                name: "IX_UbicacionesMaterial_ElementoTrazadoId",
                table: "UbicacionesMaterial",
                column: "ElementoTrazadoId");

            migrationBuilder.CreateIndex(
                name: "IX_UbicacionesMaterial_MaterialId",
                table: "UbicacionesMaterial",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_UbicacionesMaterial_TiroId",
                table: "UbicacionesMaterial",
                column: "TiroId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UbicacionesMaterial");
        }
    }
}
