using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class HojaDeVidaYActividadesPorElemento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EsHojaDeVida",
                table: "Componentes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ComponenteActividades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ComponenteId = table.Column<int>(type: "integer", nullable: false),
                    ActividadId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComponenteActividades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComponenteActividades_Actividades_ActividadId",
                        column: x => x.ActividadId,
                        principalTable: "Actividades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ComponenteActividades_Componentes_ComponenteId",
                        column: x => x.ComponenteId,
                        principalTable: "Componentes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComponenteActividades_ActividadId",
                table: "ComponenteActividades",
                column: "ActividadId");

            migrationBuilder.CreateIndex(
                name: "IX_ComponenteActividades_ComponenteId_ActividadId",
                table: "ComponenteActividades",
                columns: new[] { "ComponenteId", "ActividadId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComponenteActividades");

            migrationBuilder.DropColumn(
                name: "EsHojaDeVida",
                table: "Componentes");
        }
    }
}
