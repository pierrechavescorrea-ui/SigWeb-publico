using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class ModuloEnsayos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IconosCatalogo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Clave = table.Column<string>(type: "text", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    RutaArchivo = table.Column<string>(type: "text", nullable: false),
                    Categoria = table.Column<string>(type: "text", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IconosCatalogo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GruposEnsayo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    IconoCatalogoId = table.Column<int>(type: "integer", nullable: true),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GruposEnsayo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GruposEnsayo_IconosCatalogo_IconoCatalogoId",
                        column: x => x.IconoCatalogoId,
                        principalTable: "IconosCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TiposEnsayo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GrupoEnsayoId = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    IconoCatalogoId = table.Column<int>(type: "integer", nullable: true),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TiposEnsayo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TiposEnsayo_GruposEnsayo_GrupoEnsayoId",
                        column: x => x.GrupoEnsayoId,
                        principalTable: "GruposEnsayo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TiposEnsayo_IconosCatalogo_IconoCatalogoId",
                        column: x => x.IconoCatalogoId,
                        principalTable: "IconosCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GruposEnsayo_IconoCatalogoId",
                table: "GruposEnsayo",
                column: "IconoCatalogoId");

            migrationBuilder.CreateIndex(
                name: "IX_GruposEnsayo_Nombre",
                table: "GruposEnsayo",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IconosCatalogo_Clave",
                table: "IconosCatalogo",
                column: "Clave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TiposEnsayo_GrupoEnsayoId_Nombre",
                table: "TiposEnsayo",
                columns: new[] { "GrupoEnsayoId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TiposEnsayo_IconoCatalogoId",
                table: "TiposEnsayo",
                column: "IconoCatalogoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TiposEnsayo");

            migrationBuilder.DropTable(
                name: "GruposEnsayo");

            migrationBuilder.DropTable(
                name: "IconosCatalogo");
        }
    }
}
