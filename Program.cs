using System;
using System.IO;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Io;

namespace SiteParseTestTask {
    public class Worker {
        private static HttpClient httpClient = new HttpClient();
        private static IBrowsingContext context = BrowsingContext.New();
        private static string cityId = "61000001000";

        public static void Main(string[] args) => new Worker().AsyncWorker().GetAwaiter().GetResult();

        private async Task AsyncWorker() {
            Console.WriteLine("Собираем ссылки на товары...");
            var page = 0;
            var pageUrl = "https://www.toy.ru/catalog/boy_transport/?count=45&filterseccode%5B0%5D=transport";
            var productList = new List<string>(){};
            while (true) {
                page++;
                Console.WriteLine($"    Страница {page}");
                var html = await GetCookedResp(pageUrl);
                var doc = await context.OpenAsync(x => x.Content(html));
                var productUrls = doc.QuerySelectorAll("div.row.mt-2 > div > div.product-card > div > div > a.product-name.gtm-click").Select(x => $"https://www.toy.ru{x.GetAttribute("href")}").ToList();
                //productUrls.ForEach(x => Console.WriteLine(x));
                productList.AddRange(productUrls);

                var nextPageEl = doc.QuerySelectorAll("ul.pagination > li > a").Last();
                pageUrl = nextPageEl.TextContent.EndsWith('.') ? $"https://www.toy.ru{nextPageEl.GetAttribute("href")}" : null;
                if (pageUrl == null)
                    break;
            }

            var csvRows = new List<string>(){"region,breadcrumbs,title,price,oldPrice,available,images,url"};
            var concurrency = new SemaphoreSlim(8);
            var taskList = productList.Select(url => Task.Run(async () => {
                await concurrency.WaitAsync();
                var csvRow = await ProcessProduct(url);
                csvRows.Add(csvRow);
                concurrency.Release();
            })).ToArray();
            Task.WaitAll(taskList);
            //var result = String.Join("\n", csvRows);
            //Console.WriteLine(result);
            File.WriteAllLines("products.csv", csvRows);
            Console.WriteLine($"Результат записан в файл products.csv, товаров: {productList.Count}");
        }

        private async Task<string> ProcessProduct(string url) {
            Console.WriteLine($"Обрабатываем {url}");
            var html = await GetCookedResp(url);
            var doc = await context.OpenAsync(x => x.Content(html));
            var region = doc.QuerySelector("div.select-city-link > a")?.TextContent.Trim();
            var breadcrumbs = String.Join(" > ", doc.QuerySelector("nav.breadcrumb")?.Children.Where(x => x.TextContent != "Вернуться").Select(x => x.TextContent).ToList()).Replace(',', ';');
            var title = doc.QuerySelector("h1.detail-name")?.TextContent.Replace(',', ';');
            var price = doc.QuerySelector("span.price")?.TextContent;
            var oldPrice = doc.QuerySelector("span.old-price")?.TextContent ?? "";
            var available = doc.QuerySelectorAll("div.detail-block > div > div.py-2").Last()?.TextContent.Trim();
            var images = String.Join(";", doc.QuerySelectorAll("div.detail-image img").Select(x => x.GetAttribute("src")).ToList());
            var csvRow = $"{region},{breadcrumbs},{title},{price},{oldPrice},{available},{images},{url}";
            //Console.WriteLine(csvRow);
            return csvRow;
        }

        public static async Task<string> GetCookedResp(string url) {
            var req = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
            req.Headers.Add("Cookie", $"BITRIX_SM_country=; BITRIX_SM_country_zip=; BITRIX_SM_country_name=; BITRIX_SM_country_city=; BITRIX_SM_country_fias=; BITRIX_SM_city={cityId}");
            var resp = await httpClient.SendAsync(req);
            var str = await resp.Content.ReadAsStringAsync();
            return str;
        }
    }
}