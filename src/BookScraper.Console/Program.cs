var baseUrl = "https://books.toscrape.com/";
var scraper = new ScraperService(baseUrl, 5);
await scraper.StartAsync();
