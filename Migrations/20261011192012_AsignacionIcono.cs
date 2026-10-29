using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class AsignacionIcono : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AsignacionesIcono",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Punto = table.Column<string>(type: "text", nullable: false),
                    IconoCatalogoId = table.Column<int>(type: "integer", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsignacionesIcono", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AsignacionesIcono_IconosCatalogo_IconoCatalogoId",
                        column: x => x.IconoCatalogoId,
                        principalTable: "IconosCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesIcono_IconoCatalogoId",
                table: "AsignacionesIcono",
                column: "IconoCatalogoId");

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesIcono_Punto",
                table: "AsignacionesIcono",
                column: "Punto",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AsignacionesIcono");
        }
    }
}
