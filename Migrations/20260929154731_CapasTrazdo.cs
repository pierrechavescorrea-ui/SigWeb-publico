using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class CapasTrazdo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CapaTrazadoId",
                table: "ElementosTrazado",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Eliminado",
                table: "ElementosTrazado",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEliminacion",
                table: "ElementosTrazado",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Capas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Origen = table.Column<string>(type: "text", nullable: false),
                    FechaCarga = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Visible = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Capas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ElementosTrazado_CapaTrazadoId",
                table: "ElementosTrazado",
                column: "CapaTrazadoId");

            migrationBuilder.AddForeignKey(
                name: "FK_ElementosTrazado_Capas_CapaTrazadoId",
                table: "ElementosTrazado",
                column: "CapaTrazadoId",
                principalTable: "Capas",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ElementosTrazado_Capas_CapaTrazadoId",
                table: "ElementosTrazado");

            migrationBuilder.DropTable(
                name: "Capas");

            migrationBuilder.DropIndex(
                name: "IX_ElementosTrazado_CapaTrazadoId",
                table: "ElementosTrazado");

            migrationBuilder.DropColumn(
                name: "CapaTrazadoId",
                table: "ElementosTrazado");

            migrationBuilder.DropColumn(
                name: "Eliminado",
                table: "ElementosTrazado");

            migrationBuilder.DropColumn(
                name: "FechaEliminacion",
                table: "ElementosTrazado");
        }
    }
}
