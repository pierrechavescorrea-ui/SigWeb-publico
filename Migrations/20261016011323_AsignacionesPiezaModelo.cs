using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class AsignacionesPiezaModelo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AsignacionesPiezaModelo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Modelo = table.Column<string>(type: "text", nullable: false),
                    Pieza = table.Column<string>(type: "text", nullable: false),
                    Hoja = table.Column<int>(type: "integer", nullable: true),
                    Marca = table.Column<string>(type: "text", nullable: true),
                    Perfil = table.Column<string>(type: "text", nullable: true),
                    ArchivoPlanoMaestroId = table.Column<int>(type: "integer", nullable: true),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsignacionesPiezaModelo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AsignacionesPiezaModelo_ArchivosPlanoMaestro_ArchivoPlanoMa~",
                        column: x => x.ArchivoPlanoMaestroId,
                        principalTable: "ArchivosPlanoMaestro",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesPiezaModelo_ArchivoPlanoMaestroId",
                table: "AsignacionesPiezaModelo",
                column: "ArchivoPlanoMaestroId");

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesPiezaModelo_Modelo_Pieza",
                table: "AsignacionesPiezaModelo",
                columns: new[] { "Modelo", "Pieza" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AsignacionesPiezaModelo");
        }
    }
}
