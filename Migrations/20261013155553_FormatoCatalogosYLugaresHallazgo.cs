using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class FormatoCatalogosYLugaresHallazgo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ColorFondo",
                table: "Catalogos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ColorTexto",
                table: "Catalogos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Estilo",
                table: "Catalogos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Forma",
                table: "Catalogos",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "Negrita",
                table: "Catalogos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "LugaresHallazgo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HallazgoRiesgoId = table.Column<int>(type: "integer", nullable: false),
                    ElementoTrazadoId = table.Column<int>(type: "integer", nullable: true),
                    ComponenteId = table.Column<int>(type: "integer", nullable: true),
                    ItemCatalogoId = table.Column<int>(type: "integer", nullable: true),
                    TiroId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LugaresHallazgo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LugaresHallazgo_Catalogos_ItemCatalogoId",
                        column: x => x.ItemCatalogoId,
                        principalTable: "Catalogos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LugaresHallazgo_Componentes_ComponenteId",
                        column: x => x.ComponenteId,
                        principalTable: "Componentes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LugaresHallazgo_ElementosTrazado_ElementoTrazadoId",
                        column: x => x.ElementoTrazadoId,
                        principalTable: "ElementosTrazado",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LugaresHallazgo_HallazgosRiesgo_HallazgoRiesgoId",
                        column: x => x.HallazgoRiesgoId,
                        principalTable: "HallazgosRiesgo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LugaresHallazgo_Tiros_TiroId",
                        column: x => x.TiroId,
                        principalTable: "Tiros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LugaresHallazgo_ComponenteId",
                table: "LugaresHallazgo",
                column: "ComponenteId");

            migrationBuilder.CreateIndex(
                name: "IX_LugaresHallazgo_ElementoTrazadoId",
                table: "LugaresHallazgo",
                column: "ElementoTrazadoId");

            migrationBuilder.CreateIndex(
                name: "IX_LugaresHallazgo_HallazgoRiesgoId",
                table: "LugaresHallazgo",
                column: "HallazgoRiesgoId");

            migrationBuilder.CreateIndex(
                name: "IX_LugaresHallazgo_ItemCatalogoId",
                table: "LugaresHallazgo",
                column: "ItemCatalogoId");

            migrationBuilder.CreateIndex(
                name: "IX_LugaresHallazgo_TiroId",
                table: "LugaresHallazgo",
                column: "TiroId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LugaresHallazgo");

            migrationBuilder.DropColumn(
                name: "ColorFondo",
                table: "Catalogos");

            migrationBuilder.DropColumn(
                name: "ColorTexto",
                table: "Catalogos");

            migrationBuilder.DropColumn(
                name: "Estilo",
                table: "Catalogos");

            migrationBuilder.DropColumn(
                name: "Forma",
                table: "Catalogos");

            migrationBuilder.DropColumn(
                name: "Negrita",
                table: "Catalogos");
        }
    }
}
