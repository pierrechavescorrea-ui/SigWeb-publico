using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class UbicacionPatasYPreferenciasMapa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RutaArchivo",
                table: "RegistrosParametro");

            migrationBuilder.AddColumn<string>(
                name: "IconoTorre",
                table: "Configuracion",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "MostrarPatasEnMapa",
                table: "Configuracion",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "Latitud",
                table: "Componentes",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitud",
                table: "Componentes",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ArchivosRegistroParametro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RegistroParametroId = table.Column<int>(type: "integer", nullable: false),
                    NombreOriginal = table.Column<string>(type: "text", nullable: false),
                    RutaArchivo = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivosRegistroParametro", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchivosRegistroParametro_RegistrosParametro_RegistroParame~",
                        column: x => x.RegistroParametroId,
                        principalTable: "RegistrosParametro",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosRegistroParametro_RegistroParametroId",
                table: "ArchivosRegistroParametro",
                column: "RegistroParametroId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchivosRegistroParametro");

            migrationBuilder.DropColumn(
                name: "IconoTorre",
                table: "Configuracion");

            migrationBuilder.DropColumn(
                name: "MostrarPatasEnMapa",
                table: "Configuracion");

            migrationBuilder.DropColumn(
                name: "Latitud",
                table: "Componentes");

            migrationBuilder.DropColumn(
                name: "Longitud",
                table: "Componentes");

            migrationBuilder.AddColumn<string>(
                name: "RutaArchivo",
                table: "RegistrosParametro",
                type: "text",
                nullable: true);
        }
    }
}
