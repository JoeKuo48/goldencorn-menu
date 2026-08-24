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
                    existing.Value = kvp.Value.Value;
                }
            }

            context.SaveChanges();
        }
    }
}
