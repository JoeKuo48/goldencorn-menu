/**
 * Golden Corn - Customer Web App (app.js)
 * Modern, responsive ordering experience with live cart, payments, and order tracking.
 */

// State
let menuData = [];
let storeSettings = {};
let cart = [];
let currentPlateItem = null;
let selectedOptionIds = [];
let currentModalQty = 1;
let currentTrackingOrderNumber = null;
let trackingInterval = null;
let pendingOrderPayload = null;

// Initialize
document.addEventListener("DOMContentLoaded", () => {
    loadSettings();
    loadMenu();
    loadCartFromStorage();
    setupEventListeners();
    checkUrlForOrderTracking();
});

// 1. Load Settings & Render Store Information
async function loadSettings() {
    try {
        const res = await fetch("/api/settings");
        if (res.ok) {
            storeSettings = await res.json();
            renderStoreSettings();
        }
    } catch (err) {
        console.error("Failed to load settings:", err);
    }
}

function renderStoreSettings() {
    const annEl = document.getElementById("announcementText");
    if (annEl && storeSettings.Announcement) {
        annEl.textContent = storeSettings.Announcement;
    }
    
    const isOpen = storeSettings.IsOpen === "true" || storeSettings.IsOpen === true;
    const statusPill = document.getElementById("storeStatusPill");
    if (statusPill) {
        statusPill.className = `status-pill ${isOpen ? 'open' : 'closed'}`;
        statusPill.innerHTML = `<span class="status-dot"></span>${isOpen ? '營業中 · 現點現烤' : '今日已打烊'}`;
    }

    if (document.getElementById("storeInfoName") && storeSettings.StoreName) {
        document.getElementById("storeInfoName").textContent = storeSettings.StoreName;
    }
    if (document.getElementById("storeInfoBrandTitle") && storeSettings.StoreName) {
        document.getElementById("storeInfoBrandTitle").textContent = storeSettings.StoreName;
    }
    if (document.getElementById("storeInfoStyle") && storeSettings.BrandSub) {
        document.getElementById("storeInfoStyle").textContent = storeSettings.BrandSub;
    }
    if (document.getElementById("storeInfoPhone") && storeSettings.Phone) {
        document.getElementById("storeInfoPhone").textContent = storeSettings.Phone;
    }
    if (document.getElementById("storeInfoAddress") && storeSettings.Address) {
        document.getElementById("storeInfoAddress").textContent = storeSettings.Address;
    }
    if (document.getElementById("storeInfoHours") && storeSettings.BusinessHours) {
        document.getElementById("storeInfoHours").textContent = storeSettings.BusinessHours;
    }
    if (document.getElementById("storeInfoTip") && storeSettings.StoreTip) {
        document.getElementById("storeInfoTip").textContent = storeSettings.StoreTip;
    }
}

// 2. Load Menu
async function loadMenu() {
    const menuContainer = document.getElementById("menuContainer");
    if (!menuContainer) return;
    
    menuContainer.innerHTML = `
        <div style="text-align:center; padding: 40px 0; color: var(--gc-green-primary);">
            <div style="font-size:24px; font-weight:800;">🌽 菜單載入中...</div>
            <div style="font-size:13px; color:var(--gc-text-muted); margin-top:6px;">Delicious Texas BBQ is loading</div>
        </div>
    `;

    try {
        const res = await fetch("/api/menu");
        if (!res.ok) throw new Error("Failed to fetch menu");
        menuData = await res.json();
        renderMenuTabs();
        renderMenuItems();
    } catch (err) {
        console.error("Error loading menu:", err);
        menuContainer.innerHTML = `
            <div style="text-align:center; padding: 40px 0; color: #b91c1c;">
                <div style="font-size:18px; font-weight:800;">載入菜單失敗，請重新整理頁面</div>
            </div>
        `;
    }
}

// 3. Render Menu Tabs
function renderMenuTabs() {
    const tabsContainer = document.getElementById("categoryTabs");
    if (!tabsContainer) return;

    let html = `<button class="tab-btn active" data-cat="all" onclick="filterCategory('all', this)">🔥 全部餐點</button>`;
    
    menuData.forEach(cat => {
        let icon = "🍖";
        if (cat.name.includes("小點") || cat.name.includes("點心")) icon = "🍟";
        if (cat.name.includes("蔬菜")) icon = "🥦";
        if (cat.name.includes("綠葉") || cat.name.includes("沙拉")) icon = "🥗";
        if (cat.name.includes("醬料")) icon = "🥣";
        html += `<button class="tab-btn" data-cat="${cat.id}" onclick="filterCategory('${cat.id}', this)">${icon} ${cat.name}</button>`;
    });

    tabsContainer.innerHTML = html;
}

// 4. Render Menu Items
function renderMenuItems(filterCatId = "all", searchQuery = "") {
    const container = document.getElementById("menuContainer");
    if (!container) return;

    let html = "";

    menuData.forEach(cat => {
        if (filterCatId !== "all" && cat.id.toString() !== filterCatId.toString()) {
            return;
        }

        let items = cat.menuItems || [];
        if (searchQuery.trim()) {
            const q = searchQuery.toLowerCase().trim();
            items = items.filter(i => 
                i.name.toLowerCase().includes(q) || 
                (i.englishName && i.englishName.toLowerCase().includes(q)) ||
                (i.description && i.description.toLowerCase().includes(q))
            );
        }

        if (items.length === 0) return;

        html += `
            <section class="category-section" id="cat-section-${cat.id}">
                <div class="category-header">
                    <h2 class="category-title">${cat.name}</h2>
                    <div class="category-subtitle">${cat.englishName}</div>
                    ${cat.description ? `<p class="category-note">${cat.description}</p>` : ''}
                </div>
        `;

        if (cat.name.includes("美式餐盤")) {
            html += `
                <div class="plate-included-banner">
                    <span class="plate-included-icon">🥗</span>
                    <div>
                        <strong>美式餐盤皆附：</strong>嫩葉生菜 + 美式自選配料（5選1，可於點餐時挑選或加購小米飯）
                    </div>
                </div>
            `;
        }

        html += `<div class="menu-grid">`;

        items.forEach(item => {
            const isPlateCat = cat.name && cat.name.includes("餐盤");
            const hasOptions = item.requiresPlateSides || isPlateCat || (item.optionGroups && item.optionGroups.length > 0);

            html += `
                <div class="menu-card" data-id="${item.id}" onclick="handleItemClick(${item.id})">
                    <div>
                        <div class="card-top">
                            <div>
                                <div class="item-name">${item.name}</div>
                                <div class="item-english">${item.englishName || ''}</div>
                            </div>
                            ${item.badge ? `<span class="item-badge">${item.badge}</span>` : ''}
                        </div>
                        <p class="item-description">${item.description || ''}</p>
                    </div>
                    <div class="card-bottom">
                        <div class="item-price">
                            <span class="item-price-unit">NT$</span>${item.price}
                        </div>
                        ${item.isAvailable ? `
                            <button class="btn-add-item" onclick="event.stopPropagation(); handleItemClick(${item.id})">
                                <span>${hasOptions ? '選擇配料' : '加入'}</span>
                                <span style="font-size:16px; margin-left:2px;">+</span>
                            </button>
                        ` : `
                            <button class="btn-add-item sold-out" disabled onclick="event.stopPropagation();">已售完</button>
                        `}
                    </div>
                </div>
            `;
        });

        html += `</div></section>`;
    });

    if (html === "") {
        html = `
            <div style="text-align:center; padding: 60px 0; color: var(--gc-text-muted);">
                <div style="font-size:32px; margin-bottom:8px;">🔍</div>
                <div style="font-size:16px; font-weight:700;">找不到符合的餐點</div>
                <div style="font-size:13px; margin-top:4px;">請嘗試更換關鍵字或點選其他分類</div>
            </div>
        `;
    }

    container.innerHTML = html;
}

function filterCategory(catId, btnElement) {
    document.querySelectorAll(".tab-btn").forEach(b => b.classList.remove("active"));
    if (btnElement) btnElement.classList.add("active");

    const searchInput = document.getElementById("menuSearchInput");
    const query = searchInput ? searchInput.value : "";
    renderMenuItems(catId, query);
}

function toggleSearchBox() {
    const box = document.getElementById("searchBoxContainer");
    const input = document.getElementById("menuSearchInput");
    if (box) {
        box.classList.toggle("show");
        if (box.classList.contains("show") && input) {
            input.focus();
        }
    }
}

function handleSearchInput(input) {
    const activeTab = document.querySelector(".tab-btn.active");
    const catId = activeTab ? activeTab.getAttribute("data-cat") : "all";
    renderMenuItems(catId, input.value);
}

function openStoreInfoModal() {
    const backdrop = document.getElementById("storeInfoModalBackdrop");
    if (backdrop) backdrop.classList.add("show");
}

function closeStoreInfoModal() {
    const backdrop = document.getElementById("storeInfoModalBackdrop");
    if (backdrop) backdrop.classList.remove("show");
}

// 5. Handle Item Add / Modal Popup
function handleItemClick(itemId) {
    let targetItem = null;
    let parentCat = null;
    for (let cat of menuData) {
        let found = (cat.menuItems || []).find(i => i.id === itemId);
        if (found) {
            targetItem = found;
            parentCat = cat;
            break;
        }
    }

    if (!targetItem) return;

    const isPlateCat = parentCat && parentCat.name.includes("餐盤");
    const hasOptions = targetItem.requiresPlateSides || isPlateCat || (targetItem.optionGroups && targetItem.optionGroups.length > 0);

    if (hasOptions) {
        openPlateOptionsModal(targetItem);
    } else {
        addToCart({
            menuItemId: targetItem.id,
            name: targetItem.name,
            englishName: targetItem.englishName,
            price: targetItem.price,
            quantity: 1,
            selectedOptions: [],
            selectedOptionItemIds: [],
            itemNote: "",
            optionsSummary: ""
        });
        showToast(`已將「${targetItem.name}」加入購物車`);
    }
}

function openPlateOptionsModal(item) {
    currentPlateItem = item;
    selectedOptionIds = [];
    currentModalQty = 1;

    if (item.optionGroups) {
        item.optionGroups.forEach(g => {
            if (g.isRequired && g.options && g.options.length > 0) {
                const firstAvailable = g.options.find(o => o.isAvailable);
                if (firstAvailable) {
                    selectedOptionIds.push(firstAvailable.id);
                }
            }
        });
    }

    const nameEl = document.getElementById("modalItemName");
    if (nameEl) nameEl.textContent = item.name;

    const engEl = document.getElementById("modalItemEnglish");
    if (engEl) engEl.textContent = item.englishName || "";

    const noteEl = document.getElementById("modalItemNote");
    if (noteEl) noteEl.value = "";

    const qtyEl = document.getElementById("modalQtyDisplay");
    if (qtyEl) qtyEl.textContent = "1";

    renderOptionGroupsInModal(item);
    updateModalPriceCalculation();

    const backdrop = document.getElementById("optionsModalBackdrop");
    if (backdrop) {
        backdrop.classList.add("show");
    }
}

function closeOptionsModal() {
    const backdrop = document.getElementById("optionsModalBackdrop");
    if (backdrop) {
        backdrop.classList.remove("show");
    }
    currentPlateItem = null;
}

function renderOptionGroupsInModal(item) {
    const container = document.getElementById("modalOptionGroupsContainer");
    if (!container) return;

    let html = "";

    if (item.requiresPlateSides || (item.name && item.name.includes("排")) || (item.name && item.name.includes("牛胸肉"))) {
        html += `
            <div style="background:#eef3ec; padding:10px 12px; border-radius:8px; font-size:13px; color:var(--gc-green-dark); margin-bottom:14px; display:flex; align-items:center; gap:6px;">
                <span>🥗</span> <strong>標配：</strong>美式餐盤皆附一份【嫩葉生菜】
            </div>
        `;
    }

    if (item.optionGroups && item.optionGroups.length > 0) {
        item.optionGroups.forEach(group => {
            const isSingleChoice = group.maxSelect === 1;

            html += `
                <div class="option-group-box">
                    <div class="option-group-title">
                        <span>${group.name} <small style="color:var(--gc-text-muted); font-weight:600;">(${group.englishName})</small></span>
                        <span class="option-group-badge ${group.isRequired ? 'badge-required' : 'badge-optional'}">
                            ${group.isRequired ? '必選 1 項' : '限量加購 (可不選)'}
                        </span>
                    </div>
            `;

            (group.options || []).forEach(opt => {
                const isChecked = selectedOptionIds.includes(opt.id);
                const isAvailable = opt.isAvailable;

                html += `
                    <div class="option-item-row ${isChecked ? 'selected' : ''} ${!isAvailable ? 'sold-out' : ''}" 
                         onclick="handleOptionSelection(${group.id}, ${opt.id}, ${isSingleChoice}, ${isAvailable})">
                        <div class="option-item-label">
                            <input type="${isSingleChoice ? 'radio' : 'checkbox'}" 
                                   name="opt_group_${group.id}" 
                                   value="${opt.id}" 
                                   ${isChecked ? 'checked' : ''} 
                                   ${!isAvailable ? 'disabled' : ''} 
                                   style="accent-color: var(--gc-green-primary);"
                                   onclick="event.stopPropagation(); handleOptionSelection(${group.id}, ${opt.id}, ${isSingleChoice}, ${isAvailable});" />
                            <div>
                                <div>${opt.name} <small style="color:var(--gc-text-muted); font-size:11px;">${opt.englishName}</small></div>
                                ${opt.tag ? `<span style="font-size:10px; color:#b45309; background:#fef3c7; padding:1px 5px; border-radius:4px;">${opt.tag}</span>` : ''}
                            </div>
                        </div>
                        <div class="option-item-price">
                            ${!isAvailable ? '<span style="color:#b91c1c;">已售完</span>' : (opt.extraPrice > 0 ? `+NT$ ${opt.extraPrice}` : '+NT$ 0')}
                        </div>
                    </div>
                `;
            });

            html += `</div>`;
        });
    }

    container.innerHTML = html;
}

function handleOptionSelection(groupId, optionId, isSingleChoice, isAvailable) {
    if (!isAvailable) {
        showToast("該配料目前已售完");
        return;
    }

    if (!currentPlateItem) return;
    const group = currentPlateItem.optionGroups.find(g => g.id === groupId);
    if (!group) return;

    if (isSingleChoice) {
        const groupOptionIds = group.options.map(o => o.id);
        selectedOptionIds = selectedOptionIds.filter(id => !groupOptionIds.includes(id));
        selectedOptionIds.push(optionId);
    } else {
        const idx = selectedOptionIds.indexOf(optionId);
        if (idx > -1) {
            selectedOptionIds.splice(idx, 1);
        } else {
            selectedOptionIds.push(optionId);
        }
    }

    renderOptionGroupsInModal(currentPlateItem);
    updateModalPriceCalculation();
}

function adjustModalQty(delta) {
    currentModalQty = Math.max(1, currentModalQty + delta);
    const qtyEl = document.getElementById("modalQtyDisplay");
    if (qtyEl) qtyEl.textContent = currentModalQty;
    updateModalPriceCalculation();
}

function updateModalPriceCalculation() {
    if (!currentPlateItem) return;

    let basePrice = currentPlateItem.price;
    let extraPrice = 0;

    if (currentPlateItem.optionGroups) {
        currentPlateItem.optionGroups.forEach(g => {
            (g.options || []).forEach(o => {
                if (selectedOptionIds.includes(o.id)) {
                    extraPrice += o.extraPrice;
                }
            });
        });
    }

    let unitTotal = basePrice + extraPrice;
    let grandTotal = unitTotal * currentModalQty;

    const totalEl = document.getElementById("modalTotalPrice");
    if (totalEl) totalEl.textContent = grandTotal;
}

function confirmAddPlateModal() {
    if (!currentPlateItem) return;

    if (currentPlateItem.optionGroups) {
        for (let group of currentPlateItem.optionGroups) {
            if (group.isRequired) {
                const groupOptionIds = group.options.map(o => o.id);
                const hasSelected = selectedOptionIds.some(id => groupOptionIds.includes(id));
                if (!hasSelected) {
                    showToast(`請選擇「${group.name}」`);
                    return;
                }
            }
        }
    }

    const noteEl = document.getElementById("modalItemNote");
    const itemNote = noteEl ? noteEl.value.trim() : "";
    const selectedOptionsDetails = [];
    const summaryList = [];

    if (currentPlateItem.optionGroups) {
        currentPlateItem.optionGroups.forEach(g => {
            (g.options || []).forEach(o => {
                if (selectedOptionIds.includes(o.id)) {
                    selectedOptionsDetails.push({
                        optionGroupId: g.id,
                        optionGroupName: g.name,
                        optionItemId: o.id,
                        optionName: o.name,
                        optionEnglishName: o.englishName,
                        extraPrice: o.extraPrice
                    });
                    summaryList.push(`${g.name}: ${o.name}${o.extraPrice > 0 ? ` (+NT$${o.extraPrice})` : ''}`);
                }
            });
        });
    }

    addToCart({
        menuItemId: currentPlateItem.id,
        name: currentPlateItem.name,
        englishName: currentPlateItem.englishName,
        price: currentPlateItem.price,
        quantity: currentModalQty,
        selectedOptions: selectedOptionsDetails,
        selectedOptionItemIds: [...selectedOptionIds],
        itemNote: itemNote,
        optionsSummary: summaryList.join(" | ")
    });

    closeOptionsModal();
    showToast(`已將「${currentPlateItem.name}」加入購物車`);
}

// 6. Cart Management
function addToCart(newItem) {
    const existingIndex = cart.findIndex(i => 
        i.menuItemId === newItem.menuItemId &&
        i.optionsSummary === newItem.optionsSummary &&
        i.itemNote === newItem.itemNote
    );

    if (existingIndex > -1) {
        cart[existingIndex].quantity += newItem.quantity;
    } else {
        cart.push(newItem);
    }

    saveCartToStorage();
    updateCartUI();
}

function updateCartItemQty(index, delta) {
    if (cart[index]) {
        cart[index].quantity += delta;
        if (cart[index].quantity <= 0) {
            cart.splice(index, 1);
        }
        saveCartToStorage();
        updateCartUI();
        renderCartModalItems();
    }
}

function removeCartItem(index) {
    if (cart[index]) {
        cart.splice(index, 1);
        saveCartToStorage();
        updateCartUI();
        renderCartModalItems();
    }
}

function clearCart() {
    cart = [];
    saveCartToStorage();
    updateCartUI();
    renderCartModalItems();
}

function saveCartToStorage() {
    localStorage.setItem("gc_cart", JSON.stringify(cart));
}

function loadCartFromStorage() {
    try {
        const stored = localStorage.getItem("gc_cart");
        if (stored) {
            cart = JSON.parse(stored);
            updateCartUI();
        }
    } catch (e) {
        cart = [];
    }
}

function calculateCartTotals() {
    let totalQty = 0;
    let grandTotal = 0;

    cart.forEach(item => {
        totalQty += item.quantity;
        let unitOptionExtra = (item.selectedOptions || []).reduce((sum, o) => sum + o.extraPrice, 0);
        let itemTotal = (item.price + unitOptionExtra) * item.quantity;
        grandTotal += itemTotal;
    });

    return { totalQty, grandTotal };
}

function updateCartUI() {
    const { totalQty, grandTotal } = calculateCartTotals();
    const bottomBar = document.getElementById("bottomCartBar");
    const countBubble = document.getElementById("cartCountBubble");
    const totalDisplay = document.getElementById("cartTotalDisplay");

    if (totalQty > 0) {
        if (bottomBar) bottomBar.classList.add("show");
        if (countBubble) countBubble.textContent = totalQty;
        if (totalDisplay) totalDisplay.textContent = grandTotal;
    } else {
        if (bottomBar) bottomBar.classList.remove("show");
    }
}

function openCartModal() {
    if (cart.length === 0) {
        showToast("購物車目前是空的");
        return;
    }
    renderCartModalItems();
    const backdrop = document.getElementById("cartModalBackdrop");
    if (backdrop) backdrop.classList.add("show");
}

function closeCartModal() {
    const backdrop = document.getElementById("cartModalBackdrop");
    if (backdrop) backdrop.classList.remove("show");
}

function renderCartModalItems() {
    const list = document.getElementById("cartModalItemsList");
    if (!list) return;

    if (cart.length === 0) {
        list.innerHTML = `<div style="text-align:center; padding:30px; color:var(--gc-text-muted);">購物車是空的</div>`;
        closeCartModal();
        return;
    }

    let html = "";
    let grandTotal = 0;

    cart.forEach((item, index) => {
        let unitOptionExtra = (item.selectedOptions || []).reduce((sum, o) => sum + o.extraPrice, 0);
        let itemTotal = (item.price + unitOptionExtra) * item.quantity;
        grandTotal += itemTotal;

        html += `
            <div class="cart-item-card">
                <div style="flex:1;">
                    <div class="cart-item-name">${item.name}</div>
                    ${item.optionsSummary ? `<div class="cart-item-options">${item.optionsSummary}</div>` : ''}
                    ${item.itemNote ? `<div class="cart-item-note">備註：${item.itemNote}</div>` : ''}
                </div>
                <div style="text-align:right; display:flex; flex-direction:column; justify-content:space-between; align-items:flex-end;">
                    <div class="cart-item-price">NT$ ${itemTotal}</div>
                    <div class="quantity-controller">
                        <button class="btn-qty" onclick="updateCartItemQty(${index}, -1)">-</button>
                        <span class="qty-display">${item.quantity}</span>
                        <button class="btn-qty" onclick="updateCartItemQty(${index}, 1)">+</button>
                    </div>
                </div>
            </div>
        `;
    });

    list.innerHTML = html;
    const subtotalEl = document.getElementById("cartSummarySubtotal");
    if (subtotalEl) subtotalEl.textContent = `NT$ ${grandTotal}`;
    const totalEl = document.getElementById("cartSummaryTotal");
    if (totalEl) totalEl.textContent = `NT$ ${grandTotal}`;
}

function setDiningType(type) {
    const btnTakeout = document.getElementById("btnDiningTakeout");
    const btnDineIn = document.getElementById("btnDiningDineIn");
    const tableGroup = document.getElementById("dineInTableGroup");
    const hiddenInput = document.getElementById("orderDiningType");

    if (type === "Takeout") {
        if (btnTakeout) btnTakeout.classList.add("active");
        if (btnDineIn) btnDineIn.classList.remove("active");
        if (tableGroup) tableGroup.style.display = "none";
        if (hiddenInput) hiddenInput.value = "Takeout";
    } else {
        if (btnDineIn) btnDineIn.classList.add("active");
        if (btnTakeout) btnTakeout.classList.remove("active");
        if (tableGroup) tableGroup.style.display = "block";
        if (hiddenInput) hiddenInput.value = "DineIn";
    }
}

function setPaymentMethod(method) {
    document.querySelectorAll(".btn-payment-option").forEach(b => b.classList.remove("active"));
    const btn = document.querySelector(`.btn-payment-option[data-method="${method}"]`);
    if (btn) btn.classList.add("active");
    const input = document.getElementById("orderPaymentMethod");
    if (input) input.value = method;
}

// 7. Checkout & Payment Confirmation
async function submitOrder() {
    if (cart.length === 0) {
        showToast("購物車為空");
        return;
    }

    const name = (document.getElementById("orderCustomerName")?.value || "").trim();
    const phone = (document.getElementById("orderCustomerPhone")?.value || "").trim();
    const diningType = document.getElementById("orderDiningType")?.value || "Takeout";
    const tableNum = (document.getElementById("orderTableNumber")?.value || "").trim();
    const pickupTime = (document.getElementById("orderPickupTime")?.value || "").trim();
    const paymentMethod = document.getElementById("orderPaymentMethod")?.value || "Cash";
    const customerNote = (document.getElementById("orderCustomerNote")?.value || "").trim();

    if (!name) {
        showToast("請填寫訂購人姓名");
        document.getElementById("orderCustomerName")?.focus();
        return;
    }

    if (!phone || phone.length < 8) {
        showToast("請填寫正確的聯絡電話 (方便取餐通知)");
        document.getElementById("orderCustomerPhone")?.focus();
        return;
    }

    if (diningType === "DineIn" && !tableNum) {
        showToast("內用請輸入桌號");
        document.getElementById("orderTableNumber")?.focus();
        return;
    }

    const { grandTotal } = calculateCartTotals();

    pendingOrderPayload = {
        customerName: name,
        customerPhone: phone,
        diningType: diningType,
        tableNumber: diningType === "DineIn" ? tableNum : null,
        pickupTime: pickupTime || "儘速製作",
        paymentMethod: paymentMethod,
        customerNote: customerNote,
        items: cart.map(item => ({
            menuItemId: item.menuItemId,
            quantity: item.quantity,
            itemNote: item.itemNote,
            selectedOptionItemIds: item.selectedOptionItemIds || (item.selectedOptions || []).map(o => o.optionItemId)
        }))
    };

    // If Cash: submit directly
    if (paymentMethod === "Cash") {
        await executeSubmitOrder(pendingOrderPayload);
        return;
    }

    // If LINE Pay or Bank Transfer: Open Payment Confirmation Modal
    closeCartModal();
    openPaymentModal(paymentMethod, grandTotal);
}

function openPaymentModal(method, amount) {
    document.getElementById("paymentDueAmount").textContent = `NT$ ${amount}`;
    const secLinePay = document.getElementById("paymentSectionLinePay");
    const secBank = document.getElementById("paymentSectionBankTransfer");
    const modalTitle = document.getElementById("paymentModalTitle");

    if (method === "LinePay") {
        modalTitle.textContent = "LINE Pay 掃碼付款";
        secLinePay.style.display = "block";
        secBank.style.display = "none";
    } else if (method === "BankTransfer") {
        modalTitle.textContent = "銀行轉帳付款";
        secLinePay.style.display = "none";
        secBank.style.display = "block";

        if (storeSettings.BankCode && document.getElementById("payBankName")) {
            document.getElementById("payBankName").textContent = storeSettings.BankCode;
        }
        if (storeSettings.BankAccount && document.getElementById("payBankAccount")) {
            document.getElementById("payBankAccount").textContent = storeSettings.BankAccount;
        }
        if (storeSettings.BankAccountName && document.getElementById("payBankAccName")) {
            document.getElementById("payBankAccName").textContent = storeSettings.BankAccountName;
        }
        if (document.getElementById("paymentTransferLast5")) {
            document.getElementById("paymentTransferLast5").value = "";
        }
    }

    const backdrop = document.getElementById("paymentModalBackdrop");
    if (backdrop) backdrop.classList.add("show");
}

function closePaymentModal() {
    const backdrop = document.getElementById("paymentModalBackdrop");
    if (backdrop) backdrop.classList.remove("show");
    openCartModal();
}

function copyBankAccount() {
    const acc = document.getElementById("payBankAccount")?.textContent || "129540943647";
    navigator.clipboard.writeText(acc);
    showToast("📋 銀行帳號已複製到剪貼簿！");
}

async function confirmPaymentAndSubmitOrder() {
    if (!pendingOrderPayload) return;

    if (pendingOrderPayload.paymentMethod === "BankTransfer") {
        const last5 = (document.getElementById("paymentTransferLast5")?.value || "").trim();
        if (!last5 || last5.length < 4) {
            showToast("請填寫您的轉帳帳號末 5 碼，以便核對");
            document.getElementById("paymentTransferLast5")?.focus();
            return;
        }
        pendingOrderPayload.transferLast5 = last5;
    }

    const btn = document.getElementById("btnConfirmPaidSubmit");
    if (btn) {
        btn.disabled = true;
        btn.textContent = "⏳ 訂單傳送中...";
    }

    await executeSubmitOrder(pendingOrderPayload);

    if (btn) {
        btn.disabled = false;
        btn.textContent = "✅ 我已完成付款，立即送出訂單";
    }

    const backdrop = document.getElementById("paymentModalBackdrop");
    if (backdrop) backdrop.classList.remove("show");
}

async function executeSubmitOrder(payload) {
    try {
        const res = await fetch("/api/order", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

        const data = await res.json();
        if (!res.ok) {
            throw new Error(data.message || "建立訂單失敗");
        }

        localStorage.setItem("gc_last_phone", payload.customerPhone);
        saveOrderToHistory(data.orderNumber);

        clearCart();
        closeCartModal();

        showToast("🎉 訂單已成功送出！店家將立即安排製作");
        openOrderTracker(data.orderNumber);

    } catch (err) {
        console.error("Order submission error:", err);
        showToast("❌ 下單失敗: " + err.message);
    }
}

// 8. Live Order Tracker
function openOrderTracker(orderNumber) {
    currentTrackingOrderNumber = orderNumber;
    fetchOrderDetails(orderNumber);

    const backdrop = document.getElementById("trackerModalBackdrop");
    if (backdrop) backdrop.classList.add("show");

    if (trackingInterval) clearInterval(trackingInterval);
    trackingInterval = setInterval(() => {
        if (currentTrackingOrderNumber) {
            fetchOrderStatusOnly(currentTrackingOrderNumber);
        }
    }, 4000);
}

function closeOrderTracker() {
    const backdrop = document.getElementById("trackerModalBackdrop");
    if (backdrop) backdrop.classList.remove("show");
    if (trackingInterval) clearInterval(trackingInterval);
    currentTrackingOrderNumber = null;
}

async function fetchOrderDetails(orderNumber) {
    try {
        const res = await fetch(`/api/order/${orderNumber}`);
        if (!res.ok) throw new Error("Order not found");
        const order = await res.json();
        renderOrderTrackerData(order);
    } catch (err) {
        console.error(err);
        showToast("查詢訂單失敗");
    }
}

async function fetchOrderStatusOnly(orderNumber) {
    try {
        const res = await fetch(`/api/order/${orderNumber}/status`);
        if (res.ok) {
            const data = await res.json();
            updateTrackerStatusBadge(data.orderStatus);
        }
    } catch (e) {}
}

function renderOrderTrackerData(order) {
    const numEl = document.getElementById("trackerOrderNumber");
    if (numEl) numEl.textContent = order.orderNumber;

    const custEl = document.getElementById("trackerCustomer");
    if (custEl) custEl.textContent = `${order.customerName} (${order.customerPhone})`;

    const dtEl = document.getElementById("trackerDiningType");
    if (dtEl) dtEl.textContent = order.diningType === "DineIn" ? `內用 (桌號: ${order.tableNumber || '未填'})` : "外帶自取";

    const timeEl = document.getElementById("trackerPickupTime");
    if (timeEl) timeEl.textContent = order.pickupTime;

    const payEl = document.getElementById("trackerPaymentMethod");
    if (payEl) {
        let payText = "💵 現場現金 (未付款)";
        if (order.paymentMethod === "LinePay") payText = "🟢 LINE Pay (已完成付款)";
        if (order.paymentMethod === "BankTransfer") payText = `🏦 銀行轉帳 (已轉帳 · 末五碼: ${order.transferLast5 || '已備註'})`;
        payEl.textContent = payText;
    }

    const totalEl = document.getElementById("trackerTotalAmount");
    if (totalEl) totalEl.textContent = `NT$ ${order.totalAmount}`;

    updateTrackerStatusBadge(order.orderStatus);

    const itemsList = document.getElementById("trackerItemsList");
    if (!itemsList) return;

    let html = "";
    (order.items || []).forEach(item => {
        html += `
            <div style="display:flex; justify-content:space-between; padding:6px 0; border-bottom:1px dashed var(--gc-border); font-size:13px;">
                <div>
                    <strong>${item.menuItemName}</strong> x${item.quantity}
                    ${item.selectedOptionsSummary ? `<div style="font-size:11px; color:var(--gc-text-muted);">${item.selectedOptionsSummary}</div>` : ''}
                    ${item.itemNote ? `<div style="font-size:11px; color:#b45309;">備註: ${item.itemNote}</div>` : ''}
                </div>
                <div style="font-weight:700; color:var(--gc-text-gold);">NT$ ${item.subTotal}</div>
            </div>
        `;
    });
    itemsList.innerHTML = html;
}

function updateTrackerStatusBadge(status) {
    const badge = document.getElementById("trackerStatusBadge");
    const hint = document.getElementById("trackerStatusHint");
    if (!badge) return;

    badge.className = `tracker-badge ${status}`;
    
    let text = "待確認";
    let hintText = "店家已收到您的訂單，正準備安排製作...";

    if (status === "Preparing") {
        text = "🔥 製作中";
        hintText = "主廚正在為您炭火燻烤製作，請稍候！";
    } else if (status === "Ready") {
        text = "🔔 可取餐";
        hintText = "餐點已備妥，請至櫃檯出示訂單號取餐！";
    } else if (status === "Completed") {
        text = "✨ 已完成";
        hintText = "感謝您的光臨，祝您用餐愉快！";
    } else if (status === "Cancelled") {
        text = "❌ 已取消";
        hintText = "此訂單已取消，如有疑問請聯繫門市人員。";
    }

    badge.textContent = text;
    if (hint) hint.textContent = hintText;

    const steps = ["Pending", "Preparing", "Ready", "Completed"];
    const currentIdx = steps.indexOf(status);

    for (let i = 0; i < 4; i++) {
        const stepEl = document.getElementById(`stepItem_${i}`);
        if (!stepEl) continue;
        stepEl.classList.remove("active", "completed");

        if (i < currentIdx) {
            stepEl.classList.add("completed");
        } else if (i === currentIdx) {
            stepEl.classList.add("active");
        }
    }
}

// 9. History Lookup
function openHistoryModal() {
    const lastPhone = localStorage.getItem("gc_last_phone") || "";
    const phoneInput = document.getElementById("historyPhoneInput");
    if (phoneInput && lastPhone) {
        phoneInput.value = lastPhone;
        searchOrderHistory();
    }
    const backdrop = document.getElementById("historyModalBackdrop");
    if (backdrop) backdrop.classList.add("show");
}

function closeHistoryModal() {
    const backdrop = document.getElementById("historyModalBackdrop");
    if (backdrop) backdrop.classList.remove("show");
}

async function searchOrderHistory() {
    const phone = (document.getElementById("historyPhoneInput")?.value || "").trim();
    if (!phone) {
        showToast("請輸入查詢手機號碼");
        return;
    }

    const container = document.getElementById("historyResultList");
    if (!container) return;

    container.innerHTML = `<div style="text-align:center; padding:20px; color:var(--gc-text-muted);">🔍 查詢中...</div>`;

    try {
        const res = await fetch(`/api/order/lookup?phone=${encodeURIComponent(phone)}`);
        const orders = await res.json();

        if (!orders || orders.length === 0) {
            container.innerHTML = `<div style="text-align:center; padding:20px; color:var(--gc-text-muted);">無查到此號碼的歷史訂單</div>`;
            return;
        }

        let html = "";
        orders.forEach(o => {
            html += `
                <div class="cart-item-card" style="cursor:pointer;" onclick="closeHistoryModal(); openOrderTracker('${o.orderNumber}');">
                    <div style="flex:1;">
                        <div style="display:flex; align-items:center; gap:8px;">
                            <strong style="font-size:15px; color:var(--gc-green-primary);">${o.orderNumber}</strong>
                            <span class="tracker-badge ${o.orderStatus}" style="font-size:11px; padding:2px 8px; margin:0;">${o.orderStatus}</span>
                        </div>
                        <div style="font-size:12px; color:var(--gc-text-muted); margin-top:4px;">
                            ${new Date(o.createdAt).toLocaleString()} · ${o.diningType === 'DineIn' ? '內用' : '外帶'}
                        </div>
                    </div>
                    <div style="text-align:right;">
                        <div class="item-price" style="font-size:16px;">NT$ ${o.totalAmount}</div>
                        <span style="font-size:12px; color:var(--gc-green-light); font-weight:700;">查看進度 ➔</span>
                    </div>
                </div>
            `;
        });

        container.innerHTML = html;
    } catch (err) {
        container.innerHTML = `<div style="text-align:center; padding:20px; color:#b91c1c;">查詢失敗，請重試</div>`;
    }
}

function saveOrderToHistory(orderNumber) {
    try {
        let history = JSON.parse(localStorage.getItem("gc_order_history") || "[]");
        if (!history.includes(orderNumber)) {
            history.unshift(orderNumber);
            localStorage.setItem("gc_order_history", JSON.stringify(history.slice(0, 10)));
        }
    } catch (e) {}
}

function checkUrlForOrderTracking() {
    const urlParams = new URLSearchParams(window.location.search);
    const orderNumber = urlParams.get("order");
    if (orderNumber) {
        openOrderTracker(orderNumber);
    }
}

// 10. UI Helpers
function showToast(msg) {
    const toast = document.getElementById("toastMsg");
    if (!toast) return;
    toast.textContent = msg;
    toast.classList.add("show");
    setTimeout(() => {
        toast.classList.remove("show");
    }, 2800);
}

function setupEventListeners() {
    const lastPhone = localStorage.getItem("gc_last_phone");
    if (lastPhone) {
        const phoneInput = document.getElementById("orderCustomerPhone");
        if (phoneInput) phoneInput.value = lastPhone;
    }
}
