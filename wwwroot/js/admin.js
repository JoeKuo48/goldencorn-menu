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
                errorMsg.textContent = "PIN 碼錯誤，請重新輸入 (預設: 8888)";
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

// 5. Menu & Inventory Management
async function loadAdminMenu() {
    const tbody = document.getElementById("adminMenuTableBody");
    if (!tbody) return;

    try {
        const res = await fetch("/api/admin/menu");
        if (!res.ok) return;

        allMenuItems = await res.json();
        let html = "";

        allMenuItems.forEach(item => {
            html += `
                <tr>
                    <td><strong>${item.name}</strong><br><small style="color:var(--admin-muted);">${item.englishName || ''}</small></td>
                    <td>${item.category ? item.category.name : '-'}</td>
                    <td><strong>NT$ ${item.price}</strong></td>
                    <td>${item.badge ? `<span style="background:#fef3c7; color:#92400e; font-size:11px; padding:2px 6px; border-radius:4px;">${item.badge}</span>` : '-'}</td>
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
                        <button class="btn-kds-action print" onclick="openEditItemModal(${item.id})">編輯</button>
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

function openEditItemModal(itemId) {
    const item = allMenuItems.find(i => i.id === itemId);
    if (!item) return;

    document.getElementById("editItemId").value = item.id;
    document.getElementById("editItemName").value = item.name;
    document.getElementById("editItemEnglish").value = item.englishName || "";
    document.getElementById("editItemPrice").value = item.price;
    document.getElementById("editItemBadge").value = item.badge || "";
    document.getElementById("editItemDesc").value = item.description || "";
    document.getElementById("editItemCategory").value = item.categoryId;
    document.getElementById("editItemModalTitle").textContent = "編輯餐點";

    document.getElementById("editItemModalBackdrop").classList.add("show");
}

function closeEditItemModal() {
    document.getElementById("editItemModalBackdrop").classList.remove("show");
}

async function saveMenuItem() {
    const id = document.getElementById("editItemId").value;
    const name = document.getElementById("editItemName").value.trim();
    const englishName = document.getElementById("editItemEnglish").value.trim();
    const price = parseFloat(document.getElementById("editItemPrice").value) || 0;
    const badge = document.getElementById("editItemBadge").value.trim();
    const desc = document.getElementById("editItemDesc").value.trim();
    const categoryId = parseInt(document.getElementById("editItemCategory").value) || 1;

    if (!name) {
        showAdminToast("請輸入品名");
        return;
    }

    const payload = {
        name,
        englishName,
        price,
        badge,
        description: desc,
        categoryId,
        isAvailable: true,
        displayOrder: 1
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
            showAdminToast("餐點已成功儲存");
            closeEditItemModal();
            loadAdminMenu();
        } else {
            showAdminToast("儲存失敗");
        }
    } catch (e) {
        showAdminToast("儲存失敗");
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
        if (document.getElementById("settingAdminPin")) document.getElementById("settingAdminPin").value = settings.AdminPin || "8888";
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
        AdminPin: document.getElementById("settingAdminPin")?.value || "8888"
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
