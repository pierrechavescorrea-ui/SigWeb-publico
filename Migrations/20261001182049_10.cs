using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class _10 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DefinicionesCatalogo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    EntidadAsociada = table.Column<string>(type: "text", nullable: false),
                    EsPredeterminado = table.Column<bool>(type: "boolean", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DefinicionesCatalogo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ValoresCampoCatalogoItem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemCatalogoId = table.Column<int>(type: "integer", nullable: false),
                    Clave = table.Column<string>(type: "text", nullable: false),
                    Valor = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValoresCampoCatalogoItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ValoresCampoCatalogoItem_Catalogos_ItemCatalogoId",
                        column: x => x.ItemCatalogoId,
                        principalTable: "Catalogos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CamposCatalogoDefinicion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DefinicionCatalogoId = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CamposCatalogoDefinicion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CamposCatalogoDefinicion_DefinicionesCatalogo_DefinicionCat~",
                        column: x => x.DefinicionCatalogoId,
                        principalTable: "DefinicionesCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CamposCatalogoDefinicion_DefinicionCatalogoId_Nombre",
                table: "CamposCatalogoDefinicion",
                columns: new[] { "DefinicionCatalogoId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DefinicionesCatalogo_Nombre",
                table: "DefinicionesCatalogo",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ValoresCampoCatalogoItem_ItemCatalogoId_Clave",
                table: "ValoresCampoCatalogoItem",
                columns: new[] { "ItemCatalogoId", "Clave" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CamposCatalogoDefinicion");

            migrationBuilder.DropTable(
                name: "ValoresCampoCatalogoItem");

            migrationBuilder.DropTable(
                name: "DefinicionesCatalogo");
        }
    }
}
