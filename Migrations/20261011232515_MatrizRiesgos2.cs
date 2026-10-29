using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class MatrizRiesgos2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Responsable",
                table: "VersionesMatrizRiesgos",
                newName: "RevisadoPor");

            migrationBuilder.AddColumn<string>(
                name: "AprobadoPor",
                table: "VersionesMatrizRiesgos",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ElaboradoPor",
                table: "VersionesMatrizRiesgos",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EstadoDocumento",
                table: "VersionesMatrizRiesgos",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Etapa",
                table: "VersionesMatrizRiesgos",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "HojaOrigen",
                table: "VersionesMatrizRiesgos",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AprobadoPor",
                table: "VersionesMatrizRiesgos");

            migrationBuilder.DropColumn(
                name: "ElaboradoPor",
                table: "VersionesMatrizRiesgos");

            migrationBuilder.DropColumn(
                name: "EstadoDocumento",
                table: "VersionesMatrizRiesgos");

            migrationBuilder.DropColumn(
                name: "Etapa",
                table: "VersionesMatrizRiesgos");

            migrationBuilder.DropColumn(
                name: "HojaOrigen",
                table: "VersionesMatrizRiesgos");

            migrationBuilder.RenameColumn(
                name: "RevisadoPor",
                table: "VersionesMatrizRiesgos",
                newName: "Responsable");
        }
    }
}
