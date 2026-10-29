using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class MatrizPieTirosPatas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TiroId",
                table: "ElementosTrazado",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Tiros",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tiros", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegistrosParametroTiro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TiroId = table.Column<int>(type: "integer", nullable: false),
                    ParametroActividadId = table.Column<int>(type: "integer", nullable: false),
                    Completado = table.Column<bool>(type: "boolean", nullable: false),
                    EstadoParametroId = table.Column<int>(type: "integer", nullable: true),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Observaciones = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrosParametroTiro", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrosParametroTiro_EstadosParametro_EstadoParametroId",
                        column: x => x.EstadoParametroId,
                        principalTable: "EstadosParametro",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RegistrosParametroTiro_ParametrosActividad_ParametroActivid~",
                        column: x => x.ParametroActividadId,
                        principalTable: "ParametrosActividad",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RegistrosParametroTiro_Tiros_TiroId",
                        column: x => x.TiroId,
                        principalTable: "Tiros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ElementosTrazado_TiroId",
                table: "ElementosTrazado",
                column: "TiroId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosParametroTiro_EstadoParametroId",
                table: "RegistrosParametroTiro",
                column: "EstadoParametroId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosParametroTiro_ParametroActividadId",
                table: "RegistrosParametroTiro",
                column: "ParametroActividadId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosParametroTiro_TiroId_ParametroActividadId",
                table: "RegistrosParametroTiro",
                columns: new[] { "TiroId", "ParametroActividadId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ElementosTrazado_Tiros_TiroId",
                table: "ElementosTrazado",
                column: "TiroId",
                principalTable: "Tiros",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ElementosTrazado_Tiros_TiroId",
                table: "ElementosTrazado");

            migrationBuilder.DropTable(
                name: "RegistrosParametroTiro");

            migrationBuilder.DropTable(
                name: "Tiros");

            migrationBuilder.DropIndex(
                name: "IX_ElementosTrazado_TiroId",
                table: "ElementosTrazado");

            migrationBuilder.DropColumn(
                name: "TiroId",
                table: "ElementosTrazado");
        }
    }
}
