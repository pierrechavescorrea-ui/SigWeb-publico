using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class AdjuntosEnsayo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AdjuntoEnsayoId",
                table: "ValoresCampoEnsayo",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LlevaAdjunto",
                table: "CamposEnsayo",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AdjuntosEnsayo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Hash = table.Column<string>(type: "text", nullable: false),
                    NombreOriginal = table.Column<string>(type: "text", nullable: false),
                    Ruta = table.Column<string>(type: "text", nullable: false),
                    FechaCarga = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdjuntosEnsayo", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ValoresCampoEnsayo_AdjuntoEnsayoId",
                table: "ValoresCampoEnsayo",
                column: "AdjuntoEnsayoId");

            migrationBuilder.CreateIndex(
                name: "IX_AdjuntosEnsayo_Hash",
                table: "AdjuntosEnsayo",
                column: "Hash",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ValoresCampoEnsayo_AdjuntosEnsayo_AdjuntoEnsayoId",
                table: "ValoresCampoEnsayo",
                column: "AdjuntoEnsayoId",
                principalTable: "AdjuntosEnsayo",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ValoresCampoEnsayo_AdjuntosEnsayo_AdjuntoEnsayoId",
                table: "ValoresCampoEnsayo");

            migrationBuilder.DropTable(
                name: "AdjuntosEnsayo");

            migrationBuilder.DropIndex(
                name: "IX_ValoresCampoEnsayo_AdjuntoEnsayoId",
                table: "ValoresCampoEnsayo");

            migrationBuilder.DropColumn(
                name: "AdjuntoEnsayoId",
                table: "ValoresCampoEnsayo");

            migrationBuilder.DropColumn(
                name: "LlevaAdjunto",
                table: "CamposEnsayo");
        }
    }
}
