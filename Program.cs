using Microsoft.EntityFrameworkCore;
using SigWeb.Components;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
// Fechas en toda la app como día/mes/año (16/10/2026): se clona la cultura del equipo (números
// y separadores siguen igual) y solo se cambian los patrones de fecha.
{
    var cultura = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.CurrentCulture.Clone();
    cultura.DateTimeFormat.ShortDatePattern = "dd/MM/yyyy";
    cultura.DateTimeFormat.DateSeparator = "/";
    cultura.DateTimeFormat.ShortTimePattern = "HH:mm";
    cultura.DateTimeFormat.LongTimePattern = "HH:mm:ss";
    System.Globalization.CultureInfo.DefaultThreadCurrentCulture = cultura;
    System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = cultura;
    System.Globalization.CultureInfo.CurrentCulture = cultura;
    System.Globalization.CultureInfo.CurrentUICulture = cultura;
}

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<SigWeb.Data.Iconos.IconosApp>();
builder.Services.AddSingleton<SigWeb.Data.Catalogos.EstilosCatalogo>();
// Dossier: solicitudes de descarga pendientes (la pantalla las deja, /api/dossier/{token} las genera).
builder.Services.AddSingleton<SigWeb.Data.Dossier.AlmacenDossier>();
builder.Services.AddDbContext<SigWeb.Data.AppDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Papelera de reciclaje: aquí se registra cada módulo que tenga función de
// eliminar. Reciclaje.razor pide "dame todos los IProveedorPapelera
// registrados" y el contenedor de servicios le entrega esta lista.
builder.Services.AddScoped<SigWeb.Data.Impacto.AnalizadorImpacto>();
builder.Services.AddScoped<SigWeb.Data.Riesgos.MatrizRiesgosServicio>();
builder.Services.AddScoped<SigWeb.Data.Planes.PlanesAccionServicio>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraMateriales>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraComponentes>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraElementosTrazado>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraArchivosRegistroParametro>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraArchivosComponente>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraPlanoMaestro>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraArchivosPlanoMaestro>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraResultadosEnsayo>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraCamposExtraTorre>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraCamposExtraComponente>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraGruposEnsayo>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraTiposEnsayo>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraDefinicionesCatalogo>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraGruposPlano>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraActividades>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraTiros>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraParametros>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraVersionesMatrizRiesgos>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraRiesgos>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraSeguimientosRiesgo>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraPlanesAccion>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraAccionesPlan>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraAnexosAccion>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraArchivosRiesgo>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraCamposTabla>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraDocumentosMaestro>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraCargasDocumentos>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraCargasArchivosDocumentos>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraCargasPlanos>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraCargasArchivosPlanos>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraVersionesDocumento>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraAdjuntosMaterial>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraCargasMateriales>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraCargasPlanesAccion>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraSeguimientosAccion>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraArchivosEquipo>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraCarpetasPlan>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraArchivosCarpetaPlan>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraComunicados>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraAdjuntosComunicado>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraCargasComunicados>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraIndicadores>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraArchivosIndicador>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraCargasEquipos>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraInformesDiarios>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraRegistrosAvance>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraFotosAvance>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraActividadesObra>();

// Trazado (KMZ): cada módulo que permita vincular sus registros a una
// torre/pata se registra aquí como IProveedorUbicacion.
// Materiales ya no va aquí: la ficha lo muestra en su bloque "Materiales" (Archivos), con adjuntos.
// builder.Services.AddScoped<SigWeb.Data.Ubicacion.IProveedorUbicacion, SigWeb.Data.Ubicacion.ProveedorUbicacionMateriales>();

builder.Services.Configure<Microsoft.AspNetCore.SignalR.HubOptions>(options => { options.MaximumReceiveMessageSize = 20 * 1024 * 1024; });

var app = builder.Build();

// Si el proyecto todavía no tiene actividades configuradas, siembra las
// 7 actividades típicas (Replanteo, Civil, SPT, Montaje, Tendido, Pruebas
// Conductor, Pruebas FO) con parámetros de ejemplo. Solo corre una vez;
// después todo es editable desde el módulo Actividades.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SigWeb.Data.AppDbContext>();
    // Los 6 catálogos de fábrica (antes una lista fija en el código):
    // debe ir primero porque Catalogos.razor ahora lee la lista de la BD.
    SigWeb.Data.Catalogos.SembradorDefinicionesCatalogo.SembrarSiHaceFalta(db);
    SigWeb.Data.Riesgos.SembradorCatalogosRiesgo.SembrarDefiniciones(db);
    // Separa en elementos las causas, consecuencias, etc. numeradas de los
    // riesgos cargados antes de esta función. Solo toca los que no tienen.
    SigWeb.Data.Riesgos.ElementosRiesgo.ConvertirPendientes(db);
    // Las listas desplegables de riesgos dependen de Catálogos: aquí se
    // suben a su catálogo los valores ya usados (incluidos los cargos
    // responsables, uno por línea). Seguro de repetir.
    SigWeb.Data.Riesgos.SembradorCatalogosRiesgo.SincronizarConRiesgos(db);
    // Planes de acción: el lugar único de antes pasa a la lista de lugares.
    SigWeb.Data.Planes.PlanesAccionServicio.MigrarLugarUnico(db);
    SigWeb.Data.Planes.PlanesAccionServicio.MigrarRiesgoUnico(db);
    // Hallazgos: el lugar único de antes pasa a la lista de lugares del hallazgo.
    SigWeb.Data.Planes.PlanesAccionServicio.MigrarLugarHallazgos(db);
    SigWeb.Data.Trazado.SembradorActividades.SembrarSiVacio(db);
    SigWeb.Data.Trazado.SembradorActividadesPata.SembrarSiHaceFalta(db);
    SigWeb.Data.Trazado.SembradorCapaInicial.SembrarSiHaceFalta(db);
    SigWeb.Data.Trazado.SembradorHojaDeVida.SembrarSiHaceFalta(db);
    // Galería de íconos y módulo Ensayos: la de íconos debe ir primero,
    // el sembrador de Ensayos busca sus íconos por Clave.
    SigWeb.Data.Ensayos.SembradorIconosCatalogo.SembrarSiHaceFalta(db);
    SigWeb.Data.Ensayos.SembradorEnsayos.SembrarSiHaceFalta(db);
    SigWeb.Data.Planos.SembradorGruposPlano.SembrarSiHaceFalta(db);
    SigWeb.Data.Equipos.SembradorCamposEquipo.SembrarSiHaceFalta(db);
    // Deja el catálogo "Localización" al día con las torres ya cargadas
    // (incluye las 147 anteriores a esta función). Segura de repetir en
    // cada arranque: solo crea o actualiza lo que falte.
    SigWeb.Data.Trazado.SincronizadorCatalogoLocalizacion.SincronizarTodas(db);
    // Enlaza (o adopta/backfillea) la Actividad de cada Tipo de Ensayo en
    // Trazado. Debe ir después de SembradorActividadesPata (para poder
    // adoptar las 2 actividades de fábrica) y de SembradorEnsayos (para
    // que ya existan los Tipos de Ensayo). Segura de repetir en cada
    // arranque.
    SigWeb.Data.Trazado.SincronizadorActividadesEnsayo.SincronizarTodos(db);
    // Listado maestro de documentos: catálogos de tipo documental/estado, cargas de las
    // importaciones anteriores (sus filas quedan como "Formatos") y estado traído del Excel.
    SigWeb.Data.Documentos.MigracionDocumentos.Ejecutar(db);
    // Listado maestro de planos: importaciones y archivos anteriores quedan agrupados en cargas.
    SigWeb.Data.Planos.MigracionPlanos.Ejecutar(db);
    // Materiales: PDF/fotos → adjuntos clasificados y columnas fijas → Campos.
    SigWeb.Data.ListadoMateriales.MigracionMateriales.Ejecutar(db);
    // Indicadores: la primera vez, los de la matriz de indicadores del SGI (con ene/feb-26 congelados).
    SigWeb.Data.Indicadores.SembradorIndicadores.Ejecutar(db);
    // Avance de obra: la primera vez, las actividades del informe diario F-054 con sus pesos.
    SigWeb.Data.AvanceObra.SembradorActividadesObra.SembrarSiVacio(db);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// .ifc (modelos BIM) y .wasm (motor web-ifc) no están en la lista de tipos por defecto.
var tiposEstaticos = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
tiposEstaticos.Mappings[".ifc"] = "application/x-step";
tiposEstaticos.Mappings[".wasm"] = "application/wasm";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = tiposEstaticos });

app.UseAntiforgery();

app.MapStaticAssets();
// Buscador rápido (Ctrl + K del menú): resultados en JSON para js/navegacion.js.
app.MapGet("/api/buscar", async (string? q, SigWeb.Data.AppDbContext db) =>
    Results.Json(await SigWeb.Data.Busqueda.BuscadorGlobal.BuscarAsync(db, q)));
// Dossier: el ZIP se escribe directo en la respuesta (streaming); si el usuario cancela la descarga,
// RequestAborted detiene la generación. ZipArchive escribe su índice final de forma síncrona.
app.MapGet("/api/dossier/{token}", async (string token, HttpContext ctx, SigWeb.Data.Dossier.AlmacenDossier almacen,
    SigWeb.Data.AppDbContext db, IWebHostEnvironment env, ILogger<Program> log) =>
{
    var s = almacen.Obtener(token);
    if (s is null) { ctx.Response.StatusCode = StatusCodes.Status404NotFound; return; }
    var sync = ctx.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpBodyControlFeature>();
    if (sync is not null) sync.AllowSynchronousIO = true;
    ctx.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>()?.DisableBuffering();
    var nombre = SigWeb.Data.Dossier.GeneradorDossier.NombreZip(s.Nombre);
    ctx.Response.ContentType = "application/zip";
    ctx.Response.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
    { FileName = "\"" + new string(nombre.Select(ch => ch < 128 ? ch : '_').ToArray()) + "\"", FileNameStar = nombre }.ToString();
    try { await SigWeb.Data.Dossier.GeneradorDossier.GenerarAsync(ctx.Response.Body, s, db, env.WebRootPath, ctx.RequestAborted); }
    catch (OperationCanceledException) { }
    catch (IOException) when (ctx.RequestAborted.IsCancellationRequested) { }   // el navegador cortó la descarga
    catch (Exception ex)
    {
        log.LogError(ex, "No se pudo generar el dossier");
        // Ya se enviaron bytes: se corta la conexión para que el navegador marque la descarga como fallida.
        if (ctx.Response.HasStarted) ctx.Abort();
        else { ctx.Response.Headers.ContentDisposition = default; ctx.Response.StatusCode = StatusCodes.Status500InternalServerError; }
    }
});
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();