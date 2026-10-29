using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class CamposTabla : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CamposTabla",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tabla = table.Column<string>(type: "text", nullable: false),
                    Clave = table.Column<string>(type: "text", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Origen = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Calculado = table.Column<bool>(type: "boolean", nullable: false),
                    Formula = table.Column<string>(type: "text", nullable: true),
                    CatalogoTipo = table.Column<string>(type: "text", nullable: true),
                    Opciones = table.Column<string>(type: "text", nullable: true),
                    Decimales = table.Column<int>(type: "integer", nullable: true),
                    Visible = table.Column<bool>(type: "boolean", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CamposTabla", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FormatosCondicionales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CampoTablaId = table.Column<int>(type: "integer", nullable: false),
                    Operador = table.Column<int>(type: "integer", nullable: false),
                    Valor1 = table.Column<string>(type: "text", nullable: true),
                    Valor2 = table.Column<string>(type: "text", nullable: true),
                    Estilo = table.Column<string>(type: "text", nullable: false),
                    ColorTexto = table.Column<string>(type: "text", nullable: true),
                    ColorFondo = table.Column<string>(type: "text", nullable: true),
                    Negrita = table.Column<bool>(type: "boolean", nullable: false),
                    Forma = table.Column<string>(type: "text", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormatosCondicionales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormatosCondicionales_CamposTabla_CampoTablaId",
                        column: x => x.CampoTablaId,
                        principalTable: "CamposTabla",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ValoresCampoTabla",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CampoTablaId = table.Column<int>(type: "integer", nullable: false),
                    FilaId = table.Column<int>(type: "integer", nullable: false),
                    Valor = table.Column<string>(type: "text", nullable: true),
                    Formula = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValoresCampoTabla", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ValoresCampoTabla_CamposTabla_CampoTablaId",
                        column: x => x.CampoTablaId,
                        principalTable: "CamposTabla",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CamposTabla_Tabla_Clave",
                table: "CamposTabla",
                columns: new[] { "Tabla", "Clave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormatosCondicionales_CampoTablaId",
                table: "FormatosCondicionales",
                column: "CampoTablaId");

            migrationBuilder.CreateIndex(
                name: "IX_ValoresCampoTabla_CampoTablaId_FilaId",
                table: "ValoresCampoTabla",
                columns: new[] { "CampoTablaId", "FilaId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FormatosCondicionales");

            migrationBuilder.DropTable(
                name: "ValoresCampoTabla");

            migrationBuilder.DropTable(
                name: "CamposTabla");
        }
    }
}
