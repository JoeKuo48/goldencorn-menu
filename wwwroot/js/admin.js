/**
 * Golden Corn - Admin & Kitchen Management JS (admin.js)
 */

let allOrders = [];
let knownOrderIds = new Set();
let isAudioAlertEnabled = true;
let kdsInterval = null;
let currentFilterStatus = "active";
let allMenuItems = [];

// Initialize
document.addEventListener("DOMContentLoaded", () => {
    checkAdminAuth();
});

// 0. Security PIN Lock
function checkAdminAuth() {
    const isAuth = sessionStorage.getItem("gc_admin_auth") === "true";
    const lockModal = document.getElementById("adminAuthModalBackdrop");
    
    if (!isAuth) {
        if (lockModal) lockModal.classList.add("show");
        const pinInput = document.getElementById("inputAdminPin");
        if (pinInput) setTimeout(() => pinInput.focus(), 300);
    } else {
        if (lockModal) lockModal.classList.remove("show");
        initAdminApp();
    }
}

async function submitAdminAuth() {
    const pin = (document.getElementById("inputAdminPin").value || "").trim();
    const errorMsg = document.getElementById("authErrorMsg");

    if (!pin) {
        if (errorMsg) {
            errorMsg.textContent = "請輸入 PIN 碼";
            errorMsg.style.display = "block";
        }
        return;
    }

    try {
        const res = await fetch("/api/admin/verify-pin", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ pin })
        });

        if (res.ok) {
            sessionStorage.setItem("gc_admin_auth", "true");
            document.getElementById("adminAuthModalBackdrop").classList.remove("show");
            if (errorMsg) errorMsg.style.display = "none";
            showAdminToast("🔓 管理後台解鎖成功！");
            initAdminApp();
        } else {
            if (errorMsg) {
                errorMsg.textContent = "密碼錯誤，請重新輸入 (預設: Hawking)";
                errorMsg.style.display = "block";
            }
            document.getElementById("inputAdminPin").value = "";
            document.getElementById("inputAdminPin").focus();
        }
    } catch (e) {
        if (errorMsg) {
            errorMsg.textContent = "連線驗證失敗，請重試";
            errorMsg.style.display = "block";
        }
    }
}

function adminLogout() {
    sessionStorage.removeItem("gc_admin_auth");
    if (kdsInterval) clearInterval(kdsInterval);
    document.getElementById("inputAdminPin").value = "";
    document.getElementById("adminAuthModalBackdrop").classList.add("show");
    showAdminToast("🔒 後台已鎖定");
}

function initAdminApp() {
    setupTabNavigation();
    loadKdsOrders();
    loadAdminStats();
    loadAdminMenu();
    loadAdminHomepageSettings();
    loadAdminSettings();
    initCustomerAccessInfo();

    if (kdsInterval) clearInterval(kdsInterval);
    kdsInterval = setInterval(loadKdsOrders, 4000);
}

function initCustomerAccessInfo() {
    const origin = window.location.origin;
    const urlInput = document.getElementById("customerAccessUrl");
    if (urlInput) {
        urlInput.value = origin + "/";
    }

    const qrCanvas = document.getElementById("qrCanvas");
    if (qrCanvas && window.QRCode) {
        QRCode.toCanvas(qrCanvas, origin + "/", { width: 130, margin: 1 }, function (error) {
            if (error) console.error(error);
        });
    }
}

function copyCustomerUrl() {
    const urlInput = document.getElementById("customerAccessUrl");
    if (urlInput) {
        urlInput.select();
        navigator.clipboard.writeText(urlInput.value);
        showAdminToast("📋 點餐網址已複製到剪貼簿！");
    }
}

// 1. Tab Navigation
function setupTabNavigation() {
    const navButtons = document.querySelectorAll(".nav-link-btn");
    navButtons.forEach(btn => {
        btn.addEventListener("click", () => {
            if (btn.getAttribute("onclick")) return;

            navButtons.forEach(b => b.classList.remove("active"));
            btn.classList.add("active");

            const tabId = btn.getAttribute("data-tab");
            document.querySelectorAll(".tab-pane").forEach(pane => pane.classList.remove("active"));
            const targetPane = document.getElementById(tabId);
            if (targetPane) targetPane.classList.add("active");

            if (tabId === "tabStats") loadAdminStats();
            if (tabId === "tabMenu") loadAdminMenu();
            if (tabId === "tabHomepageCMS") loadAdminHomepageSettings();
            if (tabId === "tabSettings") {
                loadAdminSettings();
                initCustomerAccessInfo();
            }
        });
    });
}

// 2. Audio Alert
function playOrderChime() {
    if (!isAudioAlertEnabled) return;
    try {
        const audioCtx = new (window.AudioContext || window.webkitAudioContext)();
        function playTone(freq, startTime, duration) {
            const osc = audioCtx.createOscillator();
            const gain = audioCtx.createGain();
            osc.type = "sine";
            osc.frequency.setValueAtTime(freq, audioCtx.currentTime + startTime);
            gain.gain.setValueAtTime(0.3, audioCtx.currentTime + startTime);
            gain.gain.exponentialRampToValueAtTime(0.01, audioCtx.currentTime + startTime + duration);
            osc.connect(gain);
            gain.connect(audioCtx.destination);
            osc.start(audioCtx.currentTime + startTime);
            osc.stop(audioCtx.currentTime + startTime + duration);
        }
        playTone(523.25, 0, 0.2);     // C5
        playTone(659.25, 0.15, 0.2);  // E5
        playTone(783.99, 0.3, 0.35);  // G5
    } catch (e) {
        console.warn("Audio chime failed:", e);
    }
}

function toggleAudioAlert() {
    isAudioAlertEnabled = !isAudioAlertEnabled;
    const btn = document.getElementById("btnAudioToggle");
    if (btn) {
        if (isAudioAlertEnabled) {
            btn.className = "btn-audio-toggle";
            btn.innerHTML = "🔔 提示音：開";
            playOrderChime();
        } else {
            btn.className = "btn-audio-toggle muted";
            btn.innerHTML = "🔕 提示音：靜音";
        }
    }
}

// 3. KDS Orders Loading & Rendering
async function loadKdsOrders() {
    try {
        const res = await fetch(`/api/admin/orders?status=${currentFilterStatus}`);
        if (!res.ok) return;

        const orders = await res.json();
        allOrders = orders;

        let hasNewPending = false;
        orders.forEach(o => {
            if (!knownOrderIds.has(o.id) && o.orderStatus === "Pending") {
                hasNewPending = true;
            }
            knownOrderIds.add(o.id);
        });

        if (hasNewPending && knownOrderIds.size > orders.length) {
            playOrderChime();
            showAdminToast("🔔 收到新訂單！請確認出單");
        }

        renderKdsCards(orders);
        updateKdsBadgeCounts();

    } catch (err) {
        console.error("Failed to load KDS orders:", err);
    }
}

function setKdsFilter(status, btnEl) {
    currentFilterStatus = status;
    document.querySelectorAll(".kds-filter-btn").forEach(b => b.classList.remove("active"));
    if (btnEl) btnEl.classList.add("active");
    loadKdsOrders();
}

function renderKdsCards(orders) {
    const grid = document.getElementById("kdsGrid");
    if (!grid) return;

    if (orders.length === 0) {
        grid.innerHTML = `
            <div style="grid-column: 1 / -1; text-align: center; padding: 60px 0; color: var(--admin-muted);">
                <div style="font-size: 36px; margin-bottom: 8px;">📋</div>
                <div style="font-size: 16px; font-weight: 700;">目前沒有${getFilterStatusName(currentFilterStatus)}的訂單</div>
                <div style="font-size: 13px; margin-top: 4px;">新訂單送達時將自動更新並響起提示音</div>
            </div>
        `;
        return;
    }

    let html = "";
    orders.forEach(order => {
        const elapsedMins = Math.floor((new Date() - new Date(order.createdAt)) / 60000);
        const isUrgent = order.orderStatus === "Pending" && elapsedMins > 5;

        // Payment status badge
        let paymentBadgeHtml = `<span style="background:#fef3c7; color:#92400e; font-size:11px; font-weight:800; padding:2px 8px; border-radius:12px;">💵 現場現金 (未付款)</span>`;
        if (order.paymentMethod === "LinePay") {
            paymentBadgeHtml = `<span style="background:#dcfce7; color:#166534; font-size:11px; font-weight:800; padding:2px 8px; border-radius:12px;">🟢 LINE Pay (已付款)</span>`;
        } else if (order.paymentMethod === "BankTransfer") {
            paymentBadgeHtml = `<span style="background:#dbeafe; color:#1e40af; font-size:11px; font-weight:800; padding:2px 8px; border-radius:12px;">🏦 銀行轉帳 (已轉帳 · 後5碼: <strong>${order.transferLast5 || '未填'}</strong>)</span>`;
        }

        html += `
            <div class="kds-card ${order.orderStatus}" data-id="${order.id}">
                <div class="kds-card-header">
                    <div>
                        <span class="kds-order-num">${order.orderNumber}</span>
                    </div>
                    <div>
                        <span class="kds-badge ${order.diningType}">
                            ${order.diningType === 'DineIn' ? `🍽️ 內用 (${order.tableNumber || '未填'})` : '🥡 外帶自取'}
                        </span>
                    </div>
                </div>

                <div class="kds-card-meta">
                    <div>👤 <strong>${order.customerName}</strong> (${order.customerPhone})</div>
                    <div style="${isUrgent ? 'color:#dc2626; font-weight:800;' : ''}">⏱️ ${elapsedMins} 分鐘前</div>
                </div>

                <div style="padding: 4px 16px 8px;">
                    ${paymentBadgeHtml}
                </div>

                <div class="kds-items-list">
        `;

        (order.items || []).forEach(item => {
            html += `
                <div>
                    <div class="kds-item-row">
                        <div>
                            <span class="kds-item-qty">${item.quantity}x</span>
                            <span class="kds-item-name">${item.menuItemName}</span>
                        </div>
                        <div style="font-size:13px; font-weight:700; color:var(--admin-muted);">
                            NT$ ${item.subTotal}
                        </div>
                    </div>
                    ${item.selectedOptionsSummary ? `<div class="kds-item-options">↳ ${item.selectedOptionsSummary}</div>` : ''}
                    ${item.itemNote ? `<div class="kds-item-note">💬 備註: ${item.itemNote}</div>` : ''}
                </div>
            `;
        });

        html += `</div>`;

        if (order.customerNote) {
            html += `
                <div class="kds-note-box">
                    <strong>全單備註：</strong>${order.customerNote}
                </div>
            `;
        }

        html += `
                <div class="kds-card-footer">
                    <div class="kds-total-text">NT$ ${order.totalAmount}</div>
                    <div class="kds-btn-group">
                        <button class="btn-kds-action print" title="列印小票" onclick="openReceiptModal(${order.id})">🖨️</button>
                        ${renderKdsActionButtons(order)}
                    </div>
                </div>
            </div>
        `;
    });

    grid.innerHTML = html;
}

function renderKdsActionButtons(order) {
    if (order.orderStatus === "Pending") {
        return `
            <button class="btn-kds-action start" onclick="updateOrderStatus(${order.id}, 'Preparing')">接單製作 ➔</button>
            <button class="btn-kds-action cancel" onclick="updateOrderStatus(${order.id}, 'Cancelled')">拒單</button>
        `;
    } else if (order.orderStatus === "Preparing") {
        return `
            <button class="btn-kds-action ready" onclick="updateOrderStatus(${order.id}, 'Ready')">通知取餐 ➔</button>
        `;
    } else if (order.orderStatus === "Ready") {
        return `
            <button class="btn-kds-action complete" onclick="updateOrderStatus(${order.id}, 'Completed')">完成訂單 ✓</button>
        `;
    } else {
        return `
            <span style="font-size:12px; font-weight:700; color:var(--admin-muted);">${order.orderStatus}</span>
        `;
    }
}

async function updateOrderStatus(orderId, nextStatus) {
    try {
        const res = await fetch(`/api/admin/orders/${orderId}/status`, {
            method: "PUT",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ status: nextStatus })
        });

        if (res.ok) {
            showAdminToast(`訂單狀態已更新為：${getStatusName(nextStatus)}`);
            loadKdsOrders();
            loadAdminStats();
        } else {
            showAdminToast("更新狀態失敗");
        }
    } catch (err) {
        console.error(err);
        showAdminToast("更新失敗");
    }
}

async function updateKdsBadgeCounts() {
    try {
        const res = await fetch("/api/admin/stats");
        if (res.ok) {
            const stats = await res.json();
            const badge = document.getElementById("activeOrdersBadge");
            if (badge) {
                const totalActive = (stats.pendingOrdersCount || 0) + (stats.preparingOrdersCount || 0) + (stats.readyOrdersCount || 0);
                badge.textContent = totalActive;
            }
        }
    } catch (e) {}
}

function getFilterStatusName(st) {
    if (st === "active") return "進行中";
    if (st === "Pending") return "待確認";
    if (st === "Preparing") return "製作中";
    if (st === "Ready") return "待取餐";
    if (st === "Completed") return "已完成";
    return "";
}

function getStatusName(st) {
    if (st === "Pending") return "待確認";
    if (st === "Preparing") return "製作中";
    if (st === "Ready") return "可取餐";
    if (st === "Completed") return "已完成";
    if (st === "Cancelled") return "已取消";
    return st;
}

// 4. Thermal Receipt Print Modal
async function openReceiptModal(orderId) {
    try {
        const res = await fetch(`/api/admin/orders/${orderId}/receipt`);
        if (!res.ok) throw new Error("Failed to load receipt");
        const data = await res.json();
        renderReceiptView(data);
        document.getElementById("receiptModalBackdrop").classList.add("show");
    } catch (e) {
        showAdminToast("載入收據失敗");
    }
}

function closeReceiptModal() {
    document.getElementById("receiptModalBackdrop").classList.remove("show");
}

function renderReceiptView(data) {
    const o = data.order;
    const container = document.getElementById("thermalReceiptContent");
    if (!container) return;

    let itemsHtml = "";
    (o.items || []).forEach(item => {
        itemsHtml += `
            <div class="receipt-row">
                <span>${item.menuItemName} x${item.quantity}</span>
                <span>$${item.subTotal}</span>
            </div>
            ${item.selectedOptionsSummary ? `<div style="font-size:11px; color:#444; padding-left:8px;">${item.selectedOptionsSummary}</div>` : ''}
            ${item.itemNote ? `<div style="font-size:11px; color:#444; padding-left:8px;">(備註: ${item.itemNote})</div>` : ''}
        `;
    });

    let payStr = "現場現金 (未付款)";
    if (o.paymentMethod === "LinePay") payStr = "LINE Pay (已付款)";
    if (o.paymentMethod === "BankTransfer") payStr = `銀行轉帳 (已轉帳 · 末5碼: ${o.transferLast5 || '未填'})`;

    container.innerHTML = `
        <div class="receipt-header">
            <div class="receipt-store-title">${data.storeName || 'GOLDEN CORN'}</div>
            <div style="font-size:11px; margin-top:2px;">${data.brandSub || 'Texas Smoked BBQ & Soul Food'}</div>
            <div style="font-size:11px; margin-top:4px;">${data.address || ''}</div>
            <div style="font-size:11px;">電話: ${data.phone || ''}</div>
            <div class="receipt-divider"></div>
            <div style="font-size:16px; font-weight:900;">【 ${o.diningType === 'DineIn' ? `內用 桌號 ${o.tableNumber || ''}` : '外帶自取'} 】</div>
            <div style="font-size:12px; margin-top:2px;">單號: ${o.orderNumber}</div>
        </div>

        <div style="font-size:12px; margin-bottom:8px;">
            <div>時間: ${new Date(o.createdAt).toLocaleString()}</div>
            <div>顧客: ${o.customerName} (${o.customerPhone})</div>
            <div>預計: ${o.pickupTime}</div>
            ${o.customerNote ? `<div style="color:#000; font-weight:bold;">備註: ${o.customerNote}</div>` : ''}
        </div>

        <div class="receipt-divider"></div>
        <div style="margin-bottom:8px;">
            ${itemsHtml}
        </div>
        <div class="receipt-divider"></div>

        <div class="receipt-row bold">
            <span>應付總計 (TOTAL)</span>
            <span>NT$ ${o.totalAmount}</span>
        </div>
        <div class="receipt-row" style="font-size:12px;">
            <span>付款方式</span>
            <span>${payStr}</span>
        </div>

        <div class="receipt-footer">
            <div>=== 感謝您的光臨，請憑單取餐 ===</div>
            <div style="margin-top:4px; font-size:10px;">Golden Corn Texas BBQ · 現點現做</div>
        </div>
    `;
}

function printCurrentReceipt() {
    window.print();
}

// 5. Menu & Inventory Management & Category Management
let allCategories = [];

async function loadAdminMenu() {
    const tbody = document.getElementById("adminMenuTableBody");
    if (!tbody) return;

    try {
        await loadAdminCategories();

        const res = await fetch("/api/admin/menu");
        if (!res.ok) return;

        allMenuItems = await res.json();
        let html = "";

        allMenuItems.forEach(item => {
            const imgUrl = item.imageUrl || "/img/2.jpg";
            html += `
                <tr>
                    <td style="width:50px;">
                        <img src="${imgUrl}" alt="${item.name}" style="width:45px; height:45px; object-fit:cover; border-radius:6px; border:1px solid #e2e8f0;">
                    </td>
                    <td>
                        <strong>${item.name}</strong>
                        ${item.requiresPlateSides ? '<span style="background:#dcfce7; color:#166534; font-size:10px; font-weight:800; padding:1px 6px; border-radius:4px; margin-left:4px;">餐盤自選配料</span>' : ''}
                        <br><small style="color:var(--admin-muted);">${item.englishName || ''}</small>
                    </td>
                    <td>${item.category ? item.category.name : '-'}</td>
                    <td><strong>NT$ ${item.price}</strong></td>
                    <td>${item.badge ? `<span style="background:#fef3c7; color:#92400e; font-size:11px; padding:2px 6px; border-radius:4px; font-weight:700;">${item.badge}</span>` : '-'}</td>
                    <td>
                        <label class="stock-toggle-switch">
                            <span class="switch">
                                <input type="checkbox" ${item.isAvailable ? 'checked' : ''} onchange="toggleItemStock(${item.id})">
                                <span class="slider"></span>
                            </span>
                            <span style="font-size:13px; font-weight:700; color:${item.isAvailable ? '#16a34a' : '#dc2626'};">
                                ${item.isAvailable ? '供應中' : '已售完'}
                            </span>
                        </label>
                    </td>
                    <td>
                        <div style="display:flex; gap:6px;">
                            <button class="btn-kds-action print" style="padding:4px 8px; font-size:12px;" onclick="openEditItemModal(${item.id})">✏️ 編輯</button>
                            <button class="btn-kds-action cancel" style="padding:4px 8px; font-size:12px;" onclick="deleteMenuItem(${item.id})">🗑️ 刪除</button>
                        </div>
                    </td>
                </tr>
            `;
        });

        tbody.innerHTML = html;
        renderOptionStockToggles();

    } catch (err) {
        console.error("Failed to load menu items:", err);
    }
}

async function loadAdminCategories() {
    try {
        const res = await fetch("/api/admin/categories");
        if (res.ok) {
            allCategories = await res.json();
            populateCategoryDropdown();
        }
    } catch (e) {
        console.error("Failed to load categories:", e);
    }
}

function populateCategoryDropdown(selectedCatId) {
    const select = document.getElementById("editItemCategory");
    if (!select) return;

    select.innerHTML = allCategories.map(c => `
        <option value="${c.id}" ${c.id === selectedCatId ? 'selected' : ''}>
            ${c.name} (${c.englishName || ''})
        </option>
    `).join("");
}

function openCreateItemModal() {
    document.getElementById("editItemId").value = "";
    document.getElementById("editItemName").value = "";
    document.getElementById("editItemEnglish").value = "";
    document.getElementById("editItemPrice").value = "";
    document.getElementById("editItemBadge").value = "";
    document.getElementById("editItemOrder").value = "1";
    document.getElementById("editItemImageUrl").value = "/img/2.jpg";
    updateItemImagePreview("/img/2.jpg");
    document.getElementById("editItemRequiresPlateSides").checked = false;
    document.getElementById("editItemDesc").value = "";
    
    document.getElementById("editItemModalTitle").textContent = "➕ 新增餐點品項";
    document.getElementById("editItemModalSubtitle").textContent = "填寫餐點資料以新增至線上點餐菜單";

    populateCategoryDropdown(allCategories.length > 0 ? allCategories[0].id : 1);
    document.getElementById("editItemModalBackdrop").classList.add("show");
}

function openEditItemModal(itemId) {
    const item = allMenuItems.find(i => i.id === itemId);
    if (!item) return;

    document.getElementById("editItemId").value = item.id;
    document.getElementById("editItemName").value = item.name;
    document.getElementById("editItemEnglish").value = item.englishName || "";
    document.getElementById("editItemPrice").value = item.price;
    document.getElementById("editItemBadge").value = item.badge || "";
    document.getElementById("editItemOrder").value = item.displayOrder || 1;
    document.getElementById("editItemImageUrl").value = item.imageUrl || "/img/2.jpg";
    updateItemImagePreview(item.imageUrl || "/img/2.jpg");
    document.getElementById("editItemRequiresPlateSides").checked = !!item.requiresPlateSides;
    document.getElementById("editItemDesc").value = item.description || "";
    
    document.getElementById("editItemModalTitle").textContent = "✏️ 編輯餐點品項";
    document.getElementById("editItemModalSubtitle").textContent = `修改「${item.name}」的品名、價格或分類`;

    populateCategoryDropdown(item.categoryId);
    document.getElementById("editItemModalBackdrop").classList.add("show");
}

function closeEditItemModal() {
    document.getElementById("editItemModalBackdrop").classList.remove("show");
}

function updateItemImagePreview(url) {
    const preview = document.getElementById("editItemImagePreview");
    if (preview) {
        preview.src = url || "/img/2.jpg";
    }
}

function selectPresetImage(url) {
    const input = document.getElementById("editItemImageUrl");
    if (input) {
        input.value = url;
        updateItemImagePreview(url);
    }
}

async function saveMenuItem() {
    const id = document.getElementById("editItemId").value;
    const name = document.getElementById("editItemName").value.trim();
    const englishName = document.getElementById("editItemEnglish").value.trim();
    const price = parseFloat(document.getElementById("editItemPrice").value) || 0;
    const badge = document.getElementById("editItemBadge").value.trim();
    const displayOrder = parseInt(document.getElementById("editItemOrder").value) || 1;
    const imageUrl = document.getElementById("editItemImageUrl").value.trim() || "/img/2.jpg";
    const requiresPlateSides = document.getElementById("editItemRequiresPlateSides").checked;
    const desc = document.getElementById("editItemDesc").value.trim();
    const categoryId = parseInt(document.getElementById("editItemCategory").value) || 1;

    if (!name) {
        showAdminToast("⚠️ 請填寫餐點名稱");
        return;
    }
    if (price <= 0) {
        showAdminToast("⚠️ 請填寫正確售價");
        return;
    }

    const payload = {
        name,
        englishName,
        price,
        badge,
        imageUrl,
        requiresPlateSides,
        displayOrder,
        description: desc,
        categoryId,
        isAvailable: true
    };

    try {
        const url = id ? `/api/admin/menu/${id}` : "/api/admin/menu";
        const method = id ? "PUT" : "POST";

        const res = await fetch(url, {
            method: method,
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

        if (res.ok) {
            showAdminToast(id ? "✅ 餐點已成功修改！" : "✅ 新餐點已成功新增！");
            closeEditItemModal();
            loadAdminMenu();
        } else {
            const err = await res.json().catch(() => ({}));
            showAdminToast(err.message || "儲存失敗");
        }
    } catch (e) {
        showAdminToast("儲存失敗，請檢查網路連線");
    }
}

async function deleteMenuItem(itemId) {
    const item = allMenuItems.find(i => i.id === itemId);
    const itemName = item ? item.name : "此餐點";

    if (!confirm(`確定要刪除「${itemName}」嗎？刪除後前台將不再顯示此品項。`)) {
        return;
    }

    try {
        const res = await fetch(`/api/admin/menu/${itemId}`, { method: "DELETE" });
        if (res.ok) {
            showAdminToast(`🗑️ 已刪除「${itemName}」`);
            loadAdminMenu();
        } else {
            const err = await res.json().catch(() => ({}));
            showAdminToast(err.message || "刪除失敗");
        }
    } catch (e) {
        showAdminToast("連線失敗");
    }
}

// Category Management Functions
function openCategoryManagerModal() {
    renderCategoryList();
    resetCategoryForm();
    document.getElementById("categoryModalBackdrop").classList.add("show");
}

function closeCategoryManagerModal() {
    document.getElementById("categoryModalBackdrop").classList.remove("show");
}

function renderCategoryList() {
    const container = document.getElementById("adminCategoryListContainer");
    if (!container) return;

    if (allCategories.length === 0) {
        container.innerHTML = `<div style="color:var(--admin-muted); font-size:13px;">目前尚無分類</div>`;
        return;
    }

    let html = `
        <table style="width:100%; border-collapse:collapse; font-size:13px;">
            <thead>
                <tr style="border-bottom:2px solid var(--admin-border); text-align:left;">
                    <th style="padding:6px;">排序</th>
                    <th style="padding:6px;">分類名稱</th>
                    <th style="padding:6px;">英文名稱</th>
                    <th style="padding:6px; text-align:right;">操作</th>
                </tr>
            </thead>
            <tbody>
    `;

    allCategories.forEach(cat => {
        html += `
            <tr style="border-bottom:1px solid var(--admin-border);">
                <td style="padding:8px 6px;">#${cat.displayOrder}</td>
                <td style="padding:8px 6px; font-weight:700;">${cat.name}</td>
                <td style="padding:8px 6px; color:var(--admin-muted);">${cat.englishName || '-'}</td>
                <td style="padding:8px 6px; text-align:right;">
                    <button class="btn-kds-action print" style="padding:2px 6px; font-size:11px;" onclick="editCategoryInline(${cat.id})">編輯</button>
                    <button class="btn-kds-action cancel" style="padding:2px 6px; font-size:11px;" onclick="deleteCategory(${cat.id})">刪除</button>
                </td>
            </tr>
        `;
    });

    html += `</tbody></table>`;
    container.innerHTML = html;
}

function resetCategoryForm() {
    document.getElementById("manageCatId").value = "";
    document.getElementById("manageCatName").value = "";
    document.getElementById("manageCatEng").value = "";
    document.getElementById("manageCatOrder").value = (allCategories.length + 1).toString();
    document.getElementById("manageCatDesc").value = "";
    document.getElementById("catFormTitle").textContent = "➕ 新增分類";
    const cancelBtn = document.getElementById("btnCancelCatEdit");
    if (cancelBtn) cancelBtn.style.display = "none";
}

function editCategoryInline(catId) {
    const cat = allCategories.find(c => c.id === catId);
    if (!cat) return;

    document.getElementById("manageCatId").value = cat.id;
    document.getElementById("manageCatName").value = cat.name;
    document.getElementById("manageCatEng").value = cat.englishName || "";
    document.getElementById("manageCatOrder").value = cat.displayOrder;
    document.getElementById("manageCatDesc").value = cat.description || "";
    document.getElementById("catFormTitle").textContent = `✏️ 編輯分類「${cat.name}」`;
    
    const cancelBtn = document.getElementById("btnCancelCatEdit");
    if (cancelBtn) cancelBtn.style.display = "inline-block";
}

async function saveCategory() {
    const id = document.getElementById("manageCatId").value;
    const name = document.getElementById("manageCatName").value.trim();
    const englishName = document.getElementById("manageCatEng").value.trim();
    const displayOrder = parseInt(document.getElementById("manageCatOrder").value) || 1;
    const desc = document.getElementById("manageCatDesc").value.trim();

    if (!name) {
        showAdminToast("⚠️ 請輸入分類名稱");
        return;
    }

    const payload = {
        name,
        englishName,
        displayOrder,
        description: desc,
        isActive: true
    };

    try {
        const url = id ? `/api/admin/categories/${id}` : "/api/admin/categories";
        const method = id ? "PUT" : "POST";

        const res = await fetch(url, {
            method,
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

        if (res.ok) {
            showAdminToast(id ? "✅ 分類已更新" : "✅ 分類已新增");
            await loadAdminCategories();
            renderCategoryList();
            resetCategoryForm();
            loadAdminMenu();
        } else {
            const err = await res.json().catch(() => ({}));
            showAdminToast(err.message || "儲存分類失敗");
        }
    } catch (e) {
        showAdminToast("連線失敗");
    }
}

async function deleteCategory(catId) {
    const cat = allCategories.find(c => c.id === catId);
    const catName = cat ? cat.name : "此分類";

    if (!confirm(`確定要刪除分類「${catName}」嗎？若底下有餐點將無法刪除。`)) {
        return;
    }

    try {
        const res = await fetch(`/api/admin/categories/${catId}`, { method: "DELETE" });
        if (res.ok) {
            showAdminToast(`🗑️ 分類「${catName}」已刪除`);
            await loadAdminCategories();
            renderCategoryList();
            loadAdminMenu();
        } else {
            const err = await res.json().catch(() => ({}));
            showAdminToast(err.message || "刪除失敗");
        }
    } catch (e) {
        showAdminToast("連線失敗");
    }
}

function renderOptionStockToggles() {
    const container = document.getElementById("adminOptionsStockContainer");
    if (!container) return;

    const optionMap = new Map();
    allMenuItems.forEach(m => {
        (m.optionGroups || []).forEach(g => {
            (g.options || []).forEach(o => {
                if (!optionMap.has(o.name)) {
                    optionMap.set(o.name, { ...o, groupName: g.name });
                }
            });
        });
    });

    let html = "";
    optionMap.forEach((opt) => {
        html += `
            <div style="background:#f8fafc; border:1px solid var(--admin-border); border-radius:8px; padding:10px 14px; display:flex; justify-content:space-between; align-items:center;">
                <div>
                    <strong>${opt.name}</strong> <small style="color:var(--admin-muted);">(${opt.groupName})</small>
                    <div style="font-size:11px; color:var(--gc-gold); font-weight:700;">+NT$ ${opt.extraPrice}</div>
                </div>
                <label class="stock-toggle-switch">
                    <span class="switch">
                        <input type="checkbox" ${opt.isAvailable ? 'checked' : ''} onchange="toggleOptionStock(${opt.id})">
                        <span class="slider"></span>
                    </span>
                    <span style="font-size:12px; font-weight:700; color:${opt.isAvailable ? '#16a34a' : '#dc2626'};">
                        ${opt.isAvailable ? '供應中' : '售完'}
                    </span>
                </label>
            </div>
        `;
    });

    container.innerHTML = html;
}

async function toggleItemStock(itemId) {
    try {
        const res = await fetch(`/api/admin/menu/${itemId}/toggle-availability`, { method: "POST" });
        if (res.ok) {
            showAdminToast("餐點庫存狀態已更新");
            loadAdminMenu();
        }
    } catch (e) {
        showAdminToast("更新庫存失敗");
    }
}

async function toggleOptionStock(optionId) {
    try {
        const res = await fetch(`/api/admin/options/${optionId}/toggle-availability`, { method: "POST" });
        if (res.ok) {
            showAdminToast("配料/加購項目庫存已更新");
            loadAdminMenu();
        }
    } catch (e) {
        showAdminToast("更新配料庫存失敗");
    }
}

// 6. Analytics & Statistics
async function loadAdminStats() {
    try {
        const res = await fetch("/api/admin/stats");
        if (!res.ok) return;

        const stats = await res.json();
        document.getElementById("statTodayRevenue").textContent = `NT$ ${stats.todayRevenue || 0}`;
        document.getElementById("statTodayOrders").textContent = stats.todayOrdersCount || 0;
        document.getElementById("statAvgOrder").textContent = `NT$ ${Math.round(stats.averageOrderAmount || 0)}`;
        document.getElementById("statPendingCount").textContent = stats.pendingOrdersCount || 0;

        const topList = document.getElementById("statTopItemsList");
        if (topList && stats.topSellingItems) {
            let html = "";
            const maxQty = stats.topSellingItems.length > 0 ? Math.max(...stats.topSellingItems.map(i => i.totalQuantity)) : 1;

            stats.topSellingItems.forEach((item, idx) => {
                const percent = Math.round((item.totalQuantity / maxQty) * 100);
                html += `
                    <div style="margin-bottom:12px;">
                        <div style="display:flex; justify-content:space-between; font-size:13px; font-weight:700; margin-bottom:4px;">
                            <span>#${idx + 1} ${item.itemName}</span>
                            <span>${item.totalQuantity} 份 (NT$ ${item.totalSales})</span>
                        </div>
                        <div style="background:#e2e8f0; border-radius:4px; height:8px; overflow:hidden;">
                            <div style="background:var(--gc-gold); width:${percent}%; height:100%;"></div>
                        </div>
                    </div>
                `;
            });
            topList.innerHTML = html || "<div style='color:var(--admin-muted);'>暫無銷售數據</div>";
        }

    } catch (e) {
        console.error("Stats load failed:", e);
    }
}

// 7. Store Settings
async function loadAdminSettings() {
    try {
        const res = await fetch("/api/settings");
        if (!res.ok) return;
        const settings = await res.json();

        if (document.getElementById("settingStoreName")) document.getElementById("settingStoreName").value = settings.StoreName || "";
        if (document.getElementById("settingBrandSub")) document.getElementById("settingBrandSub").value = settings.BrandSub || "";
        if (document.getElementById("settingPhone")) document.getElementById("settingPhone").value = settings.Phone || "";
        if (document.getElementById("settingAddress")) document.getElementById("settingAddress").value = settings.Address || "";
        if (document.getElementById("settingHours")) document.getElementById("settingHours").value = settings.BusinessHours || "";
        if (document.getElementById("settingAnnouncement")) document.getElementById("settingAnnouncement").value = settings.Announcement || "";
        if (document.getElementById("settingIsOpen")) document.getElementById("settingIsOpen").value = settings.IsOpen || "true";
        if (document.getElementById("settingStoreTip")) document.getElementById("settingStoreTip").value = settings.StoreTip || "";
        if (document.getElementById("settingBankCode")) document.getElementById("settingBankCode").value = settings.BankCode || "";
        if (document.getElementById("settingBankAccount")) document.getElementById("settingBankAccount").value = settings.BankAccount || "";
        if (document.getElementById("settingBankAccountName")) document.getElementById("settingBankAccountName").value = settings.BankAccountName || "";
        if (document.getElementById("settingAdminPin")) document.getElementById("settingAdminPin").value = settings.AdminPin || "Hawking";
    } catch (e) {
        console.error("Settings load failed:", e);
    }
}

async function saveAdminSettings() {
    const payload = {
        StoreName: document.getElementById("settingStoreName")?.value || "",
        BrandSub: document.getElementById("settingBrandSub")?.value || "",
        Phone: document.getElementById("settingPhone")?.value || "",
        Address: document.getElementById("settingAddress")?.value || "",
        BusinessHours: document.getElementById("settingHours")?.value || "",
        Announcement: document.getElementById("settingAnnouncement")?.value || "",
        IsOpen: document.getElementById("settingIsOpen")?.value || "true",
        StoreTip: document.getElementById("settingStoreTip")?.value || "",
        BankCode: document.getElementById("settingBankCode")?.value || "",
        BankAccount: document.getElementById("settingBankAccount")?.value || "",
        BankAccountName: document.getElementById("settingBankAccountName")?.value || "",
        AdminPin: document.getElementById("settingAdminPin")?.value || "Hawking"
    };

    try {
        const res = await fetch("/api/settings", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

        if (res.ok) {
            showAdminToast("門市、收款與安全設定已成功儲存！");
        } else {
            showAdminToast("設定儲存失敗");
        }
    } catch (e) {
        showAdminToast("連線失敗");
    }
}

// 8. Homepage & Portals CMS Settings
async function loadAdminHomepageSettings() {
    try {
        const res = await fetch("/api/settings");
        if (!res.ok) return;
        const s = await res.json();

        // Card 1: Cover
        if (document.getElementById("cmsCoverImageUrl")) document.getElementById("cmsCoverImageUrl").value = s.CoverImageUrl || "/img/cover_poster.png";
        if (document.getElementById("cmsCoverAnnouncementBtnText")) document.getElementById("cmsCoverAnnouncementBtnText").value = s.CoverAnnouncementBtnText || "門市公告資訊";
        if (document.getElementById("cmsCoverSwipeBtnText")) document.getElementById("cmsCoverSwipeBtnText").value = s.CoverSwipeBtnText || "SWIPE FOR MY STORY... →";

        // Card 2: Header
        if (document.getElementById("cmsPortalPillTag")) document.getElementById("cmsPortalPillTag").value = s.PortalPillTag || "CHOOSE YOUR ORDER TYPE";
        if (document.getElementById("cmsPortalTitle")) document.getElementById("cmsPortalTitle").value = s.PortalTitle || "選擇您的美味模式";
        if (document.getElementById("cmsPortalSubtitle")) document.getElementById("cmsPortalSubtitle").value = s.PortalSubtitle || "外公的田間貨櫃工作室 · 柴火低溫慢燻 · 慢靈魂料理";
        if (document.getElementById("cmsPortalNoticeBtnText")) document.getElementById("cmsPortalNoticeBtnText").value = s.PortalNoticeBtnText || "📢 門市公告與營業資訊";
        if (document.getElementById("cmsPortalHistoryBtnText")) document.getElementById("cmsPortalHistoryBtnText").value = s.PortalHistoryBtnText || "📱 查詢我的訂單";

        // Card 3: Box A
        if (document.getElementById("cmsBoxABadge")) document.getElementById("cmsBoxABadge").value = s.BoxABadge || "[ 團體聚會 · 露營野餐 · 預約外燴 ]";
        if (document.getElementById("cmsBoxAIcon")) document.getElementById("cmsBoxAIcon").value = s.BoxAIcon || "A";
        if (document.getElementById("cmsBoxATitle")) document.getElementById("cmsBoxATitle").value = s.BoxATitle || "A. 派對餐盒";
        if (document.getElementById("cmsBoxASubtitle")) document.getElementById("cmsBoxASubtitle").value = s.BoxASubtitle || "PARTY BOX & CATERING";
        if (document.getElementById("cmsBoxADesc")) document.getElementById("cmsBoxADesc").value = s.BoxADesc || "專為露營野餐、朋友聚會、公司下午茶與派對打造的大份量美式分享盛宴！";
        if (document.getElementById("cmsBoxABullets")) document.getElementById("cmsBoxABullets").value = s.BoxABullets || "德州慢燻牛胸肉 / 焦糖豬肋排大拼盤\n美式靈魂炸雞派對桶\n炭烤黃金玉米 & 主廚特調雙醬";
        if (document.getElementById("cmsBoxABtnText")) document.getElementById("cmsBoxABtnText").value = s.BoxABtnText || "探索派對餐盒";
        if (document.getElementById("cmsBoxALink")) document.getElementById("cmsBoxALink").value = s.BoxALink || "/party.html";

        // Card 4: Box B
        if (document.getElementById("cmsBoxBBadge")) document.getElementById("cmsBoxBBadge").value = s.BoxBBadge || "[ 現點現烤 · A+B+C 自由配 · 預約外帶 ]";
        if (document.getElementById("cmsBoxBIcon")) document.getElementById("cmsBoxBIcon").value = s.BoxBIcon || "B";
        if (document.getElementById("cmsBoxBTitle")) document.getElementById("cmsBoxBTitle").value = s.BoxBTitle || "B. 門市菜單";
        if (document.getElementById("cmsBoxBSubtitle")) document.getElementById("cmsBoxBSubtitle").value = s.BoxBSubtitle || "DINER MENU & ORDERING";
        if (document.getElementById("cmsBoxBDesc")) document.getElementById("cmsBoxBDesc").value = s.BoxBDesc || "個人獨享或雙人經典美式餐盤！自由搭配主餐肉類、美式配菜與自選澱粉。";
        if (document.getElementById("cmsBoxBBullets")) document.getElementById("cmsBoxBBullets").value = s.BoxBBullets || "德州燻烤牛胸肉、豬梅花、古巴烤豬排\n招牌美式奶油玉米、蘋果捲心菜\n線上預約取餐時間、Line Pay 快速結帳";
        if (document.getElementById("cmsBoxBBtnText")) document.getElementById("cmsBoxBBtnText").value = s.BoxBBtnText || "進入線上點餐";
        if (document.getElementById("cmsBoxBLink")) document.getElementById("cmsBoxBLink").value = s.BoxBLink || "/menu.html";

        // Card 5: Story
        if (document.getElementById("cmsStoryQuote")) document.getElementById("cmsStoryQuote").value = s.StoryQuote || "「世界太快，我們在田裡為你製造一點慢靈魂。」";
        if (document.getElementById("cmsStoryAuthor")) document.getElementById("cmsStoryAuthor").value = s.StoryAuthor || "GOLDEN CORN · 慢靈魂製造所 · 柴火煙燻工作室";

        // Card 6: Notice Modal
        if (document.getElementById("cmsNoticeDelivery")) document.getElementById("cmsNoticeDelivery").value = s.NoticeDelivery || "• 本店餐點皆為低溫慢火柴燻、現點現烤，建議提前預約以確保肉品庫存。\n• 外送服務：以 LALAMOVE 平台配送（例如 7.5 公里機車外送約 140 元）。\n• 試營運優惠：消費滿 $600 免運！再送美式燒烤玉米肋排乙份！";
        if (document.getElementById("cmsNoticeContact")) document.getElementById("cmsNoticeContact").value = s.NoticeContact || "企業商務套餐、下午茶點心團購、野餐派對包，請私訊 IG 粉專由主廚一對一服務：\n📸 IG: @goldencorn_diner\n📞 電話: 0910-237-105";

        // Party Box (party.html)
        if (document.getElementById("cmsPartyHeroBadge")) document.getElementById("cmsPartyHeroBadge").value = s.PartyHeroBadge || "PARTY BOX & CATERING";
        if (document.getElementById("cmsPartyHeroTitle")) document.getElementById("cmsPartyHeroTitle").value = s.PartyHeroTitle || "美式煙燻派對餐盒";
        if (document.getElementById("cmsPartyHeroSubtitle")) document.getElementById("cmsPartyHeroSubtitle").value = s.PartyHeroSubtitle || "露營野餐 · 朋友歡聚 · 企業團訂 · 生日派對的大份量美式靈魂盛宴！";
        if (document.getElementById("cmsPartyHeroTags")) document.getElementById("cmsPartyHeroTags").value = s.PartyHeroTags || "16小時原木柴燒慢燻 | 現炸美式靈魂炸雞 | 招牌炭烤黃金玉米";
        if (document.getElementById("cmsPartyNoticeTitle")) document.getElementById("cmsPartyNoticeTitle").value = s.PartyNoticeTitle || "【派對餐盒預訂須知】";
        if (document.getElementById("cmsPartyNoticeText")) document.getElementById("cmsPartyNoticeText").value = s.PartyNoticeText || "派對餐盒採大份量客製化低溫燻烤製作，需提前 2~3 天 預訂，讓主廚有充裕時間備料與長時間慢火煙燻。目前線上菜單自選功能籌備中，歡迎直接透過 IG 或電話與主廚預訂！";

        if (document.getElementById("cmsPartySet1Badge")) document.getElementById("cmsPartySet1Badge").value = s.PartySet1Badge || "人氣首選 4~6人";
        if (document.getElementById("cmsPartySet1Title")) document.getElementById("cmsPartySet1Title").value = s.PartySet1Title || "德州慢燻狂歡肉品盛宴盒";
        if (document.getElementById("cmsPartySet1Subtitle")) document.getElementById("cmsPartySet1Subtitle").value = s.PartySet1Subtitle || "Texas Smoked BBQ Carnivore Feast";
        if (document.getElementById("cmsPartySet1Desc")) document.getElementById("cmsPartySet1Desc").value = s.PartySet1Desc || "肉食愛好者的終極救贖！一次品嚐三種經典低溫煙燻肉品。";
        if (document.getElementById("cmsPartySet1Items")) document.getElementById("cmsPartySet1Items").value = s.PartySet1Items || "德州柴燒慢燻牛胸肉 400g\n經典焦糖煙燻豬肋排 半扇\n獨門香料手撕豬梅花 300g\n附贈：美式蘋果捲心菜、特製酸黃瓜、自製BBQ醬";

        if (document.getElementById("cmsPartySet2Badge")) document.getElementById("cmsPartySet2Badge").value = s.PartySet2Badge || "聚會必點 4~6人";
        if (document.getElementById("cmsPartySet2Title")) document.getElementById("cmsPartySet2Title").value = s.PartySet2Title || "靈魂炸雞 & 黃金玉米歡聚組";
        if (document.getElementById("cmsPartySet2Subtitle")) document.getElementById("cmsPartySet2Subtitle").value = s.PartySet2Subtitle || "Soul Fried Chicken & Corn Ribs Box";
        if (document.getElementById("cmsPartySet2Desc")) document.getElementById("cmsPartySet2Desc").value = s.PartySet2Desc || "外酥內嫩的南方靈魂炸雞，搭配超人氣炭烤玉米肋排。";
        if (document.getElementById("cmsPartySet2Items")) document.getElementById("cmsPartySet2Items").value = s.PartySet2Items || "美式酸奶酥脆炸雞柳條 12 塊\n楓糖水牛城微辣炸雞 8 塊\n美式燒烤黃金玉米肋排 8 支\n附贈：海鹽美式細薯、蒔蘿優格沾醬";

        if (document.getElementById("cmsPartySet3Badge")) document.getElementById("cmsPartySet3Badge").value = s.PartySet3Badge || "戶外露營神器";
        if (document.getElementById("cmsPartySet3Title")) document.getElementById("cmsPartySet3Title").value = s.PartySet3Title || "露營野餐低溫真空即享組";
        if (document.getElementById("cmsPartySet3Subtitle")) document.getElementById("cmsPartySet3Subtitle").value = s.PartySet3Subtitle || "Outdoor Camping Ready-to-Heat Pack";
        if (document.getElementById("cmsPartySet3Desc")) document.getElementById("cmsPartySet3Desc").value = s.PartySet3Desc || "主廚已煙燻熟成並低溫真空封裝。隔水加熱即可享受大師級烤肉！";
        if (document.getElementById("cmsPartySet3Items")) document.getElementById("cmsPartySet3Items").value = s.PartySet3Items || "熟成煙燻牛胸肉真空包 2 包\n古巴風烤豬排真空包 2 包\n雞汁小米飯 & 奶油玉米包 各 2 包\n附贈專用復熱教學卡與特調香料粉";

        if (document.getElementById("cmsPartyInquiryTitle")) document.getElementById("cmsPartyInquiryTitle").value = s.PartyInquiryTitle || "預約與客製化洽詢";
        if (document.getElementById("cmsPartyInquiryDesc")) document.getElementById("cmsPartyInquiryDesc").value = s.PartyInquiryDesc || "不論是家庭聚餐、朋友露營、公司慶生或大型活動外燴，主廚皆可為您量身規劃份量與菜色組合！";
        if (document.getElementById("cmsPartyInquiryIgText")) document.getElementById("cmsPartyInquiryIgText").value = s.PartyInquiryIgText || "私訊 IG 預約：@goldencorn_diner";
        if (document.getElementById("cmsPartyInquiryIgLink")) document.getElementById("cmsPartyInquiryIgLink").value = s.PartyInquiryIgLink || "https://www.instagram.com/goldencorn_diner/";
        if (document.getElementById("cmsPartyInquiryPhoneText")) document.getElementById("cmsPartyInquiryPhoneText").value = s.PartyInquiryPhoneText || "電話洽詢：0910-237-105";
        if (document.getElementById("cmsPartyInquiryPhoneLink")) document.getElementById("cmsPartyInquiryPhoneLink").value = s.PartyInquiryPhoneLink || "tel:0910237105";

        updateImgPreview('cmsCoverImageUrl', 'coverImgPreview');
        updateIconPreview('cmsBoxAIcon', 'boxAIconPreview');
        updateIconPreview('cmsBoxBIcon', 'boxBIconPreview');

    } catch (e) {
        console.error("Homepage settings load failed:", e);
    }
}

function handleImageUpload(e, targetInputId, previewId) {
    const file = e.target.files[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = function(evt) {
        const base64 = evt.target.result;
        const input = document.getElementById(targetInputId);
        if (input) input.value = base64;
        const preview = document.getElementById(previewId);
        if (preview) {
            if (preview.tagName === 'IMG') {
                preview.src = base64;
                preview.style.display = "inline-block";
            } else {
                preview.innerHTML = `<img src="${base64}" style="max-height:48px; border-radius:6px;">`;
            }
        }
        showAdminToast("📷 照片已選取！請記得點擊最下方【儲存設定】按鈕");
    };
    reader.readAsDataURL(file);
}

function updateImgPreview(inputId, previewImgId) {
    const val = document.getElementById(inputId)?.value || "/img/cover_poster.png";
    const p = document.getElementById(previewImgId);
    if (p) p.src = val;
}

function updateIconPreview(inputId, previewId) {
    const val = (document.getElementById(inputId)?.value || "").trim();
    const p = document.getElementById(previewId);
    if (!p) return;
    if (val.startsWith("http://") || val.startsWith("https://") || val.startsWith("/") || val.startsWith("data:image")) {
        p.innerHTML = `<img src="${val}" style="max-height:48px; border-radius:6px;">`;
    } else {
        p.textContent = val || "🍱";
    }
}

async function saveAdminHomepageSettings() {
    const payload = {
        CoverImageUrl: document.getElementById("cmsCoverImageUrl")?.value || "/img/cover_poster.png",
        CoverAnnouncementBtnText: document.getElementById("cmsCoverAnnouncementBtnText")?.value || "門市公告資訊",
        CoverSwipeBtnText: document.getElementById("cmsCoverSwipeBtnText")?.value || "SWIPE FOR MY STORY... →",

        PortalPillTag: document.getElementById("cmsPortalPillTag")?.value || "CHOOSE YOUR ORDER TYPE",
        PortalTitle: document.getElementById("cmsPortalTitle")?.value || "選擇您的美味模式",
        PortalSubtitle: document.getElementById("cmsPortalSubtitle")?.value || "外公的田間貨櫃工作室 · 柴火低溫慢燻 · 慢靈魂料理",
        PortalNoticeBtnText: document.getElementById("cmsPortalNoticeBtnText")?.value || "📢 門市公告與營業資訊",
        PortalHistoryBtnText: document.getElementById("cmsPortalHistoryBtnText")?.value || "📱 查詢我的訂單",

        BoxABadge: document.getElementById("cmsBoxABadge")?.value || "",
        BoxAIcon: document.getElementById("cmsBoxAIcon")?.value || "🍱",
        BoxATitle: document.getElementById("cmsBoxATitle")?.value || "",
        BoxASubtitle: document.getElementById("cmsBoxASubtitle")?.value || "",
        BoxADesc: document.getElementById("cmsBoxADesc")?.value || "",
        BoxABullets: document.getElementById("cmsBoxABullets")?.value || "",
        BoxABtnText: document.getElementById("cmsBoxABtnText")?.value || "",
        BoxALink: document.getElementById("cmsBoxALink")?.value || "/party.html",

        BoxBBadge: document.getElementById("cmsBoxBBadge")?.value || "",
        BoxBIcon: document.getElementById("cmsBoxBIcon")?.value || "🌽",
        BoxBTitle: document.getElementById("cmsBoxBTitle")?.value || "",
        BoxBSubtitle: document.getElementById("cmsBoxBSubtitle")?.value || "",
        BoxBDesc: document.getElementById("cmsBoxBDesc")?.value || "",
        BoxBBullets: document.getElementById("cmsBoxBBullets")?.value || "",
        BoxBBtnText: document.getElementById("cmsBoxBBtnText")?.value || "",
        BoxBLink: document.getElementById("cmsBoxBLink")?.value || "/menu.html",

        StoryQuote: document.getElementById("cmsStoryQuote")?.value || "",
        StoryAuthor: document.getElementById("cmsStoryAuthor")?.value || "",

        NoticeDelivery: document.getElementById("cmsNoticeDelivery")?.value || "",
        NoticeContact: document.getElementById("cmsNoticeContact")?.value || "",

        // Party Box Settings
        PartyHeroBadge: document.getElementById("cmsPartyHeroBadge")?.value || "",
        PartyHeroTitle: document.getElementById("cmsPartyHeroTitle")?.value || "",
        PartyHeroSubtitle: document.getElementById("cmsPartyHeroSubtitle")?.value || "",
        PartyHeroTags: document.getElementById("cmsPartyHeroTags")?.value || "",
        PartyNoticeTitle: document.getElementById("cmsPartyNoticeTitle")?.value || "",
        PartyNoticeText: document.getElementById("cmsPartyNoticeText")?.value || "",

        PartySet1Badge: document.getElementById("cmsPartySet1Badge")?.value || "",
        PartySet1Title: document.getElementById("cmsPartySet1Title")?.value || "",
        PartySet1Subtitle: document.getElementById("cmsPartySet1Subtitle")?.value || "",
        PartySet1Desc: document.getElementById("cmsPartySet1Desc")?.value || "",
        PartySet1Items: document.getElementById("cmsPartySet1Items")?.value || "",

        PartySet2Badge: document.getElementById("cmsPartySet2Badge")?.value || "",
        PartySet2Title: document.getElementById("cmsPartySet2Title")?.value || "",
        PartySet2Subtitle: document.getElementById("cmsPartySet2Subtitle")?.value || "",
        PartySet2Desc: document.getElementById("cmsPartySet2Desc")?.value || "",
        PartySet2Items: document.getElementById("cmsPartySet2Items")?.value || "",

        PartySet3Badge: document.getElementById("cmsPartySet3Badge")?.value || "",
        PartySet3Title: document.getElementById("cmsPartySet3Title")?.value || "",
        PartySet3Subtitle: document.getElementById("cmsPartySet3Subtitle")?.value || "",
        PartySet3Desc: document.getElementById("cmsPartySet3Desc")?.value || "",
        PartySet3Items: document.getElementById("cmsPartySet3Items")?.value || "",

        PartyInquiryTitle: document.getElementById("cmsPartyInquiryTitle")?.value || "",
        PartyInquiryDesc: document.getElementById("cmsPartyInquiryDesc")?.value || "",
        PartyInquiryIgText: document.getElementById("cmsPartyInquiryIgText")?.value || "",
        PartyInquiryIgLink: document.getElementById("cmsPartyInquiryIgLink")?.value || "",
        PartyInquiryPhoneText: document.getElementById("cmsPartyInquiryPhoneText")?.value || "",
        PartyInquiryPhoneLink: document.getElementById("cmsPartyInquiryPhoneLink")?.value || ""
    };

    try {
        const res = await fetch("/api/settings", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

        if (res.ok) {
            showAdminToast("✅ 首頁與派對餐盒所有文案設定已成功儲存！");
        } else {
            showAdminToast("設定儲存失敗");
        }
    } catch (e) {
        showAdminToast("連線失敗");
    }
}

// Toast Helper
function showAdminToast(msg) {
    const toast = document.getElementById("adminToast");
    if (!toast) return;
    toast.textContent = msg;
    toast.classList.add("show");
    setTimeout(() => {
        toast.classList.remove("show");
    }, 2800);
}
