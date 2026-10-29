using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class SeguimientosAccion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SeguimientoAccionId",
                table: "ArchivosAccionPlan",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SeguimientosAccion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccionPlanId = table.Column<int>(type: "integer", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Observacion = table.Column<string>(type: "text", nullable: false),
                    Responsable = table.Column<string>(type: "text", nullable: false),
                    EstadoAnterior = table.Column<int>(type: "integer", nullable: false),
                    EstadoNuevo = table.Column<int>(type: "integer", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeguimientosAccion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeguimientosAccion_AccionesPlan_AccionPlanId",
                        column: x => x.AccionPlanId,
                        principalTable: "AccionesPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosAccionPlan_SeguimientoAccionId",
                table: "ArchivosAccionPlan",
                column: "SeguimientoAccionId");

            migrationBuilder.CreateIndex(
                name: "IX_SeguimientosAccion_AccionPlanId",
                table: "SeguimientosAccion",
                column: "AccionPlanId");

            migrationBuilder.AddForeignKey(
                name: "FK_ArchivosAccionPlan_SeguimientosAccion_SeguimientoAccionId",
                table: "ArchivosAccionPlan",
                column: "SeguimientoAccionId",
                principalTable: "SeguimientosAccion",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ArchivosAccionPlan_SeguimientosAccion_SeguimientoAccionId",
                table: "ArchivosAccionPlan");

            migrationBuilder.DropTable(
                name: "SeguimientosAccion");

            migrationBuilder.DropIndex(
                name: "IX_ArchivosAccionPlan_SeguimientoAccionId",
                table: "ArchivosAccionPlan");

            migrationBuilder.DropColumn(
                name: "SeguimientoAccionId",
                table: "ArchivosAccionPlan");
        }
    }
}
