namespace CarPriceEstimator.DataGatherer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using AngleSharp.Html.Dom;
    using AngleSharp.Html.Parser;
    using CarPriceEstimator.DataGatherer.Models;
    using CarPriceEstimator.DataGatherer.Utils;

    public class CarMarketBgCarDataGatherer : ICarDataGatherer
    {
        private const int OffersPerPage = 15;
        private const string AdvancedSearchUri = "https://www.carmarket.bg/tarsene";
        private const string FirstPageUrl = "https://www.carmarket.bg/obiavi/?sort=1";
        private const string SpaceSeparator = " ";
        private const string OffersListLinkSelector = ".cmOffersListLink";
        private const string OfferDetailsSelector = "div.cmOfferMoreInfoRow strong";
        private const string MakeSelector = "section.cmOffer header h1";

        public async Task<IEnumerable<Car>> GatherData(HtmlParser parser, HttpClient client)
        {
            var cars = new List<Car>();
            var pagesCount = await this.GetPagesCount(parser, client);

            var makes = await GetMakesWithModels(client: client, parser: parser);
            var pages = (int)Math.Ceiling(a: pagesCount / (double)OffersPerPage);

            for (int page = 0; page < pages; page++)
            {
                var url = $"https://www.carmarket.bg/obiavi/{page}?sort=1";

                try
                {
                    var htmlContent = await HtmlHelpers.GetHtmlContent(client: client, url: url);

                    if (string.IsNullOrWhiteSpace(value: htmlContent))
                    {
                        break;
                    }

                    var document = await parser.ParseDocumentAsync(source: htmlContent);
                    var links = document.QuerySelectorAll(OffersListLinkSelector).Select(x => ((IHtmlAnchorElement)x).Href);

                    foreach (var link in links)
                    {
                        Console.WriteLine(value: $"Crawling from {link}");
                        var offerContent = await HtmlHelpers.GetHtmlContent(client: client, url: link);
                        var offerDom = await parser.ParseDocumentAsync(source: offerContent);

                        var offerDetails = offerDom.QuerySelectorAll(OfferDetailsSelector);

                        var priceRaw = offerDetails[0].TextContent;
                        var yearRaw = offerDetails[1].TextContent;
                        var fuelTypeRaw = offerDetails[2].TextContent;
                        var horsePowerRaw = offerDetails[3].TextContent;
                        var gearTypeRaw = offerDetails[4].TextContent;
                        var rangeRaw = offerDetails[6].TextContent;
                        var makeAndModelRaw = offerDom.QuerySelector(MakeSelector).TextContent;

                        var make = HtmlHelpers.ParseMakeAndModel(makeAndModelRaw, makesCollection: makes.Keys);
                        var model = HtmlHelpers.ParseMakeAndModel(makeAndModelRaw.Substring(startIndex: make.Length), makesCollection: makes[key: make]);

                        var car = new Car
                        {
                            Make = make,
                            Model = model,
                            FuelType = fuelTypeRaw,
                            GearType = gearTypeRaw,
                            Price = HtmlHelpers.ParseThousands(priceRaw, SpaceSeparator),
                            HorsePower = HtmlHelpers.ParseHorsePower(horsePowerRaw),
                            Range = HtmlHelpers.ParseThousands(rangeRaw, SpaceSeparator),
                            Year = HtmlHelpers.ParseYear(yearRaw)
                        };

                        cars.Add(car);
                    }
                }
                catch
                {
                    // If any data is not available just continue 
                }
            }

            return cars;
        }

        private async Task<int> GetPagesCount(HtmlParser parser, HttpClient client)
        {
            var htmlContent = await HtmlHelpers.GetHtmlContent(client: client, url: FirstPageUrl);
            var document = await parser.ParseDocumentAsync(source: htmlContent);

            var offersElement = document.QuerySelector("span.foundOffers strong").TextContent;
            var offersCount = HtmlHelpers.ParseThousands(offersElement,SpaceSeparator);


            return offersCount;
        }

        private static async Task<Dictionary<string, List<string>>> GetMakesWithModels(HttpClient client, HtmlParser parser)
        {
            var makes = new Dictionary<string, List<string>>();
            var htmlContent = await HtmlHelpers.GetHtmlContent(client: client, url: AdvancedSearchUri);
            var document = await parser.ParseDocumentAsync(source: htmlContent);

            var makeElement = document.GetElementById(elementId: "brand_id");
            var optionsGroups = makeElement.Children.Skip(count: 1).ToList();
            for (int i = 0; i < optionsGroups.Count(); i++)
            {
                var currentOptionsGroup = optionsGroups[index: i];

                foreach (var optionElement in currentOptionsGroup.Children)
                {
                    if (optionElement is IHtmlOptionElement currentOption)
                    {
                        var currentMake = currentOption.InnerHtml;

                        if (!makes.ContainsKey(key: currentMake))
                        {
                            makes.Add(key: currentMake, value: new List<string>());
                        }

                        var currentMakeId = int.Parse(currentOption.Value);
                        var currentMakeModels = GetModelsByMake(currentMakeId);
                        makes[currentMake].AddRange(currentMakeModels);
                    }
                }
            }

            return makes;
        }

        private static IEnumerable<string> GetModelsByMake(int makeId)
        {
            var modelsByMake = new Dictionary<int, List<string>>
            {
                {2, new List<string> {"CL", "Integra", "Mdx", "NSX", "Rdx", "Rl", "Rsx", "Slx", "Tl", "Tsx"}},
                {3, new List<string> {"400", "505", "600", "City", "Crossline", "Mega", "Roadline", "Scouty R"}},
                {
                    4,
                    new List<string>
                    {
                        "145", "146", "147", "149", "155", "156", "156 sportwagon", "159", "159 sportwagon", "159 SW",
                        "164", "166", "33", "75", "76", "8C", "8C Competizione", "90", "Alfasud", "Alfetta", "Brera",
                        "Crosswagon", "Crosswagon q4", "Giulia", "Giulietta", "Gt", "Gtv", "Junior", "MiTo", "RZ/SZ",
                        "Spider", "Sprint", "Sud"
                    }
                },
                {125, new List<string> {"B10", "B12", "B3", "B5", "B6", "B7", "B8", "D 10", "D3", "Roadster S"}},
                {5, new List<string> {"10", "24", "242", "243", "244", "246", "32", "320", "324", "328", "33", "461"}},
                {6, new List<string> {"Rocsta"}},
                {
                    7,
                    new List<string>
                    {
                        "AR1", "DB", "Db7", "Db9", "DBS", "Lagonda", "Rapide", "V12 Vantage", "V8", "V8 Vantage",
                        "Vanquish", "Vantage", "Virage", "Volante"
                    }
                },
                {
                    8,
                    new List<string>
                    {
                        "100", "200", "50", "60", "80", "90", "A1", "A2", "A3", "A3 Cabrio", "A3 Sportback", "A4",
                        "A4 Allroad", "A4 Avant", "A5", "A5 Sportback", "A6", "A6 Allroad", "A6 Avant", "A7", "A8",
                        "Allroad", "Q3", "Q5", "Q7", "R8", "RS2", "RS3", "Rs4", "Rs5", "Rs6", "S2", "S3",
                        "S3 Sportback", "S4", "S5", "S6", "S8", "Tt", "TT Roadster", "TTS", "V8"
                    }
                },
                {
                    9,
                    new List<string>
                        {"Allegro", "Ambassador", "Maestro", "Maxi", "Metro", "Mg", "Mini", "Montego", "Princess"}
                },
                {
                    13, new List<string>
                    {
                        "116", "118", "120", "123", "125", "130", "135", "1500", "1600", "1602", "1800", "2000", "2002",
                        "310", "315", "316", "318", "320", "323", "324", "325", "328", "330", "335", "501", "518",
                        "520", "523", "524", "525", "528", "530", "535", "540", "545", "550", "5 Gran Turismo", "5GT",
                        "628", "630", "633", "635", "640", "645", "650", "700", "721", "723", "725", "728", "730",
                        "732", "733", "735", "740", "745", "750", "750IL", "754", "760", "840", "850",
                        "Active Hybrid 7", "Bertone", "Izetta", "M1", "M3", "M5", "M6", "X1", "X3", "X5", "X5 M", "X6",
                        "X6 M", "Z1", "Z3", "Z3 M", "Z4", "Z4 M", "Z6", "Z8"
                    }
                },
                {
                    10,
                    new List<string>
                    {
                        "Arnage", "Azure", "Brooklands", "Continental", "Continental gt", "Eight", "Mulsanne", "T1",
                        "T-series", "Turbo R", "Turbo RT", "Turbo S"
                    }
                },
                {11, new List<string> {"Coupe"}},
                {12, new List<string> {"Freeclimber"}},
                {14, new List<string> {"Hansa", "Isabella"}},
                {126, new List<string> {"BC3", "BS2", "BS4", "BS6"}},
                {15, new List<string> {"EB 110", "Veyron"}},
                {
                    16,
                    new List<string>
                    {
                        "Century", "Electra", "Invicta", "Le Sabre", "Park avenue", "Regal", "Rendezvous", "Riviera",
                        "Roadmaster", "Skylark", "Skyline"
                    }
                },
                {
                    17,
                    new List<string>
                    {
                        "Allante", "BLC", "BLS", "Brougham", "Cts", "Deville", "Eldorado", "Escalade", "Fleetwood",
                        "Seville", "Srx", "STS", "Suburban", "Xlr"
                    }
                },
                {
                    18,
                    new List<string>
                    {
                        "2500", "Alero", "Astro", "Avalanche", "Aveo", "Beretta", "Blazer", "C1500", "Camaro",
                        "Caprice", "Captiva", "Cavalier", "Chevelle", "Chevy Van", "Citation", "Cobalt", "Colorado",
                        "Corsica", "Corvette", "Cruze", "Epica", "Equinox", "Evanda", "Express Passenger", "G", "Gmc",
                        "Hhr", "Impala", "K1500", "K30", "Kalos", "Lacetti", "Lumina", "Malibu", "Matiz", "Niva",
                        "Nova", "Nubira", "Orlando", "Rezzo", "S-10", "Silverado", "Spark", "Ssr", "Suburban", "Tacuma",
                        "Tahoe", "Tracker", "Trailblazer", "Transsport", "Venture", "Volt"
                    }
                },
                {
                    19,
                    new List<string>
                    {
                        "300c", "300m", "Cherokee", "Crossfire", "Daytona", "Es", "Grand cherokee", "Grand Voyager",
                        "Gr.voyager", "GS", "Gts", "Interpid", "Lebaron", "Neon", "New yorker", "Pacifica",
                        "Pt cruiser", "Saratoga", "Sebring", "Stratus", "Valiant", "Viper", "Vision", "Voyager",
                        "Wrangler"
                    }
                },
                {
                    20,
                    new List<string>
                    {
                        "2cv", "Ax", "Axel", "Berlingo", "BerlingoFT", "Bx", "C1", "C15", "C2", "C3", "C3 Picasso",
                        "C3 pluriel", "C4", "C4 NEW", "C4 Picasso", "C5", "C5 NEW", "C6", "C8", "C-Crosser", "C-Elysée",
                        "Cx", "Ds", "DS3", "DS4", "Evasion", "Grand C4 Picasso", "Gsa", "Gx", "Jumper", "Jumper II",
                        "Jumpy", "Jumpy II", "Ln", "Nemo", "Oltcit", "Saxo", "SM", "Visa", "Xantia", "Xm", "Xsara",
                        "Xsara picasso", "Zx"
                    }
                },
                {
                    21, new List<string>
                    {
                        "C06 Convertible", "C06 Coupe", "C3", "C4", "C5", "C6", "GS", "Powa", "Z06", "ZR 1"
                    }
                },
                {132, new List<string> {"400"}},
                {
                    22,
                    new List<string>
                    {
                        "1100", "1300", "1304", "1307", "1310", "1350", "Duster", "Liberta", "Logan", "Nova", "Pickup",
                        "Sandero", "Solenza"
                    }
                },
                {
                    23,
                    new List<string>
                    {
                        "Ace", "Chairman", "Cielo", "Damas", "Espero", "Evanda", "Fso", "Kalos", "Korando", "Lacetti",
                        "Lanos", "Leganza", "Magnus", "Matiz", "Musso", "Nexia", "Nubira", "Prince", "Racer", "Rezzo",
                        "Super", "Tacuma", "Tico"
                    }
                },
                {
                    24,
                    new List<string>
                    {
                        "Applause", "Charade", "Charmant", "Copen", "Cuore", "Feroza", "Feroza/Sportrak", "Freeclimber",
                        "Gran move", "Hijet", "Materia", "Move", "Rocky", "Rocky/Fourtrak", "Sharade", "Sirion", "Taft",
                        "Terios", "TREVIS", "Wildcat", "Yrv"
                    }
                },
                {25, new List<string> {"Double six", "Six", "Sovereign"}},
                {26, new List<string> {"Bluebird", "Cherry", "Stanza"}},
                {27, new List<string> {"F102"}},
                {
                    28,
                    new List<string>
                    {
                        "Avenger", "Caliber", "Caravan", "Challenger", "Charger", "Coronet", "Dakota", "Daytona",
                        "Demon", "Durango", "Grand Caravan", "Hornet", "Interpid", "Journey", "Magnum", "Neon", "Nitro",
                        "Ram", "Shadow", "Stealth", "Stratus", "Viper"
                    }
                },
                {29, new List<string> {"Premire", "Talon", "Vision"}},
                {133, new List<string> {"V"}},
                {30, new List<string> {"Polonez"}},
                {
                    31,
                    new List<string>
                    {
                        "208", "246", "250", "275", "288", "308", "328", "330", "348", "360", "360 modena",
                        "360 spider", "365", "400", "412", "456", "458 Italia", "512", "550", "575", "599", "599 GTB",
                        "612", "750", "California", "Daytona", "Dino GT4", "Enzo", "Enzo Ferrari", "F355", "F40",
                        "F430", "F456m", "F50", "F575m maranello", "F612 scaglietti", "FF", "Mondial", "Mondial 8",
                        "Superamerica", "Testarossa"
                    }
                },
                {
                    32,
                    new List<string>
                    {
                        "1100", "124", "125", "126", "127", "128", "130", "131", "132", "1400", "1500", "1800", "500",
                        "600", "650", "750", "Albea", "Argenta", "Barchetta", "Bertone", "Brava", "Bravo", "Campagnola",
                        "Cinquecento", "Coupe", "Croma", "Dino", "Doblo", "Ducato", "Duna", "Fiorino", "Grande Punto",
                        "Idea", "Linea", "Marea", "Marengo", "Multipla", "Palio", "Panda", "Punto", "Regata", "Ritmo",
                        "Scudo", "Sedici", "Seicento", "Siena", "Spider Europa", "Stilo", "Strada", "Tavria", "Tempra",
                        "Tipo", "Topolino", "Ulysse", "Uno", "X 1/9"
                    }
                },
                {
                    33,
                    new List<string>
                    {
                        "12m", "15m", "17m", "20m", "Aerostar", "Bronco", "Capri", "C-max", "Connect", "Consul",
                        "Cortina", "Cosworth", "Cougar", "Countur", "Courier", "Crown", "Crown victoria", "Ecoline",
                        "Econoline", "Econovan", "Edge", "Escape", "Escort", "Everest", "Excursion", "Expedition",
                        "Explorer", "Express", "F150", "F250", "F350", "F450", "F550", "F650", "F750", "Fairlane",
                        "Falcon", "Fiesta", "Focus", "Focus c max", "Focus C-Max", "Fusion", "Galaxy", "Granada", "GT",
                        "Ka", "Kuga", "Maverick", "Mercury", "Mondeo", "Mustang", "Orion", "Probe", "Puma", "Ranger",
                        "Rs", "Scorpio", "Sierra", "S-Max", "Sportka", "Streetka", "Taunus", "Taurus", "Thunderbird",
                        "Tourneo", "Transit", "Windstar", "Zephyr"
                    }
                },
                {144, new List<string> {"XPC"}},
                {
                    34,
                    new List<string>
                    {
                        "13 ЧАЙКА", "14 ЧАЙКА", "2310", "2705", "27057", "2705 COMBI", "2752", "2752 COMBI", "3302",
                        "33023", "330232", "33027", "330273", "331070", "469", "69", "ГАЗЕЛА", "М 21", "Собол"
                    }
                },
                {35, new List<string> {"Metro", "Prizm", "Storm", "Tracker"}},
                {
                    36,
                    new List<string>
                    {
                        "Envoy", "Jimmy", "Safari", "Saturn", "Savana", "Sierra", "Sonoma", "Syclone", "Tracker",
                        "Typhoon", "Vandura", "Yukon "
                    }
                },
                {38, new List<string> {"Hover Cuv", "Hover H5", "Safe", "Steed 5", "Voleex C10"}},
                {143, new List<string> {"KLQ6840Q"}},
                {
                    39,
                    new List<string>
                    {
                        "Accord", "Aerodeck", "Cbr", "Cbx", "City", "Civic", "Civic ballade", "Concerto", "Cr-v", "Crx",
                        "Crz", "CR-Z", "Element", "Fit", "Fr-v", "Hr-v", "Insight", "Integra", "Jazz", "Legend", "Logo",
                        "Nsx", "Odyssey", "Passport", "Prelude", "Quintet", "Ridgeline", "S2000", "Shuttle", "Stream"
                    }
                },
                {138, new List<string> {"Super snipe"}},
                {40, new List<string> {"H1", "H2", "H3 "}},
                {
                    41,
                    new List<string>
                    {
                        "Accent", "Atos", "Coupe", "Elantra", "Excel", "FF", "Galloper", "Genesis", "Getz", "Grace",
                        "Grandeur", "H-1", "H 100", "H-1 Starex", "H 200", "I10", "I20", "I30", "i30 CW", "I40", "i50",
                        "Ix20", "IX35", "IX55", "Lantra", "Matrix", "Pony", "Porter", "S", "Santa fe", "Santamo",
                        "S-Coupe", "Sonata", "Stelar", "Tb", "Terracan", "Trajet", "Tucson", "Veloster", "Xg", "XG 30",
                        "XG 350"
                    }
                },
                {42, new List<string> {"F9"}},
                {
                    43,
                    new List<string>
                    {
                        "EX", "Ex30", "Ex35", "Ex37", "FX", "Fx 30", "Fx 35", "Fx 37", "Fx45", "Fx 45", "Fx 50", "G",
                        "G20", "G35", "G37", "G coupe", "G sedan", "I", "J", "M", "Q", "Q45", "Qx", "Qx4", "QX56"
                    }
                },
                {44, new List<string> {"Mini"}},
                {
                    45,
                    new List<string>
                    {
                        "Amigo", "Campo", "D-max", "Gemini", "Midi", "Piazza", "Pickup", "Rodeo", "Tfs", "Trooper",
                        "Vehi cross "
                    }
                },
                {
                    134,
                    new List<string>
                    {
                        "23010", "2.5", "260", "2.8", "30-8", "35", "3510", "3512", "35-8", "35c11", "35c13", "35s11",
                        "35s13", "4010", "4012", "4510", "4910", "4912", "5010", "5080", "50-9", "50s13", "59-12",
                        "60-11", "60-12", "6510", "6512", "7410", "7918", "80", "8013", "9013", "Classic", "Daily",
                        "Massif", "Turbo", "Uni "
                    }
                },
                {
                    46,
                    new List<string>
                    {
                        "Daimler", "Daimler double six", "Daimler six", "E Type", "MK II", "Sovereign", "S-type",
                        "S Type", "Super v8", "Xf", "Xj", "XJ12", "XJ40", "XJ6", "XJ8", "Xjr", "Xjs", "Xjsc", "XK",
                        "Xk8", "Xkr", "XKR-S", "X Type "
                    }
                },
                {
                    47,
                    new List<string>
                    {
                        "Cherokee", "CJ", "Comanche", "Commander", "Compass", "Grand cherokee", "Liberty", "Patriot",
                        "Renegade", "Wagoneer", "Willys", "Wrangler "
                    }
                },
                {48, new List<string> {"Montez "}},
                {
                    49,
                    new List<string>
                    {
                        "Avella delta", "Besta", "Borrego", "Carens", "Carnival", "Ceed", "Cerato", "Clarus", "Elan",
                        "Joecs", "Joice", "Joyce", "K2500", "K2700", "K2900", "Leo", "Magentis", "Mentor", "Mini",
                        "Mohave", "Opirus", "Optima", "Picanto", "Pregio", "Pride", "pro_ceed", "Retona", "Rio",
                        "Roadster", "Rocsta", "Sedona", "Sephia", "Shuma", "Sorento", "Soul", "Spectra", "Sportage",
                        "Venga "
                    }
                },
                {127, new List<string> {"CCR "}},
                {145, new List<string> {"Convoy "}},
                {
                    50,
                    new List<string>
                    {
                        "1200", "1300", "1500", "1600", "2101", "21011", "21012", "21013", "21015", "2102", "2103",
                        "2104", "21043", "2105", "21051", "21053", "2106", "21061", "21063", "2107", "21074", "2108",
                        "21083", "2109", "21093", "21099", "2110", "21213", "Kalina", "Niva", "Nova", "Oka", "Priora",
                        "Samara "
                    }
                },
                {51, new List<string> {"Magnum "}},
                {
                    52,
                    new List<string>
                    {
                        "Countach", "Diablo", "Espada", "Gallardo", "Jalpa", "LM", "Miura", "Murcielago", "Reventon",
                        "Urraco "
                    }
                },
                {
                    53,
                    new List<string>
                    {
                        "A112", "Aurelia", "Beta", "Dedra", "Delta", "Flaminia", "Fulvia", "Gamma", "Kappa", "Lybra",
                        "Musa", "Phedra", "Prisma", "Stratos", "Thema", "Thesis", "Unior", "Y", "Y10", "Ypsilon",
                        "Zeta "
                    }
                },
                {
                    54,
                    new List<string>
                    {
                        "Defender", "Discovery", "Discovery 3", "Freelander", "Land Rover I", "Land Rover II",
                        "Land Rover III", "Range rover", "Range Rover Evoque", "Range Rover Sport "
                    }
                },
                {55, new List<string> {"Jx6476da "}},
                {
                    56,
                    new List<string>
                    {
                        "CT200h", "Es", "ES350", "Gs", "GS300", "GS350", "GS430", "GS450", "GS460", "Gx470", "Is",
                        "IS200", "IS220", "IS250", "IS300", "IS-F", "LH470", "Ls", "LS400", "LS430", "LS460", "LS600",
                        "Lx", "LX470", "LX570", "Rx", "Rx300", "RX330", "Rx350", "RX400", "Rx400h", "Rx450", "Sc",
                        "SC400", "SC430 "
                    }
                },
                {57, new List<string> {"LF1010", "LF320", "LF520", "LF620", "LF6361", "LF7130", "LF7160", "X60 "}},
                {128, new List<string> {"Ambra", "Nova", "Optima", "X - Too "}},
                {
                    58,
                    new List<string>
                    {
                        "Aviator", "Continental", "Ls", "Mark", "Mark lt", "Mark Lt", "Mkx", "Mkz", "Navigator",
                        "Town car", "Zephyr "
                    }
                },
                {
                    59,
                    new List<string>
                    {
                        "340 R", "Cortina", "Elan", "Elise", "Elite", "Esprit", "Europa", "Europe", "Evora", "Excel",
                        "Exige", "Super Seven", "V8 "
                    }
                },
                {60, new List<string> {"Armada", "Bolero", "Cl", "Commander", "Goa", "Marshall", "Scorpio "}},
                {
                    61,
                    new List<string>
                    {
                        "222", "224", "228", "3200", "3200 gt", "418", "420", "4200", "422", "424", "430", "Biturbo",
                        "Coupe gt", "Ghibli", "GranCabrio", "Gransport", "GranTurismo", "Granturismo S", "Indy",
                        "Karif", "MC12", "Merak", "Quattroporte", "Quattroporte S", "Quattroporte Sport GT S", "Shamal",
                        "Spyder", "Zagato "
                    }
                },
                {62, new List<string> {"Murena", "Rancho "}},
                {63, new List<string> {"57", "62 "}},
                {
                    64,
                    new List<string>
                    {
                        "121", "2", "3", "323", "5", "5 NEW", "6", "626", "6 NEW", "929", "B2200", "B2500", "B2600",
                        "Bongo", "B series", "BT-50", "Cx-7", "CX 7", "Cx-9", "CX 9", "Demio", "E series", "Millenia",
                        "Mpv", "Mx-3", "Mx-5", "Mx-6", "Premacy", "Protege", "RX-6", "Rx-7", "Rx-8", "Tribute", "Xedos "
                    }
                },
                {
                    65,
                    new List<string>
                    {
                        "0405", "110", "111", "113", "114", "115", "116", "123", "124", "126", "126-260", "150", "170",
                        "180", "190", "200", "220", "230", "240", "250", "260", "270", "280", "290", "300", "307",
                        "308", "309", "310", "320", "350", "380", "380se", "400", "405 G", "410", "416", "420", "450",
                        "500", "560", "600", "609", "711", "A140", "A150", "A160", "A170", "A180", "A190", "A200",
                        "A210", "Adenauer", "B150", "B170", "B180", "B200", "C 160", "C180", "C200", "C220", "C230",
                        "C240", "C250", "C270", "C280", "C 300", "C30 AMG", "C320", "C32 AMG", "C350", "C36 AMG",
                        "C 43 AMG", "C55 AMG", "C63 AMG", "CE 200", "CE 220", "CE 300", "CL", "CL 180", "CL 200",
                        "CL 220", "CL 230", "CL 420", "CL 500", "CL55 AMG", "CL 600", "CL63 AMG", "CL65 AMG", "CLC 180",
                        "CLC 200", "CLC 220", "CLC 230", "CLC 350", "CLK", "CLK 200", "CLK 220", "CLK 230", "CLK 240",
                        "CLK 270", "CLK 280", "CLK 320", "CLK 350", "CLK 430", "CLK 500", "CLK55 AMG", "CLK63 AMG",
                        "CLS", "CLS 250", "CLS 280", "CLS320", "CLS350", "CLS500", "CLS55", "CLS55 AMG", "CLS63",
                        "CLS63 AMG", "E200", "E220", "E230", "E240", "E250", "E260", "E270", "E280", "E290", "E300",
                        "E320", "E350", "E36 AMG", "E400", "E420", "E430", "E 50", "E500", "E50 AMG", "E55", "E55 AMG",
                        "E60", "E60 AMG", "E63", "E63 AMG", "G 230", "G 240", "G 250", "G 270", "G 280", "G 290",
                        "G 300", "G 320", "G350", "G36 AMG", "G400", "G 500", "G55 AMG", "G63 AMG", "Gl", "GL 320",
                        "GL 350", "GL 420", "GL 450", "GL 500", "GL 55 AMG", "GL 63 AMG", "Glk", "GLK 200", "GLK 220",
                        "GLK 250", "GLK 280", "GLK 320", "GLK 350", "MB 100", "Ml", "ML 230", "ML 250", "ML 270",
                        "ML 280", "ML 320", "ML 350", "ML 400", "ML 420", "ML 430", "ML 450", "ML 500", "Ml55 AMG",
                        "Ml63 AMG", "R280", "R300", "R320", "R350", "R500", "R63 AMG", "S250", "S 260", "S280", "S300",
                        "S320", "S350", "S400", "S420", "S430", "S450", "S500", "S 55", "S550", "S55 AMG", "S600",
                        "S63", "S63 AMG", "S65", "S65 AMG", "Sl", "SL 280", "SL 300", "SL 320", "SL 350", "SL 380",
                        "SL 420", "SL 450", "SL 500", "Sl55 AMG", "SL 560", "SL 600", "Sl60 AMG", "Sl63 AMG",
                        "Sl65 AMG", "SL 70 AMG", "SL 73 AMG", "Slk", "SLK 200", "SLK 230", "SLK 250", "SLK 280",
                        "SLK 320", "Slk32 AMG", "SLK 350", "Slk55 AMG", "SLR", "SLS", "SLS 63", "Sls AMG", "Smart",
                        "Sprinter", "SSK Gazelle", "V 200", "V 220", "V230", "V 280", "Vaneo", "Vario", "Viano", "Vito "
                    }
                },
                {66, new List<string> {"Marauder", "Milan", "Monarch", "Mountaineer", "Villager "}},
                {
                    67,
                    new List<string> {"Mga", "Mgb", "Mgf", "Midget", "Montego", "TD", "Tf", "Zr", "Zs", "Zt", "Zt-t "}
                },
                {139, new List<string> {"Virgo "}},
                {
                    68,
                    new List<string>
                    {
                        "1000", "1300", "Clubman", "Cooper", "Cooper cabrio", "Cooper s", "Cooper s cabrio",
                        "Countryman", "D one", "John Cooper Works", "One", "One cabrio "
                    }
                },
                {
                    69,
                    new List<string>
                    {
                        "3000 gt", "ASX", "Canter", "Carisma", "Colt", "Cordia", "Cosmos", "Diamante", "Eclipse",
                        "Galant", "Galloper", "Grandis", "L200", "L300", "L400", "Lancer", "Mirage", "Montero",
                        "Outlander", "Pajero", "Pajero pinin", "Pajero sport", "Pick-up", "Raider", "Santamo",
                        "Sapporo", "Sigma", "Space gear", "Space runner", "Space star", "Space wagon", "Starion",
                        "Tredia "
                    }
                },
                {70, new List<string> {"Aero8", "Plus 4", "Plus 8", "Roadster "}},
                {
                    71,
                    new List<string>
                    {
                        "1360", "1361", "1500", "2136", "2138", "2140", "2141", "21412", "21417", "2142", "2715", "401",
                        "403", "407", "408", "412", "426", "427", "503", "Aleko", "Иж "
                    }
                },
                {142, new List<string> {"Prinz "}},
                {
                    72,
                    new List<string>
                    {
                        "100 nx", "200 sx", "240 SX", "240 z", "280 z", "280 ZX", "300 zx", "350z", "370Z", "Almera",
                        "Almera tino", "Altima", "Armada", "Bluebird", "Cabstar", "Cargo", "Cedric", "Cherry",
                        "Frontier", "Gt-r", "GTR", "Interstar", "Juke", "King Cab", "Kubistar", "Laurel", "Maxima",
                        "Micra", "Murano", "Navara", "Note", "NP300", "Pathfinder", "Patrol", "Pickup", "Pixo",
                        "Prairie", "Primastar", "Primera", "Qashqai", "Qashqai+2", "Quest", "Rogue", "Sentra", "Serena",
                        "Silvia", "Skyline", "Stantza", "Sunny", "Terrano", "Tiida", "Titan", "Titan crew cab",
                        "Titan king", "Trade", "Urvan", "Vanette", "Versa", "Xterra", "X-Terra", "X-trail "
                    }
                },
                {
                    73,
                    new List<string>
                    {
                        "Achieva", "Alero", "Aurora", "Bravada", "Custom Cruiser", "Cutlass", "Delta 88", "Firenza",
                        "Intrigue", "Regency", "Silhouette", "Supreme", "Toronado "
                    }
                },
                {129, new List<string> {"Club "}},
                {
                    74,
                    new List<string>
                    {
                        "Admiral", "Agila", "Antara", "Arena", "Ascona", "Astra", "Calibra", "Campo", "Cavalier",
                        "Combo", "Commodore", "Corsa", "Diplomat", "Frontera", "Gt", "Insignia", "Kadett", "Kapitaen",
                        "Manta", "Meriva", "Monterey", "Monza", "Movano", "New Astra", "New Meriva", "Nova", "Omega",
                        "Pick Up Sportscap", "Rekord", "Senator", "Signum", "Sintra", "Speedster", "Tigra", "Vectra",
                        "Vivaro", "Zafira", "Zafira Tourer "
                    }
                },
                {75, new List<string> {"Kancil", "Kelisa", "Kembara", "Kenari", "Nippa", "Rusa "}},
                {
                    76,
                    new List<string>
                    {
                        "1007", "104", "106", "107", "202", "204", "205", "206", "206 Plus", "207", "3008", "304",
                        "305", "306", "307", "308", "309", "4007", "402", "403", "404", "405", "406", "407", "5008",
                        "504", "505", "508", "604", "605", "607", "806", "807", "Bipper", "Boxer", "Expert", "iOn",
                        "J5", "Karsan", "Partner", "Ranch", "Range", "RCZ "
                    }
                },
                {77, new List<string> {"Cevennes", "Speedster "}},
                {130, new List<string> {"Porter "}},
                {
                    78,
                    new List<string>
                    {
                        "Acclaim", "Barracuda", "Breeze", "Colt", "Grand voyager", "Horizon", "Laser", "Neon",
                        "Prowler", "Reliant", "Road runner", "Sundance", "Volare", "Voyager "
                    }
                },
                {79, new List<string> {"Pickup "}},
                {
                    80,
                    new List<string>
                    {
                        "6000", "Aztec", "Bonneville", "Fiero", "Firebird", "G6", "Grand am", "Grand-Am", "Grand prix",
                        "Grand-Prix", "Gto", "Lemans", "Montana", "Solstice", "Sunbird", "Sunfire", "Targa", "Tempest",
                        "Trans am", "Trans sport", "Vibe "
                    }
                },
                {
                    81,
                    new List<string>
                    {
                        "356", "911", "911 Carrera C2", "911 Carrera C4S Cab", "911 Turbo Cabrio", "912", "914", "924",
                        "928", "935", "944", "956", "959", "962", "968", "993", "996", "997", "Boxster", "Boxter S",
                        "Carrera", "Carrera GT", "Cayenne", "Cayman", "Panamera "
                    }
                },
                {82, new List<string> {"400", "Persone", "Satria "}},
                {140, new List<string> {"Evoque", "Range Rover", "Sport "}},
                {
                    83,
                    new List<string>
                    {
                        "10", "11", "12", "14", "16", "18", "19", "20", "21", "25", "29", "30", "4", "5", "8", "9",
                        "Alpine", "Avantime", "Bakara", "Bulgar", "Captur", "Chamade", "Clio", "Espace", "Express",
                        "Fluence", "Fuego", "Grand espace", "Grand Modus", "Grand scenic", "Kangoo", "Koleos", "Laguna",
                        "Laguna Coupe", "Latitude", "Mascott", "Master", "Megane", "Modus", "Nevada", "P 1400", "Rapid",
                        "Safrane", "Scenic", "Scenic rx4", "Spider", "Symbol", "Trafic", "Twingo", "Vel satis "
                    }
                },
                {
                    84,
                    new List<string>
                    {
                        "Corniche", "Flying Spur", "Ghost", "Park Ward", "Phantom", "Silver Cloud", "Silver Dawn",
                        "Silver Seraph", "Silver Shadow", "Silver Spirit", "Silver Spur", "Silver Wraith "
                    }
                },
                {
                    85,
                    new List<string>
                    {
                        "100", "111", "114", "115", "200", "213", "214", "216", "218", "220", "25", "400", "414", "416",
                        "418", "420", "45", "600", "618", "620", "623", "75", "800", "820", "825", "827", "City",
                        "City Rover", "Estate", "Maestro", "Metro", "Mini", "Montego", "SD", "Streetwise "
                    }
                },
                {86, new List<string> {"Ceo "}},
                {135, new List<string> {"1939 "}},
                {87, new List<string> {"90", "900", "9000", "9-3", "9-3x", "9-4X", "9-5", "96", "9-7x", "99 "}},
                {88, new List<string> {"LX "}},
                {146, new List<string> {"300 "}},
                {89, new List<string> {"Astra", "Aura", "Outlook", "Sky", "Vue "}},
                {90, new List<string> {"Tc", "Xa", "Xb", "xD "}},
                {
                    91,
                    new List<string>
                    {
                        "Alhambra", "Altea", "Arosa", "Cordoba", "Exeo", "Fura", "Ibiza", "Inca", "Inka", "Leon",
                        "Malaga", "Marbella", "Ronda", "Terra", "Toledo", "Vario "
                    }
                },
                {136, new List<string> {"315 HD", "S 215 HD "}},
                {92, new List<string> {"Stella "}},
                {93, new List<string> {"Ceo", "Noble "}},
                {
                    94,
                    new List<string>
                    {
                        "1307", "1308", "1309", "1510", "Aront", "Chrysler", "Horizon", "Shambord", "Solara", "Special",
                        "Versail "
                    }
                },
                {
                    95,
                    new List<string>
                    {
                        "100", "1000", "105", "120", "125", "130", "135", "136", "Fabia", "Favorit", "Felicia",
                        "Forman", "Octavia", "Pick-up", "Praktik", "Rapid", "Roomster", "Superb", "Yeti "
                    }
                },
                {96, new List<string> {"Forfour", "Fortwo", "Mc", "Micro", "Roadster "}},
                {
                    98,
                    new List<string>
                    {
                        "Actyon", "Actyon Sports", "Chairman", "Family", "Korando", "Kyron", "Musso", "Rexton",
                        "Rodius "
                    }
                },
                {
                    99,
                    new List<string>
                    {
                        "1800", "B9 tribeca", "Baja", "E12", "Forester", "G3x justy", "Impreza", "Justy", "Legacy",
                        "Libero", "Outback", "Rex", "Svx", "Trezia", "Vivio", "XT "
                    }
                },
                {
                    100,
                    new List<string>
                    {
                        "Alto", "Baleno", "Cappuccino", "Carry", "Forenza", "Grand vitara", "Ignis", "Jimny", "Liana",
                        "LJ", "Maruti", "New Swift", "Reno", "Samurai", "Santana", "Sg", "Sidekick", "Sj", "SJ Samurai",
                        "Splash", "Super-Carry", "Swift", "SX4", "Vitara", "Wagon r", "Wagon R+", "X-90", "XL7", "XL-7 "
                    }
                },
                {131, new List<string> {"Chimaera", "Griffith", "Tuscan "}},
                {101, new List<string> {"1100", "1310", "Horizon", "Matra", "Murena", "Samba", "Simka", "Solara "}},
                {
                    102,
                    new List<string>
                        {"Aria", "Estate", "Indica", "Mint", "Nano", "Safari", "Sierra", "Sumo", "Telcoline "}
                },
                {103, new List<string> {"Dana", "Kombi", "Slavuta "}},
                {147, new List<string> {"EM1", "Zero "}},
                {104, new List<string> {"Gurkha", "Judo", "Traveller 2.4 D "}},
                {105, new List<string> {"Fl2850", "Sl3000 "}},
                {106, new List<string> {"Model S", "Roadster", "Roadster Sport "}},
                {107, new List<string> {"Dogan", "Kartal", "Sahin "}},
                {
                    108,
                    new List<string>
                    {
                        "4runner", "4-Runner", "Auris", "Avalon", "Avensis", "Avensis verso", "Aygo", "Camry", "Carina",
                        "Celica", "Corolla", "Corolla verso", "Cressida", "Crown", "Dyna", "FJ", "Fj cruiser", "Hiace",
                        "Highlander", "Hilux", "IQ", "Land cruiser", "Lite-Ace", "Matrix", "Mr2", "New Auris",
                        "New Yaris", "Paseo", "Picnic", "Previa", "Prius", "Rav4", "Scion", "Sequoia", "Sienna",
                        "Starlet", "Supra", "Tacoma", "Tercel", "Tundra", "Urban Cruiser", "Venza", "Verso", "Yaris",
                        "Yaris verso "
                    }
                },
                {109, new List<string> {"600", "601", "Combi", "T 1.1 "}},
                {
                    110,
                    new List<string>
                    {
                        "Acclaim", "Dolomite", "Herald", "Moss", "Spitfire", "Stag", "TR3", "TR4", "TR5", "Tr6", "Tr7",
                        "TR8"
                    }
                },
                {111, new List<string> {"452", "460", "469", "669", "69", "Hunter", "Patriot"}},
                {112, new List<string> {"22", "24", "3110", "3111", "M 20", "M 21", "Siber"}},
                {
                    113,
                    new List<string>
                    {
                        "142", "144", "145", "164", "1800 es", "240", "244", "245", "262", "262 c", "264", "340", "343",
                        "344", "345", "360", "440", "460", "480", "66", "740", "744", "745", "760", "765", "770", "780",
                        "850", "855", "940", "944", "945", "960", "965", "Amazon", "C30", "C70", "P 1800", "Polar",
                        "S40", "S60", "S70", "S80", "S90", "V40", "V50", "V60", "V70", "V90", "XC60", "Xc70", "Xc90"
                    }
                },
                {
                    114,
                    new List<string>
                    {
                        "1200", "1300", "1302", "1303", "1500", "1600", "181", "Amarok", "Amarok DoubleCab", "Beetle",
                        "Bora", "Buggy", "Caddy", "Caddy Kasten", "Caddy Kombi", "Caddy Life", "Caddy Maxi Kasten",
                        "Caddy Maxi Kombi", "Caddy Maxi Life", "Caravelle", "Corrado", "Country", "Crafter",
                        "Crafter Kasten", "Derby", "Eos", "Fox", "Golf", "Golf Plus", "Golf Variant", "Iltis", "Jetta",
                        "K 70", "Karmann-ghia", "Karmann Ghia", "Krafter DOKA", "Krafter EKA", "LT", "Lupo", "Multivan",
                        "New beetle", "Passat", "Passat CC", "Passat Variant", "Phaeton", "Polo", "Rabbit", "Santana",
                        "Scirocco", "Sharan", "T1", "T2", "T3", "T3 Caravelle", "T3 Multivan", "T4", "T4 Caravelle",
                        "T4 Multivan", "T5", "T5 Caravelle", "T5 Multivan", "T5 Shuttle", "Taro", "Tiguan", "Touareg",
                        "Touran", "Transporter", "Transporter Kombi", "up!", "Vento"
                    }
                },
                {116, new List<string> {"1.3", "311", "312", "353"}},
                {117, new List<string> {"Gt", "MF 25", "MF 28", "Mf3", "MF 30", "MF 35", "Mf4", "Mf5"}},
                {118, new List<string> {"1021d", "1021ls", "1021s", "2021d", "2021s"}},
                {119, new List<string> {"XS-D055"}},
                {120, new List<string> {"600", "750", "Florida", "Gt 55", "Koral", "Miami", "Yugo 45"}},
                {121, new List<string> {"1102", "1103", "1105", "965", "966", "968", "Tavria"}},
                {141, new List<string> {"21213"}},
                {137, new List<string> {"223", "232"}},
                {122, new List<string> {"М"}},
                {123, new List<string> {"С"}},
                {124, new List<string> {"М"}}
            };

            return modelsByMake[makeId];
        }
    }
}