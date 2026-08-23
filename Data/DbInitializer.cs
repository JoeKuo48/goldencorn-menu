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

            // Seed Plate Items
            void EnsureMenuItem(int catId, string name, string engName, decimal price, string desc, string? badge, bool reqPlate, int order, string img = "/img/2.jpg")
            {
                if (!context.MenuItems.Any(m => m.Name == name))
                {
                    var item = new MenuItem
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

            // 1. 美式餐盤
            EnsureMenuItem(catPlates.Id, "克里奧雞腿排", "Creole Chicken Thigh", 180, "特選鮮嫩雞腿排，以路易斯安那克里奧辛香料醃製炭烤", "人氣", true, 1, "/img/2.jpg");
            EnsureMenuItem(catPlates.Id, "古巴風烤豬排", "Cuban Grilled Pork Chop", 190, "經典古巴柑橘大蒜風味醃汁，高溫炙烤鎖住鮮甜肉汁", null, true, 2, "/img/2.jpg");
            EnsureMenuItem(catPlates.Id, "德州燻烤豬梅花", "Texas Smoked Pork Collar", 190, "低溫慢火果木柴燻，油花均勻軟嫩多汁，道地德州風味", "招牌", true, 3, "/img/2.jpg");
            EnsureMenuItem(catPlates.Id, "卡津風味烤魚", "Cajun Grilled Fish", 230, "精選厚切魚排，搭配香濃卡津香料炙烤，外香裡嫩", null, true, 4, "/img/2.jpg");
            EnsureMenuItem(catPlates.Id, "德州燻烤牛胸肉", "Texas Smoked Brisket", 250, "經典德州炭火低溫慢燻牛胸肉，濃郁燻香入口即化", "主廚推薦", true, 5, "/img/2.jpg");
            EnsureMenuItem(catPlates.Id, "萊姆炭香鮭魚菲力", "Grilled Lime Salmon Fillet", 240, "頂級鮭魚菲力炭烤佐新鮮青檸檬檬汁，油脂豐厚香氣誘人", null, true, 6, "/img/2.jpg");
            EnsureMenuItem(catPlates.Id, "克里奧鮮蝦", "Creole Shrimp", 210, "大尾肥美鮮蝦拌入主廚特調紐奧良克里奧香料快火炙燒", null, true, 7, "/img/2.jpg");

            // 2. 美墨小點
            EnsureMenuItem(catSnacks.Id, "海鹽經典美式細薯", "Classic American Fries with Sea Salt", 70, "特選金黃細薯炸至酥脆，灑上海洋純淨海鹽", null, false, 1, "/img/4.jpg");
            EnsureMenuItem(catSnacks.Id, "美式燒烤玉米肋排", "Corn Ribs", 120, "Golden Corn 招牌！新鮮甜玉米刀切彎曲燒烤，抹上秘製美式烤肉醬", "必點招牌", false, 2, "/img/4.jpg");
            EnsureMenuItem(catSnacks.Id, "油封大蒜花椒細薯", "Fries with Confit Garlic and Chili", 90, "慢火油封熟成大蒜碎粒，融入特製四川大紅袍花椒香氣", "微辣推薦", false, 3, "/img/4.jpg");
            EnsureMenuItem(catSnacks.Id, "燻烤起司馬鈴薯", "Smoked Potato with Cheese", 60, "原粒馬鈴薯木炭慢燻，淋上濃郁融化切達起司醬", null, false, 4, "/img/4.jpg");
            EnsureMenuItem(catSnacks.Id, "黑松露醬細薯", "Fries with Black Truffle Sauce", 120, "酥脆細薯搭配主廚特調黑松露美乃滋沾醬", null, false, 5, "/img/4.jpg");
            EnsureMenuItem(catSnacks.Id, "美式酸奶炸雞柳條", "Chicken Tender with Yogurt", 150, "現炸外酥內嫩純雞柳條，沾裹清爽優格酸奶醬", "人氣", false, 6, "/img/4.jpg");
            EnsureMenuItem(catSnacks.Id, "楓糖辣水牛城美式炸雞柳條", "Chicken Tender with Maple Buffalo Sauce", 180, "美式水牛城辣醬結合加拿大純楓糖，甜辣過癮", "美式經典", false, 7, "/img/4.jpg");

            // 3. 吃點蔬菜
            EnsureMenuItem(catVeggies.Id, "燒烤厚切櫛瓜", "Grilled Thick Cut Zucchini", 100, "嚴選新鮮綠櫛瓜厚切，直火炭烤保留飽滿清甜水分", null, false, 1, "/img/5.jpg");
            EnsureMenuItem(catVeggies.Id, "燒烤杏鮑菇", "Grilled King Oyster Mushroom", 100, "多汁厚實杏鮑菇，刷上微甜烤醬炭火燒烤", null, false, 2, "/img/5.jpg");
            EnsureMenuItem(catVeggies.Id, "燒烤甜椒青椒", "Grilled Green and Bell Pepper", 100, "紅黃彩椒與翠綠青椒高溫炭烤，甜脆爽口", null, false, 3, "/img/5.jpg");

            // 4. 最佳綠葉 (SALAD)
            EnsureMenuItem(catSalad.Id, "純 水耕嫩生菜杯", "Mesclun", 60, "嚴選水耕嫩葉綜合生菜，鮮嫩爽脆甘甜", null, false, 1, "/img/6.jpg");
            EnsureMenuItem(catSalad.Id, "燒烤嫩雞胸沙拉", "Grilled Chicken Breast Salad", 150, "低脂低卡炭火炙燒嫩雞胸，搭配豐富新鮮生菜", "輕食推薦", false, 2, "/img/6.jpg");

            // 5. 醬料 (SAUCE)
            EnsureMenuItem(catSauces.Id, "卡羅萊納BBQ燒烤醬", "Carolina BBQ Sauce", 15, "美式卡羅萊納經典酸甜微辣燒烤沾醬，肉品絕配", null, false, 1, "/img/6.jpg");
            EnsureMenuItem(catSauces.Id, "蒔蘿優格醬", "Ranch Sauce", 15, "新鮮蒔蘿香草結合濃郁優格酸奶，清新解膩", null, false, 2, "/img/6.jpg");

            context.SaveChanges();

            // Settings check & update
            var defaultSettings = new Dictionary<string, (string Value, string Desc)>
            {
                ["StoreName"] = ("Golden Corn 後勁店", "店家名稱"),
                ["BrandSub"] = ("後勁 Houjing · Texas Smoked BBQ & Soul Food", "副標題/品牌標語"),
                ["Phone"] = ("0910237105", "門市電話"),
                ["Address"] = ("高雄市楠梓區金富街85-1號", "門市地址"),
                ["BusinessHours"] = ("週四至週六 16:30-22:30 [週日至週三僅接受線上提前預點] ----- *有任何問題，如企業商務套餐、下午茶點心團購等等，請私訊IG粉專，由主廚一對一服務 IG: goldencorn_diner", "營業時間"),
                ["Announcement"] = ("🔥 歡迎光臨 Golden Corn！美式德州慢火燻烤、靈魂料理，餐點現點現烤，感謝您的耐心等候。", "前台跑馬燈公告"),
                ["StoreTip"] = ("💡 美式慢火柴燻牛胸肉、豬梅花每日限量供應，餐點現點現烤，建議提前線上預約取餐！", "門市資訊燈泡備註/溫馨提示"),
                ["BankCode"] = ("822", "銀行代碼"),
                ["BankName"] = ("中國信託", "銀行名稱"),
                ["BankAccount"] = ("129540943647", "銀行帳號"),
                ["BankAccountName"] = ("Golden Corn 後勁店", "銀行戶名"),
                ["IsOpen"] = ("true", "是否營業中 (true/false)"),
                ["AdminPin"] = ("8888", "店家管理密碼/PIN碼")
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
                    // Update default values for phone, address, business hours, and store tip if not modified
                    if (kvp.Key == "Phone" || kvp.Key == "Address" || kvp.Key == "BusinessHours" || kvp.Key == "StoreTip" || kvp.Key == "BankCode" || kvp.Key == "BankAccount")
                    {
                        existing.Value = kvp.Value.Value;
                    }
                }
            }

            context.SaveChanges();
        }
    }
}
