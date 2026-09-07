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

            // Upsert MenuItem helper (inserts if new, updates text/price/desc if exists)
            void UpsertMenuItem(int catId, string name, string engName, decimal price, string desc, string? badge, bool reqPlate, int order, string img = "/img/2.jpg")
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
                else
                {
                    item.CategoryId = catId;
                    item.EnglishName = engName;
                    item.Price = price;
                    item.Description = desc;
                    item.Badge = badge;
                    item.RequiresPlateSides = reqPlate;
                    item.DisplayOrder = order;
                }
            }

            // 1. 美式餐盤（GOLDEN BBQ PLATE）
            UpsertMenuItem(catPlates.Id, "克里奧雞腿排", "Creole Chicken Thigh", 180, "南方香料醃製, 烤出焦香外皮與飽滿肉汁。", "人氣", true, 1, "/img/2.jpg");
            UpsertMenuItem(catPlates.Id, "古巴風烤豬排", "Cuban Grilled Pork Chop", 190, "軟嫩豬排，塗上由橙汁、檸檬、歐芹、蒜頭及橄欖油做成的Mojo青醬，清新略帶微酸。", null, true, 2, "/img/2.jpg");
            UpsertMenuItem(catPlates.Id, "德州燻烤豬梅花", "Texas Smoked Pork Collar", 190, "獨門香料慢烤16小時,香氣逼人軟嫩不油膩。", "招牌", true, 3, "/img/2.jpg");
            UpsertMenuItem(catPlates.Id, "卡津風味烤魚", "Cajun Grilled Fish", 230, "卡津為路易斯安那州的經典風味，多種香草香料混合，略帶煙燻味及溫和草本香氣。", null, true, 4, "/img/2.jpg");
            UpsertMenuItem(catPlates.Id, "德州燻烤牛胸肉", "Texas Smoked Brisket", 250, "冠軍香料柴火48小時熟成，不一定每天都有。", "主廚推薦", true, 5, "/img/2.jpg");
            UpsertMenuItem(catPlates.Id, "萊姆炭香鮭魚菲力", "Grilled Lime Salmon Fillet", 240, "蚵仔寮直送整條鮭魚只取菲力，厚切鮭魚丁口感飽滿, 外層帶有淡淡煙燻香氣。", null, true, 6, "/img/2.jpg");
            UpsertMenuItem(catPlates.Id, "克里奧鮮蝦", "Creole Shrimp", 210, "南方香料醃製, 淡淡地萊姆清香，大火炙燒脆彈鮮甜。", null, true, 7, "/img/2.jpg");

            // 2. 美墨小點（SNACKS）
            UpsertMenuItem(catSnacks.Id, "海鹽經典美式細薯", "Classic American Fries with Sea Salt", 70, "鞋帶細薯，通常在餐酒館才吃得到。", null, false, 1, "/img/4.jpg");
            UpsertMenuItem(catSnacks.Id, "美式燒烤玉米肋排", "Corn Ribs", 120, "Golden Corn招牌！沾特調酸奶醬實在太搭。", "必點招牌", false, 2, "/img/4.jpg");
            UpsertMenuItem(catSnacks.Id, "油封大蒜花椒細薯", "Fries with Confit Garlic and Chili", 90, "辣辣的。", "微辣推薦", false, 3, "/img/4.jpg");
            UpsertMenuItem(catSnacks.Id, "燻烤起司馬鈴薯", "Smoked Potato with Cheese", 60, "刷上奶油燻烤至綿密, 塞入傑克寇比起司。", null, false, 4, "/img/4.jpg");
            UpsertMenuItem(catSnacks.Id, "黑松露醬細薯", "Fries with Black Truffle Sauce", 120, "黑松露醬好貴，主廚不惜成本。", null, false, 5, "/img/4.jpg");
            UpsertMenuItem(catSnacks.Id, "美式酸奶炸雞柳條", "Chicken Tender with Yogurt", 150, "住太遠不要點，炸物不夠脆主廚會傷心。", "人氣", false, 6, "/img/4.jpg");
            UpsertMenuItem(catSnacks.Id, "楓糖辣水牛城美式炸雞柳條", "Chicken Tender with Maple Buffalo Sauce", 180, "吃過最狂的水牛城秘方(謝謝老闆艾迪) 住太遠不要點，炸物現吃才讚。", "美式經典", false, 7, "/img/4.jpg");

            // 3. 吃點蔬菜 & 最佳綠葉 & 醬料
            UpsertMenuItem(catVeggies.Id, "燒烤厚切櫛瓜", "Grilled Thick Cut Zucchini", 100, "厚切才爽。", null, false, 1, "/img/5.jpg");
            UpsertMenuItem(catVeggies.Id, "燒烤杏鮑菇", "Grilled King Oyster Mushroom", 100, "烤杏鮑菇需要耐心。", null, false, 2, "/img/5.jpg");
            UpsertMenuItem(catVeggies.Id, "燒烤甜椒青椒", "Grilled Green and Bell Pepper", 100, "甜椒好貴，但配色才美。", null, false, 3, "/img/5.jpg");

            UpsertMenuItem(catSalad.Id, "純 水耕嫩生菜杯", "Mesclun", 60, "內行人點來包肉吃。喜歡可以專門配送。", null, false, 1, "/img/6.jpg");
            UpsertMenuItem(catSalad.Id, "燒烤嫩雞胸沙拉", "Grilled Chicken Breast Salad", 150, "經典不敗，凱撒的升級低脂版！", "輕食推薦", false, 2, "/img/6.jpg");

            UpsertMenuItem(catSauces.Id, "卡羅萊納BBQ燒烤醬", "Carolina BBQ Sauce", 15, "經典道地濃郁酸甜，胡椒的微微辣是大人的口味。", null, false, 1, "/img/6.jpg");
            UpsertMenuItem(catSauces.Id, "蒔蘿優格醬", "Ranch Sauce", 15, "蒔蘿與優格特調，簡單清爽什麼都能沾。", null, false, 2, "/img/6.jpg");

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
                    ["IsOpen"] = ("true", "是否營業中 (true/false)"),
                    ["AdminPin"] = ("Hawking", "店家管理密碼"),

                    // Homepage CMS Settings
                    ["CoverImageUrl"] = ("/img/cover_poster.png", "封面海報圖片網址"),
                    ["CoverAnnouncementBtnText"] = ("門市公告與營業資訊", "封面公告按鈕文字"),
                    ["CoverSwipeBtnText"] = ("SWIPE FOR MY STORY... ↓", "封面滑動按鈕文字"),

                    ["PortalPillTag"] = ("CHOOSE YOUR ORDER TYPE", "引導頁頂部標籤"),
                    ["PortalTitle"] = ("選擇您的美味模式", "引導頁主標題"),
                    ["PortalSubtitle"] = ("外公的田間貨櫃工作室 · 柴火低溫慢燻 · 慢靈魂料理", "引導頁副標題"),
                    ["PortalNoticeBtnText"] = ("[ 門市公告與營業資訊 ]", "引導頁公告按鈕文字"),
                    ["PortalHistoryBtnText"] = ("[ 查詢我的訂單 ]", "引導頁查單按鈕文字"),

                    ["BoxABadge"] = ("[ 團體聚會 · 露營野餐 · 預約外燴 ]", "A框頂部徽章"),
                    ["BoxAIcon"] = ("/img/box_a_icon.jpg", "A框圖示/圖片"),
                    ["BoxATitle"] = ("A. 派對餐盒", "A框中文品名"),
                    ["BoxASubtitle"] = ("PARTY BOX & CATERING", "A框英文品名"),
                    ["BoxADesc"] = ("專為露營野餐、朋友聚會、公司下午茶與派對打造的大份量美式分享盛宴！", "A框說明"),
                    ["BoxABullets"] = ("燻烤肉拼盤/美式炸物\n黃金玉米肋條/嫩葉沙拉\n玉米布丁麵包/布朗尼\n手工獨門醬料\n*一對一客製化諮詢100%享受", "A框特色條列"),
                    ["BoxABtnText"] = ("探索派對餐盒", "A框按鈕文字"),
                    ["BoxALink"] = ("/party.html", "A框連結"),

                    ["BoxBBadge"] = ("[ 靈魂美式餐點 · 預約外帶 ]", "B框頂部徽章"),
                    ["BoxBIcon"] = ("/img/box_b_icon.jpg", "B框圖示/圖片"),
                    ["BoxBTitle"] = ("B. 門市菜單", "B框中文品名"),
                    ["BoxBSubtitle"] = ("DINER MENU & ORDERING", "B框英文品名"),
                    ["BoxBDesc"] = ("個人獨享或雙人經典美式餐盤！自由搭配主餐肉類、美式配菜與自選澱粉。", "B框說明"),
                    ["BoxBBullets"] = ("德州燻烤牛胸肉/克里奧雞\n美式奶油玉米/蘋果捲心菜\n*線上預約取餐時間、Line Pay快速結帳", "B框特色條列"),
                    ["BoxBBtnText"] = ("進入線上點餐", "B框按鈕文字"),
                    ["BoxBLink"] = ("/menu.html", "B框連結"),

                    ["StoryQuote"] = ("「世界太快，我們在田裡為你製造一點慢靈魂。」", "底部金句名言"),
                    ["StoryAuthor"] = ("STAY GOLDEN · 慢靈魂製造所 · STAY HUNGRY", "底部名言署名"),

                    ["NoticeDelivery"] = ("• 本店餐點皆為低溫慢火柴燻，建議提前預約以確保肉品庫存。\n• 外送服務：以 LALAMOVE 平台配送（例如 7.5 公里機車外送約 140 元）。\n• 試營運優惠：消費滿 $600 免運！", "外送與優惠說明"),
                    ["NoticeContact"] = ("企業商務套餐、下午茶點心團購、野餐派對包，請私訊 IG 粉專由主廚一對一服務：\n📸 IG: @goldencorn_diner\n📞 電話: 0910-237-105", "企業團訂與聯絡說明"),

                    // Party Box Page Settings
                    ["PartyHeroBadge"] = ("PARTY BOX & CATERING", "派對頁橫幅小標籤"),
                    ["PartyHeroTitle"] = ("美式煙燻派對餐盒", "派對頁大標題"),
                    ["PartyHeroSubtitle"] = ("露營野餐 · 朋友歡聚 · 企業團訂 · 生日派對的大份量美式靈魂盛宴！", "派對頁副標題"),
                    ["PartyHeroTags"] = ("原木柴燒慢燻 | 美式靈魂炸物 | 招牌黃金玉米", "派對頁特色標籤"),
                    ["PartyNoticeTitle"] = ("【派對餐盒預訂須知】", "派對頁預訂須知標題"),
                    ["PartyNoticeText"] = ("前 2~3 天 預訂，讓主廚有充裕時間備料與長時間慢火煙燻。目前線上菜單自選功能籌備中，歡迎直接透過 IG 或電話與主廚預訂！", "派對頁預訂須知內容"),

                    ["PartySet1Label"] = ("組合 1", "組合1方案名稱/編號標籤"),
                    ["PartySet1Badge"] = ("BBQ COMBO人氣首選 4~6人", "組合1特色標籤"),
                    ["PartySet1Portion"] = ("大份量肉品拼盤", "組合1副標籤/份量說明"),
                    ["PartySet1Title"] = ("德州慢燻狂歡肉品盛宴盒", "組合1品名"),
                    ["PartySet1Subtitle"] = ("Texas Smoked BBQ Carnivore Feast", "組合1英文品名"),
                    ["PartySet1Desc"] = ("肉食愛好者的終極救贖！一次品嚐三種經典低溫煙燻肉品。", "組合1說明"),
                    ["PartySet1Items"] = ("待建製", "組合1內容條列"),

                    ["PartySet2Label"] = ("組合 2", "組合2方案名稱/編號標籤"),
                    ["PartySet2Badge"] = ("HIGH PARTY聚會必點 6~12人", "組合2特色標籤"),
                    ["PartySet2Portion"] = ("美式炸物 & 燒烤玉米", "組合2副標籤/份量說明"),
                    ["PartySet2Title"] = ("美式靈魂 & 黃金玉米歡聚組", "組合2品名"),
                    ["PartySet2Subtitle"] = ("Soul Fried Chicken & Corn Ribs Box", "組合2英文品名"),
                    ["PartySet2Desc"] = ("美式靈魂炸物，搭配超人氣炭烤玉米肋排。", "組合2說明"),
                    ["PartySet2Items"] = ("待建製", "組合2內容條列"),

                    ["PartySet3Label"] = ("組合 3", "組合3方案名稱/編號標籤"),
                    ["PartySet3Badge"] = ("戶外露營神器", "組合3特色標籤"),
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
                    existing.Description = kvp.Value.Desc;
                    if (kvp.Key == "AdminPin")
                    {
                        if (existing.Value == "8888" || string.IsNullOrEmpty(existing.Value))
                        {
                            existing.Value = "Hawking";
                        }
                    }
                    else
                    {
                        // 自動將資料庫內的舊預設值同步為最新固定文案
                        existing.Value = kvp.Value.Value;
                    }
                }
            }

            context.SaveChanges();
        }
    }
}
