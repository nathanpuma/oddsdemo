using DotNetEnv;
using System.Text.Json;


class Program
{
    static decimal MoneylineToDecimal(decimal moneyline)
    {

        return moneyline >= 0m
            ? 1m + (moneyline / 100m)
            : 1m + (100m / Math.Abs(moneyline));
    }

    static async Task Main()
    {

        Env.Load();

        var apiKey = Environment.GetEnvironmentVariable("ODDS_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.WriteLine("Api key is not working");
            return;
        }
        Console.WriteLine("Api key is working");

        var url = "https://api.the-odds-api.com/v4/sports/basketball_nba/odds/?" + $"apiKey={apiKey}&regions=us&markets=h2h,spreads&oddsFormat=american";

        using var http = new HttpClient();

        var response = await http.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"HTTP {(int)response.StatusCode} - {response.ReasonPhrase}");
            Console.WriteLine(await response.Content.ReadAsStringAsync());
            return;
        }

        if (response.Headers.TryGetValues("x-requests-used", out var used))
            Console.WriteLine($"Used: {used.First()}");
        if (response.Headers.TryGetValues("x-requests-remaining", out var rem))
            Console.WriteLine($"Remaining: {rem.First()}");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var eventsArray = doc.RootElement;

        Console.WriteLine("NBA Odds H2H");
        int shown = 0;
        foreach (var ev in eventsArray.EnumerateArray())
        {
            if (shown++ > 5) break;

            var away_team = ev.GetProperty("away_team").GetString();
            var home_team = ev.GetProperty("home_team").GetString();
            var start = ev.GetProperty("commence_time").GetDateTime();

            Console.WriteLine($"\n{away_team} vs {home_team} at {start:yyyy-MM-dd HH:mm}");

            if (!ev.TryGetProperty("bookmakers", out var books) || books.GetArrayLength() == 0)
            {
                Console.WriteLine("No bookmakers available for this game");
                continue;
            }

            var book = books[0];
            var bookmaker = book.GetProperty("title").GetString();
            var last_update = book.GetProperty("last_update").GetDateTime();

            var markets = book.GetProperty("markets");
            JsonElement? h2hMarket = null;
            foreach (var m in markets.EnumerateArray())
            {
                if (m.GetProperty("key").GetString() == "h2h") { h2hMarket = m; break; }
            }

            if (h2hMarket is null)
            {
                Console.WriteLine($"{bookmaker} has no h2h market");
                continue;
            }

            Console.WriteLine($"Book: {bookmaker} (updated {last_update:yyyy-MM-dd HH:mm})");

            var outcomes = h2hMarket.Value.GetProperty("outcomes");
            foreach (var o in outcomes.EnumerateArray())
            {
                var teamName = o.GetProperty("name").GetString();
                var teamPrice = o.GetProperty("price").GetDecimal();
                var teamPriceDecimal = MoneylineToDecimal(teamPrice);

                Console.WriteLine($"{teamName,-25} odds: {teamPriceDecimal}");
            }
        }

        Console.WriteLine("Done");
    }
}