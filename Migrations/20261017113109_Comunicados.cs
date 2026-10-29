using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class Comunicados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ComunicadosPlazoDias",
                table: "Configuracion",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComunicadosRespondeEntrada",
                table: "Configuracion",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComunicadosRespondeSalida",
                table: "Configuracion",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CargasComunicados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NombreArchivo = table.Column<string>(type: "text", nullable: false),
                    Hoja = table.Column<string>(type: "text", nullable: true),
                    FilaEncabezados = table.Column<int>(type: "integer", nullable: true),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FilasCreadas = table.Column<int>(type: "integer", nullable: false),
                    FilasActualizadas = table.Column<int>(type: "integer", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CargasComunicados", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Comunicados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tipo = table.Column<string>(type: "text", nullable: true),
                    Remitente = table.Column<string>(type: "text", nullable: true),
                    Sentido = table.Column<string>(type: "text", nullable: true),
                    Consecutivo = table.Column<string>(type: "text", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Descripcion = table.Column<string>(type: "text", nullable: true),
                    RequiereRespuesta = table.Column<bool>(type: "boolean", nullable: true),
                    ConsecutivoRespuesta = table.Column<string>(type: "text", nullable: true),
                    FechaRespuesta = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ResponsableRespuesta = table.Column<string>(type: "text", nullable: true),
                    Observaciones = table.Column<string>(type: "text", nullable: true),
                    SugerenciaDescartada = table.Column<string>(type: "text", nullable: true),
                    CargaComunicadosId = table.Column<int>(type: "integer", nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comunicados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Comunicados_CargasComunicados_CargaComunicadosId",
                        column: x => x.CargaComunicadosId,
                        principalTable: "CargasComunicados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AdjuntosComunicado",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ComunicadoId = table.Column<int>(type: "integer", nullable: false),
                    Clase = table.Column<string>(type: "text", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Ruta = table.Column<string>(type: "text", nullable: false),
                    FechaCarga = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdjuntosComunicado", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdjuntosComunicado_Comunicados_ComunicadoId",
                        column: x => x.ComunicadoId,
                        principalTable: "Comunicados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdjuntosComunicado_ComunicadoId",
                table: "AdjuntosComunicado",
                column: "ComunicadoId");

            migrationBuilder.CreateIndex(
                name: "IX_Comunicados_CargaComunicadosId",
                table: "Comunicados",
                column: "CargaComunicadosId");

            migrationBuilder.CreateIndex(
                name: "IX_Comunicados_Consecutivo",
                table: "Comunicados",
                column: "Consecutivo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdjuntosComunicado");

            migrationBuilder.DropTable(
                name: "Comunicados");

            migrationBuilder.DropTable(
                name: "CargasComunicados");

            migrationBuilder.DropColumn(
                name: "ComunicadosPlazoDias",
                table: "Configuracion");

            migrationBuilder.DropColumn(
                name: "ComunicadosRespondeEntrada",
                table: "Configuracion");

            migrationBuilder.DropColumn(
                name: "ComunicadosRespondeSalida",
                table: "Configuracion");
        }
    }
}
