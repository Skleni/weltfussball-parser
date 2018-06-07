using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace WeltfussballParser
{
    class Program
    {
        const string eventName = "wm-2018-in-russland";
        const string qualificationLinkText = "wm-quali";

        static async Task Main(string[] args)
        {
            var startDate = new DateTime(2018, 6, 14);

            Console.WriteLine("Loading players...");
            Console.WriteLine();

            var players = await LoadPlayersAsync();

            Console.WriteLine();
            Console.WriteLine("Writing CSV file...");
            Console.WriteLine();

            await WriteCsvFileAsync(players, startDate);

            Console.WriteLine("Finished");
        }        

        private static async Task<IEnumerable<Player>> LoadPlayersAsync()
        {
            var players = new List<Player>();

            var client = new HttpClient()
            {
                BaseAddress = new Uri("http://www.weltfussball.at")
            };

            int page = 1;
            while (true)
            {
                var response = await client.GetAsync($"/spielerliste/{eventName}/nach-name/{page}");

                var document = new HtmlDocument();
                document.LoadHtml(await response.Content.ReadAsStringAsync());

                var th = from e in document.DocumentNode.Descendants("th")
                         where e.InnerText == "Spieler"
                         select e;

                var table = th.Single().Ancestors("table").Single();

                var count = players.Count;
                players.AddRange(GetPlayers(table));
                if (players.Count == count)
                {
                    break;
                }

                for (int i = count; i < players.Count; i++)
                {
                    Console.WriteLine("Loading " + players[i].Name);
                    response = await client.GetAsync(players[i].DetailsUrl);
                    document = new HtmlDocument();
                    document.LoadHtml(await response.Content.ReadAsStringAsync());

                    UpdateStatistics(players[i], document);

                    var url = GetQualificationStatisticsUrl(document);
                    if (url != null)
                    {
                        response = await client.GetAsync(url);
                        document.LoadHtml(await response.Content.ReadAsStringAsync());
                        UpdateQualificationStatistics(players[i], document);
                    }
                }

                page++;
            }

            return players;
        }

        private static IEnumerable<Player> GetPlayers(HtmlNode table)
        {
            var players = from tr in table.Descendants("tr").Skip(1)
                          where tr.Elements("td").Count() >= 6
                          select GetPlayer(tr);

            return players;
        }

        private static Player GetPlayer(HtmlNode tr)
        {
            var tds = tr.Elements("td");
            var a = tds.ElementAt(0).Element("a");
            return new Player()
            {
                Name = a.InnerText.Trim(),
                DetailsUrl = a.GetAttributeValue("href", null),
                Nation = tds.ElementAt(2).Element("a").InnerText,
                DateOfBirth = DateTime.Parse(tds.ElementAt(3).InnerText),
                Position = tds.ElementAt(5).InnerText
            };
        }

        private static string GetQualificationStatisticsUrl(HtmlDocument document)
        {
            var table = GetTableByH2(document, "Länderspiele");

            return table?.Descendants("a").Select(a => a.GetAttributeValue("href", string.Empty)).FirstOrDefault(h => h.Contains("spieler_profil") && h.Contains(qualificationLinkText));
        }

        private static void UpdateStatistics(Player player, HtmlDocument document)
        {
            var table = GetTableByH2(document, "Vereinsstationen als Spieler");
            player.Team = table.Descendants("b").FirstOrDefault()?.InnerText;
            player.TeamStatistics = GetStatistics(document, "Vereinsspiele");
            player.NationStatistics = GetStatistics(document, "Länderspiele");
            player.QualificationStatistics = new Statistics();
        }



        private static void UpdateQualificationStatistics(Player player, HtmlDocument document)
        {
            var table = document.DocumentNode.Descendants("table").FirstOrDefault(t => t.HasClass("standard_tabelle"));
            var tr = table?.Descendants("tr").ElementAtOrDefault(1);

            if (tr != null)
            {
                var tds = tr.Descendants("td");
                player.QualificationStatistics.Matches = int.Parse(tds.ElementAt(4).InnerText);
                player.QualificationStatistics.Goals = int.Parse(tds.ElementAt(5).InnerText);
                player.QualificationStatistics.StartingEleven = int.Parse(tds.ElementAt(6).InnerText);
                player.QualificationStatistics.In = int.Parse(tds.ElementAt(7).InnerText);
                player.QualificationStatistics.Out = int.Parse(tds.ElementAt(8).InnerText);
                player.QualificationStatistics.YellowCards = int.Parse(tds.ElementAt(9).InnerText);
                player.QualificationStatistics.YellowRedCards = int.Parse(tds.ElementAt(10).InnerText);
                player.QualificationStatistics.RedCards = int.Parse(tds.ElementAt(11).InnerText);
            }
        }

        private static Statistics GetStatistics(HtmlDocument document, string title)
        {
            var table = GetTableByH2(document, title);
            if (table == null)
            { 
                return new Statistics();
            }

            var tr = table.Descendants("tr").FirstOrDefault(e => e.InnerText.Contains("Alle " + title));
            if (tr == null)
            { 
                return new Statistics();
            }

            var bs = tr.Descendants("b");
            return new Statistics()
            {
                Matches = int.Parse(bs.ElementAt(1).InnerText),
                Goals = int.Parse(bs.ElementAt(2).InnerText),
                StartingEleven = int.Parse(bs.ElementAt(3).InnerText),
                In = int.Parse(bs.ElementAt(4).InnerText),
                Out = int.Parse(bs.ElementAt(5).InnerText),
                YellowCards = int.Parse(bs.ElementAt(6).InnerText),
                YellowRedCards = int.Parse(bs.ElementAt(7).InnerText),
                RedCards = int.Parse(bs.ElementAt(8).InnerText)
            };
        }

        private static HtmlNode GetTableByH2(HtmlDocument document, string text)
        {
            var h2 = document.DocumentNode.Descendants("h2").SingleOrDefault(n => n.InnerText == text);
            if (h2 == null)
            {
                return null;
            }
                
            var div = h2.ParentNode.ParentNode;
            var table = div.Descendants("table").First();

            return table;
        }

        private static async Task WriteCsvFileAsync(IEnumerable<Player> players, DateTime startDate)
        {
            using (var writer = new StreamWriter(File.Open("players.csv", FileMode.Create), Encoding.UTF8))
            {
                await writer.WriteLineAsync("Name\tNation\tVerein\tPosition\tAlter\tSpiele (Quali)\tTore (Quali)\tStartelf (Quali)\tEin (Quali)\tAus (Quali)\tGelb (Quali)\tGelb-rot (Quali)\tRot (Quali)5\tSpiele\tTore\tStartelf\tEin\tAus\tGelb\tGelb-rot\tRot\tSpiele (V)\tTore (V)\tStartelf (V)\tEinwechslungen (V)\tAuswechslungen (V)\tGelb (V)\tGelb-rot (V)\tRot (V)");
                foreach (var player in players)
                {
                    writer.WriteLine(
                        string.Join("\t",
                        player.Name.Trim(),
                        player.Nation.Trim(),
                        player.Team.Trim(),
                        player.Position.Trim(),
                        (int)((startDate - player.DateOfBirth).TotalDays / 365),
                        player.QualificationStatistics.Matches,
                        player.QualificationStatistics.Goals,
                        player.QualificationStatistics.StartingEleven,
                        player.QualificationStatistics.In,
                        player.QualificationStatistics.Out,
                        player.QualificationStatistics.YellowCards,
                        player.QualificationStatistics.YellowRedCards,
                        player.QualificationStatistics.RedCards,
                        player.NationStatistics.Matches,
                        player.NationStatistics.Goals,
                        player.NationStatistics.StartingEleven,
                        player.NationStatistics.In, player.NationStatistics.Out, player.NationStatistics.YellowCards,
                        player.NationStatistics.YellowRedCards,
                        player.NationStatistics.RedCards,
                        player.TeamStatistics.Matches,
                        player.TeamStatistics.Goals,
                        player.TeamStatistics.StartingEleven,
                        player.TeamStatistics.In,
                        player.TeamStatistics.Out,
                        player.TeamStatistics.YellowCards,
                        player.TeamStatistics.YellowRedCards,
                        player.TeamStatistics.RedCards));
                }
            }
        }
    }
}
