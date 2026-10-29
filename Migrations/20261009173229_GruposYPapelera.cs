using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class GruposYPapelera : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Eliminado",
                table: "TiposEnsayo",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEliminacion",
                table: "TiposEnsayo",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GrupoPlanoId",
                table: "PlanosMaestro",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Eliminado",
                table: "GruposEnsayo",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEliminacion",
                table: "GruposEnsayo",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Eliminado",
                table: "DefinicionesCatalogo",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEliminacion",
                table: "DefinicionesCatalogo",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IconoCatalogoId",
                table: "DefinicionesCatalogo",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GruposPlano",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    IconoCatalogoId = table.Column<int>(type: "integer", nullable: true),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GruposPlano", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GruposPlano_IconosCatalogo_IconoCatalogoId",
                        column: x => x.IconoCatalogoId,
                        principalTable: "IconosCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanosMaestro_GrupoPlanoId",
                table: "PlanosMaestro",
                column: "GrupoPlanoId");

            migrationBuilder.CreateIndex(
                name: "IX_DefinicionesCatalogo_IconoCatalogoId",
                table: "DefinicionesCatalogo",
                column: "IconoCatalogoId");

            migrationBuilder.CreateIndex(
                name: "IX_GruposPlano_IconoCatalogoId",
                table: "GruposPlano",
                column: "IconoCatalogoId");

            migrationBuilder.CreateIndex(
                name: "IX_GruposPlano_Nombre",
                table: "GruposPlano",
                column: "Nombre",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DefinicionesCatalogo_IconosCatalogo_IconoCatalogoId",
                table: "DefinicionesCatalogo",
                column: "IconoCatalogoId",
                principalTable: "IconosCatalogo",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PlanosMaestro_GruposPlano_GrupoPlanoId",
                table: "PlanosMaestro",
                column: "GrupoPlanoId",
                principalTable: "GruposPlano",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DefinicionesCatalogo_IconosCatalogo_IconoCatalogoId",
                table: "DefinicionesCatalogo");

            migrationBuilder.DropForeignKey(
                name: "FK_PlanosMaestro_GruposPlano_GrupoPlanoId",
                table: "PlanosMaestro");

            migrationBuilder.DropTable(
                name: "GruposPlano");

            migrationBuilder.DropIndex(
                name: "IX_PlanosMaestro_GrupoPlanoId",
                table: "PlanosMaestro");

            migrationBuilder.DropIndex(
                name: "IX_DefinicionesCatalogo_IconoCatalogoId",
                table: "DefinicionesCatalogo");

            migrationBuilder.DropColumn(
                name: "Eliminado",
                table: "TiposEnsayo");

            migrationBuilder.DropColumn(
                name: "FechaEliminacion",
                table: "TiposEnsayo");

            migrationBuilder.DropColumn(
                name: "GrupoPlanoId",
                table: "PlanosMaestro");

            migrationBuilder.DropColumn(
                name: "Eliminado",
                table: "GruposEnsayo");

            migrationBuilder.DropColumn(
                name: "FechaEliminacion",
                table: "GruposEnsayo");

            migrationBuilder.DropColumn(
                name: "Eliminado",
                table: "DefinicionesCatalogo");

            migrationBuilder.DropColumn(
                name: "FechaEliminacion",
                table: "DefinicionesCatalogo");

            migrationBuilder.DropColumn(
                name: "IconoCatalogoId",
                table: "DefinicionesCatalogo");
        }
    }
}
