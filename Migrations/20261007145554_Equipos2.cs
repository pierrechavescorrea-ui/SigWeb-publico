using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class Equipos2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CamposEquipo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Clave = table.Column<string>(type: "text", nullable: false),
                    Etiqueta = table.Column<string>(type: "text", nullable: false),
                    TipoDato = table.Column<int>(type: "integer", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    EditableEnVistaExcel = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CamposEquipo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Equipos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    PeriodicidadMeses = table.Column<int>(type: "integer", nullable: true),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FotosEquipo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EquipoId = table.Column<int>(type: "integer", nullable: false),
                    RutaArchivo = table.Column<string>(type: "text", nullable: false),
                    FechaCarga = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FotosEquipo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FotosEquipo_Equipos_EquipoId",
                        column: x => x.EquipoId,
                        principalTable: "Equipos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntervencionesEquipo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EquipoId = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ProximaFecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Responsable = table.Column<string>(type: "text", nullable: true),
                    NumeroCertificado = table.Column<string>(type: "text", nullable: true),
                    Descripcion = table.Column<string>(type: "text", nullable: true),
                    RutaCertificado = table.Column<string>(type: "text", nullable: true),
                    NombreCertificado = table.Column<string>(type: "text", nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntervencionesEquipo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntervencionesEquipo_Equipos_EquipoId",
                        column: x => x.EquipoId,
                        principalTable: "Equipos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ValoresCampoEquipo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EquipoId = table.Column<int>(type: "integer", nullable: false),
                    CampoEquipoId = table.Column<int>(type: "integer", nullable: false),
                    Valor = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValoresCampoEquipo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ValoresCampoEquipo_CamposEquipo_CampoEquipoId",
                        column: x => x.CampoEquipoId,
                        principalTable: "CamposEquipo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ValoresCampoEquipo_Equipos_EquipoId",
                        column: x => x.EquipoId,
                        principalTable: "Equipos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CamposEquipo_Clave",
                table: "CamposEquipo",
                column: "Clave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FotosEquipo_EquipoId",
                table: "FotosEquipo",
                column: "EquipoId");

            migrationBuilder.CreateIndex(
                name: "IX_IntervencionesEquipo_EquipoId_Fecha",
                table: "IntervencionesEquipo",
                columns: new[] { "EquipoId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_ValoresCampoEquipo_CampoEquipoId",
                table: "ValoresCampoEquipo",
                column: "CampoEquipoId");

            migrationBuilder.CreateIndex(
                name: "IX_ValoresCampoEquipo_EquipoId_CampoEquipoId",
                table: "ValoresCampoEquipo",
                columns: new[] { "EquipoId", "CampoEquipoId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FotosEquipo");

            migrationBuilder.DropTable(
                name: "IntervencionesEquipo");

            migrationBuilder.DropTable(
                name: "ValoresCampoEquipo");

            migrationBuilder.DropTable(
                name: "CamposEquipo");

            migrationBuilder.DropTable(
                name: "Equipos");
        }
    }
}
