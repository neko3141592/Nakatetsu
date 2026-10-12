using MagicOnion;
using MagicOnion.Server;
using Nakatetsu.TrafficControl.Server.Communication.Unity;
using Nakatetsu.TrafficControl.Server.Communication.Web;
using Nakatetsu.TrafficControl.Server.CTC;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Nakatetsu.TrafficControl.Server.Timetable.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("Timetable")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Timetable が設定されていません。");

var sqlite = new SqliteConnectionStringBuilder(connectionString);

sqlite.DataSource = Path.GetFullPath(
    sqlite.DataSource,
    builder.Environment.ContentRootPath);

Directory.CreateDirectory(
    Path.GetDirectoryName(sqlite.DataSource)!);

// DbContextを作成するFactoryをDIに登録する
builder.Services.AddDbContextFactory<TimetableDbContext>(
    options => options.UseSqlite(sqlite.ToString()));


builder.Services.AddMagicOnion();

builder.Services.AddSingleton<UnitySnapshotStore>();
builder.Services.AddSingleton<TimetableStore>();
builder.Services.AddSingleton<TimetableMasterStore>();
builder.Services.AddSingleton<CTCController>();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

CTCEndpoint.MapRoutes(app);

app.MapMagicOnionService();

app.Run();