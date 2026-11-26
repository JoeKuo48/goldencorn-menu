using System;
using System.Collections.Generic;
using System.Linq;
using GoldenCornOrder.Models;
using Microsoft.EntityFrameworkCore;

namespace GoldenCornOrder.Data
{
    public static class DbInitializer
    {
        public static void Initialize(AppDbContext context)
        {
            context.Database.EnsureCreated();

            // Auto-migrate: Ensure "StockQuantity" column exists on MenuItems table
            try
            {
                var provider = context.Database.ProviderName ?? "";
                if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
                {
                    context.Database.ExecuteSqlRaw(@"ALTER TABLE ""MenuItems"" ADD COLUMN IF NOT EXISTS ""StockQuantity"" integer NULL;");
                }
                else
                {
                    try
                    {
                        context.Database.ExecuteSqlRaw(@"ALTER TABLE ""MenuItems"" ADD COLUMN ""StockQuantity"" INTEGER NULL;");
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DB Migration Note: {ex.Message}");
            }

            // 1. Categories
            var catPlates = context.Categories.FirstOrDefault(c => c.Name == "美式餐盤");
            if (catPlates == null)
            {
                catPlates = new Category
                {
                    Name = "美式餐盤",
                    EnglishName = "GOLDEN BBQ PLATE",
                    Description = "美式餐盤皆附：嫩葉生菜 + 美式自選配料",
                    DisplayOrder = 1,
                    IsActive = true
                };
                context.Categories.Add(catPlates);
                context.SaveChanges();
            }

            var catSnacks = context.Categories.FirstOrDefault(c => c.Name == "美墨小點");
            if (catSnacks == null)
            {
                catSnacks = new Category
                {
                    Name = "美墨小點",
                    EnglishName = "SNACKS",
                    Description = "現炸金黃酥脆小點與特製沾醬",
                    DisplayOrder = 2,
                    IsActive = true
                };
                context.Categories.Add(catSnacks);
                context.SaveChanges();
            }

            var catVeggies = context.Categories.FirstOrDefault(c => c.Name == "吃點蔬菜");
            if (catVeggies == null)
            {
                catVeggies = new Category
                {
                    Name = "吃點蔬菜",
                    EnglishName = "VEGETABLE",
                    Description = "炭烤鮮蔬，清爽解膩",
                    DisplayOrder = 3,
                    IsActive = true
                };
                context.Categories.Add(catVeggies);
                context.SaveChanges();
            }

            var catSalad = context.Categories.FirstOrDefault(c => c.Name == "最佳綠葉");
            if (catSalad == null)
            {
                catSalad = new Category
                {
                    Name = "最佳綠葉",
                    EnglishName = "SALAD",
                    Description = "水耕生菜與特製主廚沙拉",
                    DisplayOrder = 4,
                    IsActive = true
                };
                context.Categories.Add(catSalad);
                context.SaveChanges();
            }

            var catSauces = context.Categories.FirstOrDefault(c => c.Name == "醬料");
            if (catSauces == null)
            {
                catSauces = new Category
                {
                    Name = "醬料",
                    EnglishName = "SAUCE",
                    Description = "主廚特製美式BBQ與風味沾醬",
                    DisplayOrder = 5,
                    IsActive = true
                };
                context.Categories.Add(catSauces);
                context.SaveChanges();
            }

            // Helper for plate options
            List<OptionGroup> CreatePlateOptionGroups()
            {
                var sideGroup = new OptionGroup
                {
                    Name = "美式餐盤自選配料",
                    EnglishName = "SIDE DISHES",
                    Description = "美式餐盤附贈自選配料（以上擇一）",
                    IsRequired = true,
                    MinSelect = 1,
                    MaxSelect = 1,
                    DisplayOrder = 1,
                    Options = new List<OptionItem>
                    {
                        new() { Name = "蘋果捲心菜", EnglishName = "Apple Kale Coleslaw", ExtraPrice = 0, DisplayOrder = 1, IsAvailable = true },
                        new() { Name = "美式奶油玉米", EnglishName = "Creamed Corn", ExtraPrice = 0, DisplayOrder = 2, IsAvailable = true },
                        new() { Name = "薯塊沙拉", EnglishName = "Potato Salad", ExtraPrice = 10, DisplayOrder = 3, IsAvailable = true },
                        new() { Name = "火烤起司紅薯", EnglishName = "Sweet Potato With Cheese", ExtraPrice = 20, DisplayOrder = 4, IsAvailable = true },
                        new() { Name = "起司通心粉", EnglishName = "Mac & Cheese", ExtraPrice = 30, DisplayOrder = 5, IsAvailable = true }
                    }
                };

                var addOnGroup = new OptionGroup
                {
                    Name = "限量加購",
                    EnglishName = "LIMITED ADD-ON",
                    Description = "限量加購，售完為止",
                    IsRequired = false,
                    MinSelect = 0,
                    MaxSelect = 1,
                    DisplayOrder = 2,
                    Options = new List<OptionItem>
                    {
                        new() { Name = "雞汁小米飯", EnglishName = "Chicken Jus Millet Rice", ExtraPrice = 10, Tag = "限量加購 售完為止", DisplayOrder = 1, IsAvailable = true }
                    }
                };

                return new List<OptionGroup> { sideGroup, addOnGroup };
            }

            // Legacy renames and cleanups
            // 1. Rename 柴燻櫻桃鴨胸 -> 燒烤厚切豬排
            var oldDuck = context.MenuItems.FirstOrDefault(m => m.Name == "柴燻櫻桃鴨胸");
            if (oldDuck != null)
            {
                oldDuck.Name = "燒烤厚切豬排";
                oldDuck.EnglishName = "Texas BBQ Pork Chop";
                oldDuck.Description = "豬沙朗是煙燻路線！厚切豬排是碳烤風味！";
                oldDuck.Price = 200;
                oldDuck.Badge = "嫩";
                oldDuck.DisplayOrder = 2;
                oldDuck.RequiresPlateSides = true;
            }

            // 2. Rename 卡津風味烤魚 -> 卡津風味烤中卷
            var oldFish = context.MenuItems.FirstOrDefault(m => m.Name == "卡津風味烤魚");
            if (oldFish != null)
            {
                oldFish.Name = "卡津風味烤中卷";
                oldFish.EnglishName = "Cajun Grilled Squid";
                oldFish.Description = "卡津為路易斯安那州的經典風味，多種香草香料混合，略帶煙燻味及溫和草本香氣。";
                oldFish.Price = 230;
                oldFish.Badge = "剛好有去蚵仔寮";
                oldFish.DisplayOrder = 4;
                oldFish.RequiresPlateSides = true;
            }

            // 3. Rename 燒烤甜椒青椒 -> 燒烤青椒甜椒
            var oldPepper = context.MenuItems.FirstOrDefault(m => m.Name == "燒烤甜椒青椒");
            if (oldPepper != null)
            {
                oldPepper.Name = "燒烤青椒甜椒";
                oldPepper.Description = "吃BBQ青椒不能缺席的吧";
                oldPepper.Price = 110;
                oldPepper.CategoryId = catVeggies.Id;
                oldPepper.DisplayOrder = 1;
                oldPepper.IsAvailable = true;
            }

            // 4. Remove discontinued 蘋果手撕豬沙拉 if not in orders
            var oldSalad = context.MenuItems.FirstOrDefault(m => m.Name == "蘋果手撕豬沙拉");
            if (oldSalad != null)
            {
                var hasOrders = context.OrderItems.Any(oi => oi.MenuItemId == oldSalad.Id);
                if (!hasOrders)
                {
                    context.MenuItems.Remove(oldSalad);
                }
                else
                {
                    oldSalad.IsAvailable = false;
                }
            }

            // 5. Remove discontinued 楓糖辣水牛城炸雞柳條 if not in orders
            var oldBuffalo = context.MenuItems.FirstOrDefault(m => m.Name == "楓糖辣水牛城炸雞柳條");
            if (oldBuffalo != null)
            {
                var hasOrders = context.OrderItems.Any(oi => oi.MenuItemId == oldBuffalo.Id);
                if (!hasOrders)
                {
                    context.MenuItems.Remove(oldBuffalo);
                }
                else
                {
                    oldBuffalo.IsAvailable = false;
                }
            }

            context.SaveChanges();

            // Upsert MenuItem helper (固定住店家更新的內容，不會無故覆蓋店家設定的庫存與上下架狀態！)
            void UpsertMenuItem(int catId, string name, string engName, decimal price, string desc, string? badge, bool reqPlate, int order, string img = "/img/2.jpg", bool isAvailable = true)
            {
                var item = context.MenuItems.Include(m => m.OptionGroups).ThenInclude(g => g.Options).FirstOrDefault(m => m.Name == name);
                if (item == null)
                {
                    item = new MenuItem
                    {
                        CategoryId = catId,
                        Name = name,
                        EnglishName = engName,
                        Price = price,
                        Description = desc,
                        Badge = badge,
                        RequiresPlateSides = reqPlate,
                        DisplayOrder = order,
                        ImageUrl = img,
                        IsAvailable = isAvailable
                    };
                    if (reqPlate)
                    {
                        item.OptionGroups = CreatePlateOptionGroups();
                    }
                    context.MenuItems.Add(item);
                }
                else
                {
                    item.CategoryId = catId;
                    item.EnglishName = engName;
                    item.Price = price;
                    item.Description = desc;
                    item.Badge = badge;
                    item.RequiresPlateSides = reqPlate;
                    item.DisplayOrder = order;
                    if (string.IsNullOrWhiteSpace(item.ImageUrl))
                    {
                        item.ImageUrl = img;
                    }

                    // 注意：保留 item.IsAvailable 與 item.StockQuantity，避免重啟或重新部屬時覆蓋店長後台上下架操作！

                    if (reqPlate && (item.OptionGroups == null || !item.OptionGroups.Any()))
                    {
                        item.OptionGroups = CreatePlateOptionGroups();
                    }
                    else if (!reqPlate && item.OptionGroups != null && item.OptionGroups.Any())
                    {
                        context.OptionGroups.RemoveRange(item.OptionGroups);
                    }
                }
            }

            // 1. 美式餐盤（GOLDEN BBQ PLATE）
            UpsertMenuItem(catPlates.Id, "克里奧雞腿排", "Creole Chicken Thigh", 180, "南方香料醃製, 烤出焦香外皮與飽滿肉汁。", "人氣", true, 1, "/img/2.jpg", false);
            UpsertMenuItem(catPlates.Id, "燒烤厚切豬排", "Texas BBQ Pork Chop", 200, "豬沙朗是煙燻路線！厚切豬排是碳烤風味！", "嫩", true, 2, "/img/2.jpg", true);
            UpsertMenuItem(catPlates.Id, "柴燻慢烤豬沙朗", "Texas Smoked Pork Collar", 210, "獨門香料慢烤18小時,香氣逼人軟嫩不油膩。", "招牌", true, 3, "/img/2.jpg", true);
            UpsertMenuItem(catPlates.Id, "卡津風味烤中卷", "Cajun Grilled Squid", 230, "卡津為路易斯安那州的經典風味，多種香草香料混合，略帶煙燻味及溫和草本香氣。", "剛好有去蚵仔寮", true, 4, "/img/2.jpg", true);
            UpsertMenuItem(catPlates.Id, "德州燻烤牛胸肉", "Texas Smoked Brisket", 250, "冠軍香料柴火48小時熟成，不一定每天都有。", "主廚推薦", true, 5, "/img/2.jpg", true);
            UpsertMenuItem(catPlates.Id, "萊姆炭香鮭魚菲力", "Grilled Lime Salmon Fillet", 240, "蚵仔寮直送整條鮭魚只取菲力(可能會有小細刺請小心)", "海港直送", true, 6, "/img/2.jpg", true);
            UpsertMenuItem(catPlates.Id, "克里奧萊姆鮮蝦", "Creole Shrimp", 230, "南方香料醃製, 淡淡地萊姆清香，大火炙燒脆彈鮮甜。", "手剝去腸泥", true, 7, "/img/2.jpg", true);
            UpsertMenuItem(catPlates.Id, "美式酸奶炸雞柳條", "Chicken Tender with Yogurt", 190, "薄皮嫩雞，用酸奶和香料醃一晚才能賣。", "在地土雞", true, 8, "/img/4.jpg", true);

            // 2. 美墨小點（SNACKS）
            UpsertMenuItem(catSnacks.Id, "美式燒烤玉米肋排", "Corn Ribs", 110, "Golden Corn招牌必點！特調醬料刷烤，沾酸奶醬實在太搭", "必點招牌", false, 1, "/img/4.jpg", true);
            UpsertMenuItem(catSnacks.Id, "黑松露醬細薯", "Fries with Black Truffle Sauce", 110, "酥脆鞋帶細薯拌入高檔黑松露醬", null, false, 2, "/img/4.jpg", true);
            UpsertMenuItem(catSnacks.Id, "辣辣的花椒細薯", "Fries with Confit Garlic and Chili", 80, "辣辣的麻麻的", "微辣推薦", false, 3, "/img/4.jpg", true);
            UpsertMenuItem(catSnacks.Id, "海鹽經典美式細薯", "Classic American Fries with Sea Salt", 70, "通常餐酒館才吃得到", null, false, 4, "/img/4.jpg", true);
            UpsertMenuItem(catSnacks.Id, "燻烤起司馬鈴薯", "Smoked Potato with Cheese", 80, "最近買的都很大顆，兩人一顆剛剛好。塞入傑克寇比起司，不是化學的起司醬。", null, false, 5, "/img/4.jpg", true);

            // 3. 吃點蔬菜 & 最佳綠葉 & 醬料
            UpsertMenuItem(catVeggies.Id, "燒烤青椒甜椒", "Grilled Green and Bell Pepper", 110, "吃BBQ青椒不能缺席的吧", null, false, 1, "/img/5.jpg", true);
            UpsertMenuItem(catVeggies.Id, "燒烤厚切櫛瓜", "Grilled Thick Cut Zucchini", 110, "厚切才爽快", null, false, 2, "/img/5.jpg", true);
            UpsertMenuItem(catVeggies.Id, "燒烤杏鮑菇", "Grilled King Oyster Mushroom", 110, "烤菇需要耐心，梅納反應很重要。", null, false, 3, "/img/5.jpg", true);

            // 4. 醬料
            UpsertMenuItem(catSauces.Id, "卡羅萊納BBQ燒烤醬", "Carolina BBQ Sauce", 20, "手工熬煮一不小心就燒焦。經典道地濃郁酸甜，胡椒的微微辣是大人的口味。", null, false, 1, "/img/6.jpg", true);
            UpsertMenuItem(catSauces.Id, "燒烤酸辣醬", "BBQ Spicy Sauce", 20, "煙燻感的酸甜辣醬，偏酸微辣。", null, false, 2, "/img/2.jpg", true);
            UpsertMenuItem(catSauces.Id, "蒔蘿優格醬", "Ranch Sauce", 15, "蒔蘿與優格特調，簡單清爽什麼都能沾。", null, false, 3, "/img/6.jpg", true);

            // 確保所有餐盤自選配料（包含薯塊沙拉、起司通心粉）預設為上架可用狀態
            var allPlateOptions = context.OptionItems
                .Include(o => o.OptionGroup)
                .Where(o => o.OptionGroup != null && o.OptionGroup.Name == "美式餐盤自選配料")
                .ToList();

            foreach (var opt in allPlateOptions)
            {
                if (opt.Name == "薯塊沙拉" || opt.Name == "起司通心粉" || opt.Name == "蘋果捲心菜" || opt.Name == "美式奶油玉米" || opt.Name == "火烤起司紅薯")
                {
                    opt.IsAvailable = true;
                }
            }

            // 確保雞汁小米飯為限量加購並上架
            var milletOptions = context.OptionItems
                .Where(o => o.Name == "雞汁小米飯")
                .ToList();
            foreach (var opt in milletOptions)
            {
                opt.Tag = "限量加購 售完為止";
                opt.ExtraPrice = 10;
                opt.IsAvailable = true;
            }

            // 確保美式酸奶炸雞柳條屬於美式餐盤且有自選配料
            var chickenTenderItem = context.MenuItems
                .Include(m => m.OptionGroups)
                    .ThenInclude(g => g.Options)
                .FirstOrDefault(m => m.Name.Contains("炸雞柳") && m.CategoryId == catPlates.Id);
            if (chickenTenderItem != null)
            {
                chickenTenderItem.RequiresPlateSides = true;
                if (chickenTenderItem.OptionGroups == null || !chickenTenderItem.OptionGroups.Any())
                {
                    chickenTenderItem.OptionGroups = CreatePlateOptionGroups();
                }
            }

            context.SaveChanges();

            // Store Settings (保留店家所有設定，重啟或重新部屬絕對不覆蓋！)
            var defaultSettings = new Dictionary<string, (string Value, string Desc)>
            {
                ["StoreName"] = ("Golden Corn 後勁店", "店家名稱"),
                ["BrandSub"] = ("後勁 Houjing · Texas Smoked BBQ & Soul Food", "副標題/品牌標語"),
                ["Phone"] = ("0910237105", "門市電話"),
                ["Address"] = ("高雄市楠梓區金富街85-1號", "門市地址"),
                ["BusinessHours"] = ("週四至週六 16:30-22:30 《週日至週三可來電預點餐》《BBQ外燴或客製化團訂歡迎來電或私訊IG》", "營業時間"),
                ["Announcement"] = ("🔥 歡迎光臨 Golden Corn！美式德州慢火燻烤、靈魂料理，餐點現點現做，感謝您的耐心等候。", "前台跑馬燈公告"),
                ["BankCode"] = ("822", "銀行代碼"),
                ["BankName"] = ("中國信託", "銀行名稱"),
                ["BankAccount"] = ("129540943647", "銀行帳號"),
                ["BankAccountName"] = ("Golden Corn 後勁店", "銀行戶名"),
                ["LinePayUrl"] = ("line://nv/cameraRoll/single", "LINE Pay 連結"),
                ["LineServiceUrl"] = ("https://line.me/R/ti/p/@260nqbpr", "官方 LINE 客服連結"),
                ["IgServiceUrl"] = ("https://www.instagram.com/goldencorn_diner/", "官方 IG 連結"),
                ["IsOpen"] = ("true", "是否營業中 (true/false)"),
                ["AdminPin"] = ("Hawking", "店家管理密碼/PIN碼"),
                ["CoverImageUrl"] = ("/img/cover_poster.png", "首頁封面大圖網址"),
                ["HeroBadge"] = ("🔥 每日限量低溫煙燻熟成", "首頁徽章"),
                ["HeroTitle"] = ("GOLDEN CORN", "首頁大標題"),
                ["HeroSubtitle"] = ("德州慢火燻烤 · 靈魂美墨料理", "首頁副標題"),
                ["HeroDesc"] = ("堅持古法慢火煙燻熟成，將濃郁肉汁與柴火香氣鎖在每一吋肉質裡。現在立即線上預點，現場取餐不用等！", "首頁簡介"),
                ["CoverAnnouncementBtnText"] = ("門市公告與營業資訊", "封面公告按鈕文字"),
                ["CoverSwipeBtnText"] = ("SWIPE FOR MY STORY... ↓", "封面下滑提示文字"),
                ["PortalPillTag"] = ("CHOOSE YOUR ORDER TYPE", "入口標籤"),
                ["PortalTitle"] = ("選擇您的美味模式", "入口大標題"),
                ["PortalSubtitle"] = ("外公的田間貨櫃工作室 · 柴火低溫慢燻 · 慢靈魂料理", "入口副標題"),
                ["PortalNoticeBtnText"] = ("[ 門市公告與營業資訊 ]", "入口公告按鈕文字"),
                ["PortalHistoryBtnText"] = ("[ 查詢我的訂單 ]", "入口歷史按鈕文字"),
                ["BoxATag"] = ("🌟 經典門市點餐", "A區標籤"),
                ["BoxATitle"] = ("門市線上點餐", "A區標題"),
                ["BoxASubtitle"] = ("Daily Fresh Menu & Ordering", "A區副標題"),
                ["BoxADesc"] = ("煙燻牛胸肉、極嫩豬梅花與招牌玉米肋排，顛覆你對美式餐點的想像!", "A區簡介"),
                ["BoxABadge"] = ("[ 靈魂美式餐點 · 預約外帶 ]", "A區徽章"),
                ["BoxAIcon"] = ("/img/box_b_icon.jpg", "A區圖示"),
                ["BoxABullets"] = ("大口吃菜大口吃肉\n美式配菜不馬虎", "A區條列特點"),
                ["BoxAFeatures"] = ("✅ 現點現做熱騰騰出餐|✅ 自由選配美式配料|✅ 支援線上LINE Pay與轉帳", "A區特色"),
                ["BoxABtnText"] = ("進入門市點餐 🍔", "A區按鈕文字"),
                ["BoxALink"] = ("/menu", "A區連結"),
                ["BoxBTag"] = ("🍖 聚會・露營・派對專區", "B區標籤"),
                ["BoxBTitle"] = ("派對餐點 ＆ 露營真空包", "B區標題"),
                ["BoxBSubtitle"] = ("Party Feast & Vacuum Camping Pack", "B區副標題"),
                ["BoxBDesc"] = ("露營野餐、朋友聚會、公司慶生必備！真空低溫冷藏包與派對澎湃分享餐盒。", "B區簡介"),
                ["BoxBBadge"] = ("[ 團體聚會 · 露營野餐 · 預約外燴 ]", "B區徽章"),
                ["BoxBIcon"] = ("/img/box_a_icon.jpg", "B區圖示"),
                ["BoxBBullets"] = ("燻烤肉拼盤\n美式靈魂炸物\n黃金玉米肋條\n嫩葉沙拉/碳烤時蔬\n艾莉西亞布朗尼\n手工獨門醬料\n一對一客製化諮詢100%享受", "B區條列特點"),
                ["BoxBFeatures"] = ("✅ 大份量肉肉分享盒|✅ 低溫真空即享包(隔水加熱即可)|✅ 專人客製化菜單與外燴洽詢", "B區特色"),
                ["BoxBBtnText"] = ("查看派對與露營方案 🔥", "B區按鈕文字"),
                ["BoxBLink"] = ("/party", "B區連結"),
                ["StoryQuote"] = ("「世界太快，我們在田裡為你製造一點慢靈魂。」", "底部引言"),
                ["StoryAuthor"] = ("STAY GOLDEN · 慢靈魂製造所 · STAY HUNGRY", "底部作者"),
                ["NoticeDelivery"] = ("• 本店餐點皆為低溫慢火柴燻，建議提前預約以確保肉品庫存。\n• 外送服務：以 LALAMOVE 平台配送（例如 7.5 公里機車外送約 140 元）。\n• 試營運優惠：北高雄消費滿 $600 免運！", "外送須知"),
                ["NoticeContact"] = ("企業商務套餐、下午茶點心團購、野餐派對包，請私訊 IG 粉專由主廚一對一服務：\n📷 IG: @goldencorn_diner\n📞 電話: 0910-237-105", "聯繫須知"),
                ["PartyBannerBadge"] = ("🏕️ 露營野餐 · 聚會派對 · 歡聚分享", "派對橫幅徽章"),
                ["PartyBannerTitle"] = ("派對餐盒 ＆ 露營真空包", "派對橫幅標題"),
                ["PartyBannerSubtitle"] = ("BBQ Party Feast & Camping Vacuum Pack", "派對橫幅副標題"),
                ["PartyBannerDesc"] = ("聚會、野餐、露營、公司團購首選！\n主廚精心慢火煙燻熟成，大份量餐盒現點即享，真空包隔水加熱即有大師級烤肉！", "派對橫幅簡介"),
                ["PartyHeroBadge"] = ("PARTY BOX & CATERING", "派對英雄徽章"),
                ["PartyHeroTitle"] = ("美式煙燻派對餐盒", "派對英雄標題"),
                ["PartyHeroSubtitle"] = ("露營野餐 · 朋友歡聚 · 企業團訂 · 生日派對的大份量美式靈魂盛宴！", "派對英雄副標題"),
                ["PartyHeroTags"] = ("原木柴燒慢燻 | 美式靈魂炸物 | 招牌黃金玉米", "派對英雄標籤"),
                ["PartyNoticeTitle"] = ("訂購須知與預約說明", "派對須知標題"),
                ["PartyNoticeText"] = ("📌 派對餐盒與露營真空包因製程繁複（需柴火低溫慢燻16-48小時），請至少提前 2~3 天預約。\n📌 取餐方式：可於營業時間至門市自取，或協助安排 Lalamove 外送（運費另計）。\n📌 如有特殊客製化份量或企業外燴需求，歡迎直接私訊官方 IG 或來電洽詢！", "派對須知內文"),
                ["PartySet1Badge"] = ("熱門推薦", "套裝1徽章"),
                ["PartySet1Label"] = ("組合 1", "套裝1標籤"),
                ["PartySet1Portion"] = ("適合小趴踢", "套裝1份量"),
                ["PartySet1Title"] = ("精緻美式小聚會", "套裝1標題"),
                ["PartySet1Subtitle"] = ("Texas Smoked Feast Box (For 4-8)", "套裝1副標題"),
                ["PartySet1Desc"] = ("小聚會可至現場用餐也可外帶", "套裝1描述"),
                ["PartySet1Items"] = ("主廚嫩葉沙拉\n美式酸奶炸雞柳\n經典海鹽細薯\n炭烤時蔬佐法式醬汁\n炭烤金玉米肋排\n燻肉拼盤(牛/豬/雞)\n鮮蝦小米燉飯\n手工布朗尼", "套裝1品項"),
                ["PartySet1Price"] = ("依人數客製報價", "套裝1特價"),
                ["PartySet1OriginalPrice"] = ("", "套裝1原價"),
                ["PartySet2Badge"] = ("美式狂歡", "套裝2徽章"),
                ["PartySet2Label"] = ("組合 2", "套裝2標籤"),
                ["PartySet2Portion"] = ("適合大趴踢", "套裝2份量"),
                ["PartySet2Title"] = ("經典美式大派對", "套裝2標題"),
                ["PartySet2Subtitle"] = ("Texas Smoked Feast Box (For 16-20)", "套裝2副標題"),
                ["PartySet2Desc"] = ("美式派對組合送到府", "套裝2描述"),
                ["PartySet2Items"] = ("炭烤時蔬\n美式炸物拼盤(菇菇/雞柳/魚柳)\n水果騷莎玉米脆片\n燻肉拼盤(牛/豬/雞)\n卡薩帝亞墨西哥酥餅\n手工布朗尼", "套裝2品項"),
                ["PartySet2Price"] = ("依人數客製報價", "套裝2特價"),
                ["PartySet2OriginalPrice"] = ("", "套裝2原價"),
                ["PartySet3Badge"] = ("露營首選", "套裝3徽章"),
                ["PartySet3Label"] = ("組合 3", "套裝3標籤"),
                ["PartySet3Portion"] = ("真空封裝即開即享", "套裝3份量"),
                ["PartySet3Title"] = ("露營野餐低溫真空即享組", "套裝3標題"),
                ["PartySet3Subtitle"] = ("Outdoor Camping Ready-to-Heat Pack", "套裝3副標題"),
                ["PartySet3Desc"] = ("主廚已煙燻熟成並低溫真空封裝。隔水加熱即可享受大師級烤肉！", "套裝3描述"),
                ["PartySet3Items"] = ("德州燻烤極嫩豬梅花\n德州燻烤牛胸肉\n克里奧香草烤雞\n水牛城辣雞翅", "套裝3品項"),
                ["PartySet3Price"] = ("依人數客製報價", "套裝3特價"),
                ["PartySet3OriginalPrice"] = ("", "套裝3原價"),
                ["PartyInquiryTitle"] = ("預約與客製化洽詢", "洽詢標題"),
                ["PartyInquiryDesc"] = ("不論是家庭聚餐、朋友露營、公司慶生或大型活動外燴，主廚皆可為您量身規劃份量與菜色組合！", "洽詢描述"),
                ["PartyInquiryIgText"] = ("私訊 IG 預約：@goldencorn_diner", "IG洽詢文字"),
                ["PartyInquiryIgLink"] = ("https://www.instagram.com/goldencorn_diner/", "IG洽詢連結"),
                ["PartyInquiryPhoneText"] = ("電話洽詢：0910-237-105", "電話洽詢文字"),
                ["PartyInquiryPhoneLink"] = ("tel:0910237105", "電話洽詢連結"),
                ["LineChannelAccessToken"] = ("fq8y2BxxQgc1HTk3FjLehq6vwk5o7w/qUPaq13SBOawDme5JPpmEJEiSnqIA/j761V4xWEiRY3Z+WmxAIGjfF77lx3JZjr6Alnz+jjHBdyt8qqqFpluvsIKWPbKk0MqvfugY1K22JL84gV4ET7bdRAdB04t89/1O/w1cDnyilFU=", "LINE Bot Channel Access Token"),
                ["LineAdminUserId"] = ("U5f20f9ebb8804fc0671b89b5a453fd28", "老闆的 LINE User ID"),
                ["PopupNoticeEnabled"] = ("false", "進站彈窗開關"),
                ["PopupNoticeTitle"] = ("🚨 門市公告", "進站彈窗標題"),
                ["PopupNoticeContent"] = ("", "進站彈窗內容"),
                ["SlotAmountLimit"] = ("3000", "每30分鐘訂單金額上限"),
                ["BusyEstimatedMinutes"] = ("30", "廚房爆單預估製作分鐘數")
            };

            foreach (var kvp in defaultSettings)
            {
                var existing = context.StoreSettings.FirstOrDefault(s => s.Key == kvp.Key);
                if (existing == null)
                {
                    context.StoreSettings.Add(new StoreSetting
                    {
                        Key = kvp.Key,
                        Value = kvp.Value.Value,
                        Description = kvp.Value.Desc
                    });
                }
                else
                {
                    // If LineChannelAccessToken in DB is empty, populate with the verified token
                    if (kvp.Key == "LineChannelAccessToken" && string.IsNullOrWhiteSpace(existing.Value))
                    {
                        existing.Value = kvp.Value.Value;
                    }
                    if (kvp.Key == "LineAdminUserId" && string.IsNullOrWhiteSpace(existing.Value))
                    {
                        existing.Value = kvp.Value.Value;
                    }
                    // 保留後台店家所有已設定之數值，重啟或重新部屬時絕對不覆蓋！
                }
            }

            // Remove StoreTip if it exists in DB
            var tipSetting = context.StoreSettings.FirstOrDefault(s => s.Key == "StoreTip");
            if (tipSetting != null)
            {
                context.StoreSettings.Remove(tipSetting);
            }

            context.SaveChanges();
        }
    }
}
