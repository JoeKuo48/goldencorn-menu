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

            // Helper to insert MenuItem only if not exists (preserves user edits)
            void EnsureMenuItem(int catId, string name, string engName, decimal price, string desc, string? badge, bool reqPlate, int order, string img = "/img/2.jpg")
            {
                var item = context.MenuItems.Include(m => m.OptionGroups).FirstOrDefault(m => m.Name == name);
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
                        IsAvailable = true
                    };
                    if (reqPlate)
                    {
                        item.OptionGroups = CreatePlateOptionGroups();
                    }
                    context.MenuItems.Add(item);
                }
            }

            // 1. 美式餐盤（GOLDEN BBQ PLATE）
            EnsureMenuItem(catPlates.Id, "克里奧雞腿排", "Creole Chicken Thigh", 180, "南方香料醃製, 烤出焦香外皮與飽滿肉汁。", "人氣", true, 1, "/img/2.jpg");
            EnsureMenuItem(catPlates.Id, "古巴風烤豬排", "Cuban Grilled Pork Chop", 190, "軟嫩豬排，塗上由橙汁、檸檬、歐芹、蒜頭及橄欖油做成的Mojo青醬，清新略帶微酸。", null, true, 2, "/img/2.jpg");
            EnsureMenuItem(catPlates.Id, "德州燻烤豬梅花", "Texas Smoked Pork Collar", 190, "獨門香料慢烤16小時,香氣逼人軟嫩不油膩。", "招牌", true, 3, "/img/2.jpg");
            EnsureMenuItem(catPlates.Id, "卡津風味烤魚", "Cajun Grilled Fish", 230, "卡津為路易斯安那州的經典風味，多種香草香料混合，略帶煙燻味及溫和草本香氣。", null, true, 4, "/img/2.jpg");
            EnsureMenuItem(catPlates.Id, "德州燻烤牛胸肉", "Texas Smoked Brisket", 250, "冠軍香料柴火48小時熟成，不一定每天都有。", "主廚推薦", true, 5, "/img/2.jpg");
            EnsureMenuItem(catPlates.Id, "萊姆炭香鮭魚菲力", "Grilled Lime Salmon Fillet", 240, "蚵仔寮直送整條鮭魚只取菲力，厚切鮭魚丁口感飽滿, 外層帶有淡淡煙燻香氣。", null, true, 6, "/img/2.jpg");
            EnsureMenuItem(catPlates.Id, "克里奧鮮蝦", "Creole Shrimp", 210, "南方香料醃製, 淡淡地萊姆清香，大火炙燒脆彈鮮甜。", null, true, 7, "/img/2.jpg");

            // 2. 美墨小點（SNACKS）
            EnsureMenuItem(catSnacks.Id, "海鹽經典美式細薯", "Classic American Fries with Sea Salt", 70, "鞋帶細薯，通常在餐酒館才吃得到。", null, false, 1, "/img/4.jpg");
            EnsureMenuItem(catSnacks.Id, "美式燒烤玉米肋排", "Corn Ribs", 120, "Golden Corn招牌！沾特調酸奶醬實在太搭。", "必點招牌", false, 2, "/img/4.jpg");
            EnsureMenuItem(catSnacks.Id, "油封大蒜花椒細薯", "Fries with Confit Garlic and Chili", 90, "辣辣的。", "微辣推薦", false, 3, "/img/4.jpg");
            EnsureMenuItem(catSnacks.Id, "燻烤起司馬鈴薯", "Smoked Potato with Cheese", 60, "刷上奶油燻烤至綿密, 塞入傑克寇比起司。", null, false, 4, "/img/4.jpg");
            EnsureMenuItem(catSnacks.Id, "黑松露醬細薯", "Fries with Black Truffle Sauce", 120, "黑松露醬好貴，主廚不惜成本。", null, false, 5, "/img/4.jpg");
            EnsureMenuItem(catSnacks.Id, "美式酸奶炸雞柳條", "Chicken Tender with Yogurt", 150, "住太遠不要點，炸物不夠脆主廚會傷心。", "人氣", false, 6, "/img/4.jpg");
            EnsureMenuItem(catSnacks.Id, "楓糖辣水牛城美式炸雞柳條", "Chicken Tender with Maple Buffalo Sauce", 180, "吃過最狂的水牛城秘方(謝謝老闆艾迪) 住太遠不要點，炸物現吃才讚。", "美式經典", false, 7, "/img/4.jpg");

            // 3. 吃點蔬菜 & 最佳綠葉 & 醬料
            EnsureMenuItem(catVeggies.Id, "燒烤厚切櫛瓜", "Grilled Thick Cut Zucchini", 100, "厚切才爽。", null, false, 1, "/img/5.jpg");
            EnsureMenuItem(catVeggies.Id, "燒烤杏鮑菇", "Grilled King Oyster Mushroom", 100, "烤杏鮑菇需要耐心。", null, false, 2, "/img/5.jpg");
            EnsureMenuItem(catVeggies.Id, "燒烤甜椒青椒", "Grilled Green and Bell Pepper", 100, "甜椒好貴，但配色才美。", null, false, 3, "/img/5.jpg");

            EnsureMenuItem(catSalad.Id, "純 水耕嫩生菜杯", "Mesclun", 60, "內行人點來包肉吃。喜歡可以專門配送。", null, false, 1, "/img/6.jpg");
            EnsureMenuItem(catSalad.Id, "燒烤嫩雞胸沙拉", "Grilled Chicken Breast Salad", 150, "經典不敗，凱撒的升級低脂版！", "輕食推薦", false, 2, "/img/6.jpg");

            EnsureMenuItem(catSauces.Id, "卡羅萊納BBQ燒烤醬", "Carolina BBQ Sauce", 15, "經典道地濃郁酸甜，胡椒的微微辣是大人的口味。", null, false, 1, "/img/6.jpg");
            EnsureMenuItem(catSauces.Id, "蒔蘿優格醬", "Ranch Sauce", 15, "蒔蘿與優格特調，簡單清爽什麼都能沾。", null, false, 2, "/img/6.jpg");

            context.SaveChanges();

            // Store Settings
            var defaultSettings = new Dictionary<string, (string Value, string Desc)>
            {
                ["StoreName"] = ("Golden Corn 後勁店", "店家名稱"),
                ["BrandSub"] = ("後勁 Houjing · Texas Smoked BBQ & Soul Food", "副標題/品牌標語"),
                ["Phone"] = ("0910237105", "門市電話"),
                ["Address"] = ("高雄市楠梓區金富街85-1號", "門市地址"),
                ["BusinessHours"] = ("週四至週六 16:30-22:30 《週日至週三僅接受線上提前預點》", "營業時間"),
                ["Announcement"] = ("🔥 歡迎光臨 Golden Corn！美式德州慢火燻烤、靈魂料理，餐點現點現做，感謝您的耐心等候。", "前台跑馬燈公告"),
                ["StoreTip"] = ("💡現階段為主廚一人工作室，建議提前預約，部分燻肉品項才不會缺貨唷！\n💡如需外送，皆以LALAMOVE平台試算運費。EX.7.5公里機車外送約140元\n💡試營運期間，消費滿600元外送直接免運！再送美式燒烤玉米肋排乙份！\n💡有任何問題，如企業商務套餐、下午茶點心團購等等，請私訊IG粉專，由主廚一對一服務。\n💡IG: goldencorn_diner", "門市資訊燈泡備註/溫馨提示"),
                ["BankCode"] = ("822", "銀行代碼"),
                ["BankName"] = ("中國信託", "銀行名稱"),
                ["BankAccount"] = ("129540943647", "銀行帳號"),
                ["BankAccountName"] = ("Golden Corn 後勁店", "銀行戶名"),
                ["LinePayUrl"] = ("line://nv/cameraRoll/single", "LINE Pay一鍵開啟/掃碼連結"),
                ["LineServiceUrl"] = ("https://line.me/R/ti/p/@goldencorn_diner", "店家LINE客服連結 (一對一私訊)"),
                ["IgServiceUrl"] = ("https://www.instagram.com/goldencorn_diner/", "店家IG客服連結"),
                ["IsOpen"] = ("true", "是否營業中 (true/false)"),
                ["AdminPin"] = ("Hawking", "店家管理密碼"),

                // Homepage CMS Settings
                ["CoverImageUrl"] = ("/img/cover_poster.png", "封面海報圖片網址"),
                ["HeroBadge"] = ("🔥 每日限量低溫煙燻熟成", "首頁主視覺徽章文字"),
                ["HeroTitle"] = ("GOLDEN CORN", "首頁主視覺大標題"),
                ["HeroSubtitle"] = ("德州慢火燻烤 · 靈魂美墨料理", "首頁主視覺副標題"),
                ["HeroDesc"] = ("堅持古法慢火煙燻熟成，將濃郁肉汁與柴火香氣鎖在每一吋肉質裡。現在立即線上預點，現場取餐不用等！", "首頁主視覺說明"),

                // Box A: 門市預約點餐 (menu.html)
                ["BoxATag"] = ("🌟 經典門市點餐", "方塊A標籤"),
                ["BoxATitle"] = ("門市線上點餐", "方塊A標題"),
                ["BoxASubtitle"] = ("Daily Fresh Menu & Ordering", "方塊A英文標題"),
                ["BoxADesc"] = ("煙燻牛胸肉、卡津魚排、美式炸雞與招牌玉米肋排，現點現做。", "方塊A說明"),
                ["BoxAFeatures"] = ("✅ 現點現做熱騰騰出餐|✅ 自由選配美式配料|✅ 支援線上LINE Pay與轉帳", "方塊A特色清單(以|分隔)"),
                ["BoxABtnText"] = ("進入門市點餐 🍔", "方塊A按鈕文字"),
                ["BoxALink"] = ("/menu", "方塊A跳轉連結"),

                // Box B: 派對餐盒與露營真空包 (party.html)
                ["BoxBTag"] = ("🍖 聚會・露營・派對專區", "方塊B標籤"),
                ["BoxBTitle"] = ("派對餐盒 ＆ 露營真空包", "方塊B標題"),
                ["BoxBSubtitle"] = ("Party Feast & Vacuum Camping Pack", "方塊B英文標題"),
                ["BoxBDesc"] = ("露營野餐、朋友聚會、公司慶生必備！真空低溫冷藏包與派對澎湃分享餐盒。", "方塊B說明"),
                ["BoxBFeatures"] = ("✅ 大份量肉肉分享盒|✅ 低溫真空即享包(隔水加熱即可)|✅ 專人客製化菜單與外燴洽詢", "方塊B特色清單(以|分隔)"),
                ["BoxBBtnText"] = ("查看派對與露營方案 🔥", "方塊B按鈕文字"),
                ["BoxBLink"] = ("/party", "方塊B跳轉連結"),

                // Party Page CMS Settings
                ["PartyBannerBadge"] = ("🏕️ 露營野餐 · 聚會派對 · 歡聚分享", "派對頁頂部徽章"),
                ["PartyBannerTitle"] = ("派對餐盒 ＆ 露營真空包", "派對頁頂部大標題"),
                ["PartyBannerSubtitle"] = ("BBQ Party Feast & Camping Vacuum Pack", "派對頁頂部副標"),
                ["PartyBannerDesc"] = ("聚會、野餐、露營、公司團購首選！\n主廚精心慢火煙燻熟成，大份量餐盒現點即享，真空包隔水加熱即有大師級烤肉！", "派對頁頂部說明"),

                ["PartyNoticeTitle"] = ("訂購須知與預約說明", "派對頁注意事項標題"),
                ["PartyNoticeText"] = ("📌 派對餐盒與露營真空包因製程繁複（需柴火低溫慢燻16-48小時），請至少提前 2~3 天預約。\n📌 取餐方式：可於營業時間至門市自取，或協助安排 Lalamove 外送（運費另計）。\n📌 如有特殊客製化份量或企業外燴需求，歡迎直接私訊官方 IG 或來電洽詢！", "派對頁注意事項內容"),

                ["PartySet1Badge"] = ("熱門推薦", "組合1角標"),
                ["PartySet1Portion"] = ("適合 3-4 人享用", "組合1副標籤/份量說明"),
                ["PartySet1Title"] = ("美式慢火煙燻四人分享派對盒", "組合1品名"),
                ["PartySet1Subtitle"] = ("Texas Smoked Feast Box (For 4)", "組合1英文品名"),
                ["PartySet1Desc"] = ("經典肉肉總匯！一次吃遍招牌德州燻烤豬梅花、克里奧香料雞腿排與招牌烤玉米肋排，聚會超滿足。", "組合1說明"),
                ["PartySet1Items"] = ("德州慢火燻烤豬梅花 大份 (約 300g)|克里奧香料烤雞腿排 2份|招牌美式烤玉米肋排 2份|海鹽經典美式細薯 大份|主廚特調卡羅萊納BBQ醬 2入|蒔蘿優格風味沾醬 2入", "組合1內容條列(以|分隔)"),
                ["PartySet1Price"] = ("1,080", "組合1售價"),
                ["PartySet1OriginalPrice"] = ("1,250", "組合1原價"),

                ["PartySet2Badge"] = ("肉食狂歡", "組合2角標"),
                ["PartySet2Portion"] = ("適合 6-8 人澎湃聚會", "組合2副標籤/份量說明"),
                ["PartySet2Title"] = ("主廚豪華雙牛海陸派對大盛合", "組合2品名"),
                ["PartySet2Subtitle"] = ("Chef's Deluxe Surf & Turf Platter (For 6-8)", "組合2英文品名"),
                ["PartySet2Desc"] = ("頂級德州牛胸肉搭配炭烤鮭魚菲力與鮮蝦，搭配滿滿炸物與蔬菜，派對主場最吸睛王者！", "組合2說明"),
                ["PartySet2Items"] = ("柴火48H熟成德州燻烤牛胸肉 (約 400g)|德州慢火燻烤豬梅花 大份 (約 300g)|蚵仔寮直送炭香鮭魚菲力 2份|克里奧香烤鮮蝦 12隻|黑松露醬美式細薯 大份|招牌美式烤玉米肋排 4份|炭烤時蔬拼盤 (厚切櫛瓜+杏鮑菇+甜椒)|特製醬料全套組 4入", "組合2內容條列(以|分隔)"),
                ["PartySet2Price"] = ("2,380", "組合2售價"),
                ["PartySet2OriginalPrice"] = ("2,700", "組合2原價"),

                ["PartySet3Badge"] = ("露營首選", "組合3角標"),
                ["PartySet3Portion"] = ("真空封裝 即開即享", "組合3副標籤/份量說明"),
                ["PartySet3Title"] = ("露營野餐低溫真空即享組", "組合3品名"),
                ["PartySet3Subtitle"] = ("Outdoor Camping Ready-to-Heat Pack", "組合3英文品名"),
                ["PartySet3Desc"] = ("主廚已煙燻熟成並低溫真空封裝。隔水加熱即可享受大師級烤肉！", "組合3說明"),
                ["PartySet3Items"] = ("待建製", "組合3內容條列"),

                ["PartyInquiryTitle"] = ("預約與客製化洽詢", "洽詢區標題"),
                ["PartyInquiryDesc"] = ("不論是家庭聚餐、朋友露營、公司慶生或大型活動外燴，主廚皆可為您量身規劃份量與菜色組合！", "洽詢區說明"),
                ["PartyInquiryIgText"] = ("私訊 IG 預約：@goldencorn_diner", "IG按鈕文字"),
                ["PartyInquiryIgLink"] = ("https://www.instagram.com/goldencorn_diner/", "IG連結"),
                ["PartyInquiryPhoneText"] = ("電話洽詢：0910-237-105", "電話按鈕文字"),
                ["PartyInquiryPhoneLink"] = ("tel:0910237105", "電話連結")
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
                    // 僅更新後台說明文字，絕對不覆蓋店長已修改的設定內容與文案
                    existing.Description = kvp.Value.Desc;
                    if (kvp.Key == "AdminPin" && (existing.Value == "8888" || string.IsNullOrEmpty(existing.Value)))
                    {
                        existing.Value = "Hawking";
                    }
                }
            }

            context.SaveChanges();
        }
    }
}
