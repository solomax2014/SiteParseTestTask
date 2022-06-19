using System;
using System.IO;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using AngleSharp;

namespace SiteParseTestTask {
    public class Worker {
        private static IBrowsingContext context = BrowsingContext.New(Configuration.Default.WithDefaultLoader());

        public static void Main(string[] args) => new Worker().AsyncWorker().GetAwaiter().GetResult();

        private async Task AsyncWorker() {
            Console.WriteLine("Собираем ссылки на товары...");
            var page = 0;
            var pageUrl = "https://www.toy.ru/catalog/boy_transport/?count=45&filterseccode%5B0%5D=transport";
            var productList = new List<string>();
            while (true) {
                page++;
                Console.WriteLine($"    Страница {page}");
                var doc = await context.OpenAsync(pageUrl);
                var productUrls = doc.QuerySelectorAll("div.row.mt-2 > div > div.product-card > div > div > a.product-name.gtm-click").Select(x => $"https://www.toy.ru{x.GetAttribute("href")}").ToList();
                //productUrls.ForEach(x => Console.WriteLine(x));
                productList.AddRange(productUrls);

                var nextPageEl = doc.QuerySelectorAll("ul.pagination > li > a").Where(x => x.TextContent == "След.");
                pageUrl = nextPageEl.Any() ? nextPageEl.Select(x => $"https://www.toy.ru{x.GetAttribute("href")}").First() : null;
                if (pageUrl == null)
                    break;
            }
        }
    }
}