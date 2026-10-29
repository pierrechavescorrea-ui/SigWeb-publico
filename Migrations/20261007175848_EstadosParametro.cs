using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class EstadosParametro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EstadoParametroId",
                table: "RegistrosParametro",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EstadosParametro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Porcentaje = table.Column<decimal>(type: "numeric", nullable: false),
                    Color = table.Column<string>(type: "text", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstadosParametro", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosParametro_EstadoParametroId",
                table: "RegistrosParametro",
                column: "EstadoParametroId");

            migrationBuilder.AddForeignKey(
                name: "FK_RegistrosParametro_EstadosParametro_EstadoParametroId",
                table: "RegistrosParametro",
                column: "EstadoParametroId",
                principalTable: "EstadosParametro",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RegistrosParametro_EstadosParametro_EstadoParametroId",
                table: "RegistrosParametro");

            migrationBuilder.DropTable(
                name: "EstadosParametro");

            migrationBuilder.DropIndex(
                name: "IX_RegistrosParametro_EstadoParametroId",
                table: "RegistrosParametro");

            migrationBuilder.DropColumn(
                name: "EstadoParametroId",
                table: "RegistrosParametro");
        }
    }
}
