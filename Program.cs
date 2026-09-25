using AngleSharp.Html.Parser;
using System.Text.RegularExpressions;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(o => o.AddPolicy("all", p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddHttpClient();

var app = builder.Build();
app.UseCors("all");

// Главная страница — отдаём index.html
app.MapGet("/", () => Results.Content(File.ReadAllText("index.html"), "text/html"));

// Список студентов — читаем students.json
app.MapGet("/api/students", () =>
{
    if (!File.Exists("students.json")) return Results.Json(new List<object>());
    var json = File.ReadAllText("students.json");
    return Results.Content(json, "application/json");
});

// Расписание по группе
app.MapGet("/api/schedule/{group}", async (string group, IHttpClientFactory http) =>
{
    var client = http.CreateClient();
    var url = $"https://www.polessu.by/ruz/?f=1&q={Uri.EscapeDataString(group)}";
    string html;
    try { html = await client.GetStringAsync(url); }
    catch (Exception e) { return Results.Json(new { error = e.Message }); }

    var parser = new HtmlParser();
    var doc = parser.ParseDocument(html);

    // Собираем сырые строки: [ "Понедельник", "(Неделя) Предмет", ... ] и т.д.
    var rawRows = new List<List<string>>();
    foreach (var tr in doc.QuerySelectorAll("tr"))
    {
        var cells = tr.QuerySelectorAll("td, th")
                      .Select(td => td.TextContent.Trim())
                      .ToList();
        if (cells.Count > 0 && cells.Any(c => !string.IsNullOrWhiteSpace(c)))
            rawRows.Add(cells);
    }

    // Парсим в структуру: { day -> { time -> [ {subject, weeks, room, teacher, sub} ] } }
    var schedule = new Dictionary<string, Dictionary<string, List<object>>>();
    string? currentDay = null;

    var days = new[] { "Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота", "Воскресенье" };

    foreach (var row in rawRows)
    {
        // Пропускаем строку-заголовок "(Неделя) Предмет" и т.п.
        if (row.Count > 1 && row[1] == "(Неделя) Предмет")
        {
            currentDay = row[0];
            if (!schedule.ContainsKey(currentDay))
                schedule[currentDay] = new();
            continue;
        }

        // Если встретили название дня в первой ячейке
        if (row.Count >= 1 && days.Contains(row[0]))
        {
            currentDay = row[0];
            if (!schedule.ContainsKey(currentDay))
                schedule[currentDay] = new();
            continue;
        }

        if (currentDay == null || row.Count < 2) continue;

        var time = row[0];       // "8:30-9:50"
        var subjectRaw = row[1]; // "(2) Современная политэкономия - лек."
        var room = row.Count > 2 ? row[2] : "";
        var teacher = row.Count > 3 ? row[3] : "";
        var subGroup = row.Count > 4 ? row[4] : null;

        // Парсим недели из скобок: "(3-4, 7)" -> [3,4,7]; "(5)" -> [5]; если нет скобок -> []
        var weeks = new List<int>();
        var m = Regex.Match(subjectRaw, @"^\(([\d,\s\-]+)\)");
        if (m.Success)
        {
            foreach (var part in m.Groups[1].Value.Split(','))
            {
                var p = part.Trim();
                if (p.Contains('-'))
                {
                    var range = p.Split('-');
                    if (range.Length == 2 &&
                        int.TryParse(range[0].Trim(), out var a) &&
                        int.TryParse(range[1].Trim(), out var b))
                    {
                        for (int i = a; i <= b; i++) weeks.Add(i);
                    }
                }
                else if (int.TryParse(p, out var n)) weeks.Add(n);
            }
        }

        // Убираем "(...) " из предмета
        var subject = Regex.Replace(subjectRaw, @"^\([\d,\s\-]+\)\s*", "").Trim();

        if (!schedule[currentDay].ContainsKey(time))
            schedule[currentDay][time] = new();

        schedule[currentDay][time].Add(new
        {
            subject,
            weeks,
            room,
            teacher,
            sub = subGroup
        });
    }

    return Results.Json(schedule);
});

app.Run();