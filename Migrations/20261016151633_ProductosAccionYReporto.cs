using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class ProductosAccionYReporto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReportoCargo",
                table: "PlanesAccion",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReportoEmpresa",
                table: "PlanesAccion",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReportoNombre",
                table: "PlanesAccion",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ProductoAccionId",
                table: "ArchivosAccionPlan",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductosAccion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccionPlanId = table.Column<int>(type: "integer", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductosAccion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductosAccion_AccionesPlan_AccionPlanId",
                        column: x => x.AccionPlanId,
                        principalTable: "AccionesPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosAccionPlan_ProductoAccionId",
                table: "ArchivosAccionPlan",
                column: "ProductoAccionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductosAccion_AccionPlanId",
                table: "ProductosAccion",
                column: "AccionPlanId");

            migrationBuilder.AddForeignKey(
                name: "FK_ArchivosAccionPlan_ProductosAccion_ProductoAccionId",
                table: "ArchivosAccionPlan",
                column: "ProductoAccionId",
                principalTable: "ProductosAccion",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ArchivosAccionPlan_ProductosAccion_ProductoAccionId",
                table: "ArchivosAccionPlan");

            migrationBuilder.DropTable(
                name: "ProductosAccion");

            migrationBuilder.DropIndex(
                name: "IX_ArchivosAccionPlan_ProductoAccionId",
                table: "ArchivosAccionPlan");

            migrationBuilder.DropColumn(
                name: "ReportoCargo",
                table: "PlanesAccion");

            migrationBuilder.DropColumn(
                name: "ReportoEmpresa",
                table: "PlanesAccion");

            migrationBuilder.DropColumn(
                name: "ReportoNombre",
                table: "PlanesAccion");

            migrationBuilder.DropColumn(
                name: "ProductoAccionId",
                table: "ArchivosAccionPlan");
        }
    }
}
